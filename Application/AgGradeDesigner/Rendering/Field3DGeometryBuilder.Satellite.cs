using System.Runtime.InteropServices;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Rendering.Vulkan;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    partial class Field3DGeometryBuilder
    {
        private const double SATELLITE_PLINTH_HALF_SPAN_M = 1609.344;
        private const double SATELLITE_PLINTH_THICKNESS_FRACTION = 0.012;
        private const double SATELLITE_PLINTH_MIN_MODEL_THICKNESS = 1.5;
        private const int TILE_SUBDIVISIONS = 4;

        private static readonly SKColor PlinthEdgeColor = SKColors.Black;

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
            double PlaneElevation
            )
        {
            double FieldCentreLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double FieldCentreLon = (Surface.MinLon + Surface.MaxLon) * 0.5;
            double HalfSideM = SATELLITE_PLINTH_HALF_SPAN_M;
            double PlinthDiagonalM = HalfSideM * 2.0 * Math.Sqrt(2.0);
            UTM.UTMCoordinate FieldCentreUtm = UTM.FromLatLon(FieldCentreLat, FieldCentreLon);
            double PlinthThickness = Math.Max(
                SATELLITE_PLINTH_MIN_MODEL_THICKNESS,
                Camera.HorizontalExtentM * SATELLITE_PLINTH_THICKNESS_FRACTION);

            (double East, double North) Sw = ModelOffset(FieldCentreUtm, Camera, -HalfSideM, -HalfSideM);
            (double East, double North) Se = ModelOffset(FieldCentreUtm, Camera, HalfSideM, -HalfSideM);
            (double East, double North) Ne = ModelOffset(FieldCentreUtm, Camera, HalfSideM, HalfSideM);
            (double East, double North) Nw = ModelOffset(FieldCentreUtm, Camera, -HalfSideM, HalfSideM);

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
                MetresPerPixel: 0.0);

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

        static private void AppendTexturedTile
            (
            List<GpuTextureMesh> TextureMeshes,
            ref double DepthMin,
            ref double DepthMax,
            FieldCamera3D Camera,
            SKBitmap Bitmap,
            int TileZoom,
            int TileX,
            int TileY,
            double TileMinLat,
            double TileMinLon,
            double TileMaxLat,
            double TileMaxLon,
            double Elevation
            )
        {
            int Subdivisions = TILE_SUBDIVISIONS;
            List<GpuTexturedVertex> Vertices = new List<GpuTexturedVertex>(Subdivisions * Subdivisions * 6);
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

                    (double East, double North, double Z) Corner00 = Camera.ToModelSpace(Lat0, Lon0, Elevation, FlatPlane: true);
                    (double East, double North, double Z) Corner10 = Camera.ToModelSpace(Lat0, Lon1, Elevation, FlatPlane: true);
                    (double East, double North, double Z) Corner11 = Camera.ToModelSpace(Lat1, Lon1, Elevation, FlatPlane: true);
                    (double East, double North, double Z) Corner01 = Camera.ToModelSpace(Lat1, Lon0, Elevation, FlatPlane: true);

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

        static private byte[] CopyBitmapPixels
            (
            SKBitmap Bitmap
            )
        {
            byte[] Pixels = new byte[Bitmap.Width * Bitmap.Height * 4];
            using SKPixmap Pixmap = Bitmap.PeekPixels();
            IntPtr Source = Pixmap.GetPixels();
            if (Source == IntPtr.Zero)
                return Pixels;

            int WidthBytes = Bitmap.Width * 4;
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

        static private (double East, double North) ModelOffset
            (
            UTM.UTMCoordinate FieldCentreUtm,
            FieldCamera3D Camera,
            double EastOffset,
            double NorthOffset
            )
        {
            return (
                (FieldCentreUtm.Easting + EastOffset) - Camera.CentreUtm.Easting,
                (FieldCentreUtm.Northing + NorthOffset) - Camera.CentreUtm.Northing);
        }

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
