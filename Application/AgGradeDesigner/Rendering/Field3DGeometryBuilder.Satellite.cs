using System.Runtime.InteropServices;

using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Rendering.Vulkan;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    partial class Field3DGeometryBuilder
    {
        /// <summary>Fraction of horizontal extent used for satellite plinth thickness.</summary>
        private const double SATELLITE_PLINTH_THICKNESS_FRACTION = 0.012;
        /// <summary>Minimum plinth side thickness in model units.</summary>
        private const double SATELLITE_PLINTH_MIN_MODEL_THICKNESS = 1.5;
        /// <summary>Multiplier applied to plinth half-span when computing tile coverage diagonal.</summary>
        private const double PLINTH_DIAGONAL_SIDE_MULTIPLIER = 2.0;
        /// <summary>Metres per pixel hint passed to plinth tile layout (0 = auto from zoom).</summary>
        private const double PLINTH_TILE_METRES_PER_PIXEL = 0.0;
        /// <summary>GPU vertices emitted per subdivided texture cell (two triangles).</summary>
        private const int TRIANGLES_PER_TEXTURE_SUBCELL = 6;
        /// <summary>One quad per tile is sufficient for near-nadir plan view.</summary>
        private const int PLAN_VIEW_TILE_SUBDIVISIONS = 1;
        /// <summary>Maximum viewport satellite tiles to cap GPU upload during plan view.</summary>
        private const int PLAN_VIEW_MAX_TILE_COUNT = 192;
        /// <summary>Bytes per RGBA pixel when copying tile bitmaps for GPU upload.</summary>
        private const int RGBA_BYTES_PER_PIXEL = 4;

        /// <summary>Stroke colour for plinth side faces.</summary>
        private static readonly SKColor PlinthEdgeColor = SKColors.Black;

        /// <summary>
        /// Adds viewport-aligned satellite tiles for near-nadir plan view (2D mode).
        /// </summary>
        static private void AppendPlanViewSatelliteBackground
            (
            List<GpuTextureMesh> TextureMeshes,
            ref double DepthMin,
            ref double DepthMax,
            SatelliteTileService Satellite,
            FieldCamera3D Camera,
            UTM.UTMCoordinate FieldCentreUtm,
            SKRect Viewport,
            double PlaneElevation
            )
        {
            double MetresPerPixel = Camera.EstimateGroundMetresPerScreenPixel(Viewport);
            SatelliteMapViewport Layout = SatelliteMapLayout.Compute(
                Camera.CentreLat,
                Camera.CentreLon,
                Viewport.Width,
                Viewport.Height,
                MetresPerPixel);

            int TilesPerAxis = 1 << Layout.Zoom;
            foreach (SatelliteTileRequest Tile in Layout.Tiles)
            {
                if (Tile.TileY < 0 || Tile.TileY >= TilesPerAxis)
                    continue;

                WebMercator.TileLatLonBounds(
                    Layout.Zoom,
                    Tile.TileX,
                    Tile.TileY,
                    out double RawMinLon,
                    out double RawMinLat,
                    out double RawMaxLon,
                    out double RawMaxLat);
                double TileMinLat = Math.Min(RawMinLat, RawMaxLat);
                double TileMaxLat = Math.Max(RawMinLat, RawMaxLat);
                double TileMinLon = Math.Min(RawMinLon, RawMaxLon);
                double TileMaxLon = Math.Max(RawMinLon, RawMaxLon);

                SKBitmap? Bitmap = Satellite.GetTileBitmap(Layout.Zoom, Tile.TileX, Tile.TileY);
                if (Bitmap == null)
                    continue;

                AppendTexturedTile(
                    TextureMeshes,
                    ref DepthMin,
                    ref DepthMax,
                    FieldCentreUtm,
                    Camera,
                    Bitmap,
                    Layout.Zoom,
                    Tile.TileX,
                    Tile.TileY,
                    TileMinLat,
                    TileMinLon,
                    TileMaxLat,
                    TileMaxLon,
                    PlaneElevation,
                    PLAN_VIEW_TILE_SUBDIVISIONS);

                if (TextureMeshes.Count >= PLAN_VIEW_MAX_TILE_COUNT)
                    return;
            }
        }

        /// <summary>
        /// Adds black plinth side faces around the satellite ground square.
        /// </summary>
        static private void AppendPlinthSides
            (
            List<GpuVertex> BackgroundMesh,
            ref double DepthMin,
            ref double DepthMax,
            FieldCamera3D Camera
            )
        {
            double HalfSideM = RenderingConstants.METRES_PER_MILE;
            double PlinthThickness = Math.Max(
                SATELLITE_PLINTH_MIN_MODEL_THICKNESS,
                Camera.HorizontalExtentM * SATELLITE_PLINTH_THICKNESS_FRACTION);

            (double East, double North) Sw = ModelOffset(-HalfSideM, -HalfSideM);
            (double East, double North) Se = ModelOffset(HalfSideM, -HalfSideM);
            (double East, double North) Ne = ModelOffset(HalfSideM, HalfSideM);
            (double East, double North) Nw = ModelOffset(-HalfSideM, HalfSideM);

            AppendModelQuad(BackgroundMesh, ref DepthMin, ref DepthMax, Camera,
                Sw.East, Sw.North, 0, Se.East, Se.North, 0, Se.East, Se.North, -PlinthThickness, Sw.East, Sw.North, -PlinthThickness,
                PlinthEdgeColor);
            AppendModelQuad(BackgroundMesh, ref DepthMin, ref DepthMax, Camera,
                Se.East, Se.North, 0, Ne.East, Ne.North, 0, Ne.East, Ne.North, -PlinthThickness, Se.East, Se.North, -PlinthThickness,
                PlinthEdgeColor);
            AppendModelQuad(BackgroundMesh, ref DepthMin, ref DepthMax, Camera,
                Ne.East, Ne.North, 0, Nw.East, Nw.North, 0, Nw.East, Nw.North, -PlinthThickness, Ne.East, Ne.North, -PlinthThickness,
                PlinthEdgeColor);
            AppendModelQuad(BackgroundMesh, ref DepthMin, ref DepthMax, Camera,
                Nw.East, Nw.North, 0, Sw.East, Sw.North, 0, Sw.East, Sw.North, -PlinthThickness, Nw.East, Nw.North, -PlinthThickness,
                PlinthEdgeColor);
        }

        /// <summary>
        /// Adds GPU-textured plinth-top satellite tiles for orbit view.
        /// </summary>
        static private void AppendOrbitSatelliteTiles
            (
            List<GpuTextureMesh> TextureMeshes,
            ref double DepthMin,
            ref double DepthMax,
            FieldSurfaceSampler Surface,
            SatelliteTileService Satellite,
            FieldCamera3D Camera,
            UTM.UTMCoordinate FieldCentreUtm,
            double PlaneElevation
            )
        {
            double FieldCentreLat = (Surface.MinLat + Surface.MaxLat) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
            double FieldCentreLon = (Surface.MinLon + Surface.MaxLon) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
            double HalfSideM = RenderingConstants.METRES_PER_MILE;
            double PlinthDiagonalM = HalfSideM * PLINTH_DIAGONAL_SIDE_MULTIPLIER * Math.Sqrt(PLINTH_DIAGONAL_SIDE_MULTIPLIER);

            (int Zoom, IReadOnlyList<(int TileX, int TileY)> TileIndices) = SatelliteMapLayout3D.ComputePlinthTiles(
                FieldCentreLat,
                FieldCentreLon,
                PlinthDiagonalM,
                MetresPerPixel: PLINTH_TILE_METRES_PER_PIXEL);

            (double MinLat, double MaxLat, double MinLon, double MaxLon) PlinthBounds = SatelliteMapLayout3D.ComputePlinthSquareBounds(
                FieldCentreLat,
                FieldCentreLon,
                PlinthDiagonalM);

            int TilesPerAxis = 1 << Zoom;
            foreach ((int TileX, int TileY) in TileIndices)
            {
                if (TileY < 0 || TileY >= TilesPerAxis)
                    continue;

                WebMercator.TileLatLonBounds(Zoom, TileX, TileY, out double RawMinLon, out double RawMinLat, out double RawMaxLon, out double RawMaxLat);
                double TileMinLat = Math.Min(RawMinLat, RawMaxLat);
                double TileMaxLat = Math.Max(RawMinLat, RawMaxLat);
                double TileMinLon = Math.Min(RawMinLon, RawMaxLon);
                double TileMaxLon = Math.Max(RawMinLon, RawMaxLon);
                if (!TileIntersectsPlinthBounds(TileMinLat, TileMaxLat, TileMinLon, TileMaxLon, PlinthBounds))
                    continue;

                SKBitmap? Bitmap = Satellite.GetTileBitmap(Zoom, TileX, TileY);
                if (Bitmap == null)
                    continue;

                AppendTexturedTile(
                    TextureMeshes,
                    ref DepthMin,
                    ref DepthMax,
                    FieldCentreUtm,
                    Camera,
                    Bitmap,
                    Zoom,
                    TileX,
                    TileY,
                    TileMinLat,
                    TileMinLon,
                    TileMaxLat,
                    TileMaxLon,
                    PlaneElevation);
            }
        }

        /// <summary>
        /// Adds plinth sides and GPU-textured satellite tiles.
        /// </summary>
        static private void AppendSatelliteBackground
            (
            List<GpuVertex> BackgroundMesh,
            List<GpuTextureMesh> TextureMeshes,
            ref double DepthMin,
            ref double DepthMax,
            FieldSurfaceSampler Surface,
            SatelliteTileService Satellite,
            FieldCamera3D Camera,
            UTM.UTMCoordinate FieldCentreUtm,
            double PlaneElevation
            )
        {
            double FieldCentreLat = (Surface.MinLat + Surface.MaxLat) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
            double FieldCentreLon = (Surface.MinLon + Surface.MaxLon) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
            double HalfSideM = RenderingConstants.METRES_PER_MILE;
            double PlinthDiagonalM = HalfSideM * PLINTH_DIAGONAL_SIDE_MULTIPLIER * Math.Sqrt(PLINTH_DIAGONAL_SIDE_MULTIPLIER);
            double PlinthThickness = Math.Max(
                SATELLITE_PLINTH_MIN_MODEL_THICKNESS,
                Camera.HorizontalExtentM * SATELLITE_PLINTH_THICKNESS_FRACTION);

            (double East, double North) Sw = ModelOffset(-HalfSideM, -HalfSideM);
            (double East, double North) Se = ModelOffset(HalfSideM, -HalfSideM);
            (double East, double North) Ne = ModelOffset(HalfSideM, HalfSideM);
            (double East, double North) Nw = ModelOffset(-HalfSideM, HalfSideM);

            AppendModelQuad(BackgroundMesh, ref DepthMin, ref DepthMax, Camera,
                Sw.East, Sw.North, 0, Se.East, Se.North, 0, Se.East, Se.North, -PlinthThickness, Sw.East, Sw.North, -PlinthThickness,
                PlinthEdgeColor);
            AppendModelQuad(BackgroundMesh, ref DepthMin, ref DepthMax, Camera,
                Se.East, Se.North, 0, Ne.East, Ne.North, 0, Ne.East, Ne.North, -PlinthThickness, Se.East, Se.North, -PlinthThickness,
                PlinthEdgeColor);
            AppendModelQuad(BackgroundMesh, ref DepthMin, ref DepthMax, Camera,
                Ne.East, Ne.North, 0, Nw.East, Nw.North, 0, Nw.East, Nw.North, -PlinthThickness, Ne.East, Ne.North, -PlinthThickness,
                PlinthEdgeColor);
            AppendModelQuad(BackgroundMesh, ref DepthMin, ref DepthMax, Camera,
                Nw.East, Nw.North, 0, Sw.East, Sw.North, 0, Sw.East, Sw.North, -PlinthThickness, Nw.East, Nw.North, -PlinthThickness,
                PlinthEdgeColor);

            (int Zoom, IReadOnlyList<(int TileX, int TileY)> TileIndices) = SatelliteMapLayout3D.ComputePlinthTiles(
                FieldCentreLat,
                FieldCentreLon,
                PlinthDiagonalM,
                MetresPerPixel: PLINTH_TILE_METRES_PER_PIXEL);

            (double MinLat, double MaxLat, double MinLon, double MaxLon) PlinthBounds = SatelliteMapLayout3D.ComputePlinthSquareBounds(
                FieldCentreLat,
                FieldCentreLon,
                PlinthDiagonalM);

            int TilesPerAxis = 1 << Zoom;
            foreach ((int TileX, int TileY) in TileIndices)
            {
                if (TileY < 0 || TileY >= TilesPerAxis)
                    continue;

                WebMercator.TileLatLonBounds(Zoom, TileX, TileY, out double RawMinLon, out double RawMinLat, out double RawMaxLon, out double RawMaxLat);
                double TileMinLat = Math.Min(RawMinLat, RawMaxLat);
                double TileMaxLat = Math.Max(RawMinLat, RawMaxLat);
                double TileMinLon = Math.Min(RawMinLon, RawMaxLon);
                double TileMaxLon = Math.Max(RawMinLon, RawMaxLon);
                if (!TileIntersectsPlinthBounds(TileMinLat, TileMaxLat, TileMinLon, TileMaxLon, PlinthBounds))
                    continue;

                SKBitmap? Bitmap = Satellite.GetTileBitmap(Zoom, TileX, TileY);
                if (Bitmap == null)
                    continue;

                AppendTexturedTile(
                    TextureMeshes,
                    ref DepthMin,
                    ref DepthMax,
                    FieldCentreUtm,
                    Camera,
                    Bitmap,
                    Zoom,
                    TileX,
                    TileY,
                    TileMinLat,
                    TileMinLon,
                    TileMaxLat,
                    TileMaxLon,
                    PlaneElevation);
            }
        }

        /// <summary>
        /// Subdivides one satellite tile into textured GPU triangles on the ground plane.
        /// </summary>
        static private void AppendTexturedTile
            (
            List<GpuTextureMesh> TextureMeshes,
            ref double DepthMin,
            ref double DepthMax,
            UTM.UTMCoordinate FieldCentreUtm,
            FieldCamera3D Camera,
            SKBitmap Bitmap,
            int TileZoom,
            int TileX,
            int TileY,
            double TileMinLat,
            double TileMinLon,
            double TileMaxLat,
            double TileMaxLon,
            double Elevation,
            int Subdivisions = RenderingConstants.TILE_SUBDIVISIONS
            )
        {
            List<GpuTexturedVertex> Vertices = new List<GpuTexturedVertex>(Subdivisions * Subdivisions * TRIANGLES_PER_TEXTURE_SUBCELL);
            for (int Row = 0; Row < Subdivisions; Row++)
            {
                double Lat0T = (double)Row / Subdivisions;
                double Lat1T = (double)(Row + 1) / Subdivisions;
                double Lat0 = TileMinLat + Lat0T * (TileMaxLat - TileMinLat);
                double Lat1 = TileMinLat + Lat1T * (TileMaxLat - TileMinLat);

                float V0 = 1.0f - (float)Lat0T;
                float V1 = 1.0f - (float)Lat1T;

                for (int Col = 0; Col < Subdivisions; Col++)
                {
                    double Lon0T = (double)Col / Subdivisions;
                    double Lon1T = (double)(Col + 1) / Subdivisions;
                    double Lon0 = TileMinLon + Lon0T * (TileMaxLon - TileMinLon);
                    double Lon1 = TileMinLon + Lon1T * (TileMaxLon - TileMinLon);

                    float U0 = (float)Lon0T;
                    float U1 = (float)Lon1T;

                    (double East, double North, double Z) Corner00 = ToFieldModelSpace(FieldCentreUtm, Camera, Lat0, Lon0, Elevation, FlatPlane: true);
                    (double East, double North, double Z) Corner10 = ToFieldModelSpace(FieldCentreUtm, Camera, Lat0, Lon1, Elevation, FlatPlane: true);
                    (double East, double North, double Z) Corner11 = ToFieldModelSpace(FieldCentreUtm, Camera, Lat1, Lon1, Elevation, FlatPlane: true);
                    (double East, double North, double Z) Corner01 = ToFieldModelSpace(FieldCentreUtm, Camera, Lat1, Lon0, Elevation, FlatPlane: true);

                    AppendTexturedTriangle(Vertices, ref DepthMin, ref DepthMax, Camera, Corner00, U0, V0, Corner10, U1, V0, Corner11, U1, V1);
                    AppendTexturedTriangle(Vertices, ref DepthMin, ref DepthMax, Camera, Corner00, U0, V0, Corner11, U1, V1, Corner01, U0, V1);
                }
            }

            TextureMeshes.Add(new GpuTextureMesh
            {
                Width = Bitmap.Width,
                Height = Bitmap.Height,
                TileZoom = TileZoom,
                TileX = TileX,
                TileY = TileY,
                Pixels = CopyBitmapPixels(Bitmap),
                Vertices = Vertices.ToArray()
            });
        }

        /// <summary>
        /// Appends one textured triangle with depth range tracking.
        /// </summary>
        static private void AppendTexturedTriangle
            (
            List<GpuTexturedVertex> Vertices,
            ref double DepthMin,
            ref double DepthMax,
            FieldCamera3D Camera,
            (double East, double North, double Z) V0,
            float TexU0,
            float TexV0,
            (double East, double North, double Z) V1,
            float TexU1,
            float TexV1,
            (double East, double North, double Z) V2,
            float TexU2,
            float TexV2
            )
        {
            TrackDepth(Camera, V0.East, V0.North, V0.Z, ref DepthMin, ref DepthMax);
            TrackDepth(Camera, V1.East, V1.North, V1.Z, ref DepthMin, ref DepthMax);
            TrackDepth(Camera, V2.East, V2.North, V2.Z, ref DepthMin, ref DepthMax);
            Vertices.Add(ToGpuTexturedVertex(V0.East, V0.North, V0.Z, TexU0, TexV0));
            Vertices.Add(ToGpuTexturedVertex(V1.East, V1.North, V1.Z, TexU1, TexV1));
            Vertices.Add(ToGpuTexturedVertex(V2.East, V2.North, V2.Z, TexU2, TexV2));
        }

        /// <summary>
        /// Converts model coordinates and UVs to a GPU textured vertex.
        /// </summary>
        static private GpuTexturedVertex ToGpuTexturedVertex
            (
            double EastM,
            double NorthM,
            double ModelZ,
            float U,
            float V
            )
        {
            return new GpuTexturedVertex
            {
                EastM = (float)EastM,
                NorthM = (float)NorthM,
                ModelZ = (float)ModelZ,
                U = U,
                V = V
            };
        }

        /// <summary>
        /// Copies Skia bitmap pixels into a tightly packed RGBA byte array for GPU upload.
        /// </summary>
        /// <param name="Bitmap">Source tile bitmap.</param>
        /// <returns>Row-major RGBA pixel bytes.</returns>
        static private byte[] CopyBitmapPixels
            (
            SKBitmap Bitmap
            )
        {
            byte[] Pixels = new byte[Bitmap.Width * Bitmap.Height * RGBA_BYTES_PER_PIXEL];
            using SKPixmap Pixmap = Bitmap.PeekPixels();
            IntPtr Source = Pixmap.GetPixels();
            if (Source == IntPtr.Zero)
                return Pixels;

            int WidthBytes = Bitmap.Width * RGBA_BYTES_PER_PIXEL;
            int RowBytes = Pixmap.RowBytes;
            if (RowBytes == WidthBytes)
            {
                Marshal.Copy(Source, Pixels, 0, Pixels.Length);
                return Pixels;
            }

            for (int Row = 0; Row < Bitmap.Height; Row++)
                Marshal.Copy(Source + (Row * RowBytes), Pixels, Row * WidthBytes, WidthBytes);

            return Pixels;
        }

        /// <summary>
        /// Appends one coloured triangle to a solid mesh with depth tracking.
        /// </summary>
        static private void AppendColoredTriangle
            (
            List<GpuVertex> Mesh,
            ref double DepthMin,
            ref double DepthMax,
            FieldCamera3D Camera,
            (double East, double North, double Z) V0,
            SKColor C0,
            (double East, double North, double Z) V1,
            SKColor C1,
            (double East, double North, double Z) V2,
            SKColor C2
            )
        {
            TrackDepth(Camera, V0.East, V0.North, V0.Z, ref DepthMin, ref DepthMax);
            TrackDepth(Camera, V1.East, V1.North, V1.Z, ref DepthMin, ref DepthMax);
            TrackDepth(Camera, V2.East, V2.North, V2.Z, ref DepthMin, ref DepthMax);
            Mesh.Add(ToGpuVertex(V0.East, V0.North, V0.Z, C0));
            Mesh.Add(ToGpuVertex(V1.East, V1.North, V1.Z, C1));
            Mesh.Add(ToGpuVertex(V2.East, V2.North, V2.Z, C2));
        }

        /// <summary>
        /// Splits a model-space quad into two coloured triangles for plinth sides.
        /// </summary>
        static private void AppendModelQuad
            (
            List<GpuVertex> Mesh,
            ref double DepthMin,
            ref double DepthMax,
            FieldCamera3D Camera,
            double E0,
            double N0,
            double Z0,
            double E1,
            double N1,
            double Z1,
            double E2,
            double N2,
            double Z2,
            double E3,
            double N3,
            double Z3,
            SKColor Color
            )
        {
            AppendColoredTriangle(
                Mesh,
                ref DepthMin,
                ref DepthMax,
                Camera,
                (E0, N0, Z0),
                Color,
                (E1, N1, Z1),
                Color,
                (E2, N2, Z2),
                Color);
            AppendColoredTriangle(
                Mesh,
                ref DepthMin,
                ref DepthMax,
                Camera,
                (E0, N0, Z0),
                Color,
                (E2, N2, Z2),
                Color,
                (E3, N3, Z3),
                Color);
        }

        /// <summary>
        /// Converts east/north offsets from field centre to field-relative model coordinates.
        /// </summary>
        static private (double East, double North) ModelOffset
            (
            double EastOffset,
            double NorthOffset
            )
        {
            return (EastOffset, NorthOffset);
        }

        /// <summary>
        /// Returns whether a Web Mercator tile overlaps the plinth square bounds.
        /// </summary>
        static private bool TileIntersectsPlinthBounds
            (
            double TileMinLat,
            double TileMaxLat,
            double TileMinLon,
            double TileMaxLon,
            (double MinLat, double MaxLat, double MinLon, double MaxLon) PlinthBounds
            )
        {
            if (TileMaxLat < PlinthBounds.MinLat || TileMinLat > PlinthBounds.MaxLat)
                return false;
            if (TileMaxLon < PlinthBounds.MinLon || TileMinLon > PlinthBounds.MaxLon)
                return false;
            return true;
        }
    }
}
