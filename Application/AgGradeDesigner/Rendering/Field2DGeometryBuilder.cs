using AgGradeDesigner.Core.Color;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Rendering.Vulkan;
using AgGradeDesigner.Core.Survey;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Builds GPU vertex buffers for the 2D plan-view field renderer.
    /// </summary>
    static internal class Field2DGeometryBuilder
    {
        private const int MAX_COLORMAP_SUBDIVISIONS = 32;
        private const int COLORMAP_SUBDIVISION_DIVISOR = 8;
        private const int VERTICES_PER_TEXTURE_TRIANGLE = 6;
        private const int MIN_POLYLINE_POINTS = 2;
        private const int MIN_BOUNDARY_POINTS = 3;
        private const int QUAD_VERTEX_COUNT = 4;
        private const int TRIANGLE_VERTEX_COUNT = 3;
        private const byte PLACEHOLDER_COLOR_R = 32;
        private const byte PLACEHOLDER_COLOR_G = 34;
        private const byte PLACEHOLDER_COLOR_B = 32;
        private const float CLEAR_COLOR_OPAQUE = 1f;
        private const float CLEAR_COLOR_TRANSPARENT = 0f;
        private const double CONTOUR_BAND_OFFSET_FRACTION = 0.001;
        private const double OPACITY_PERCENT_TO_UNIT = 100.0;

        /// <summary>
        /// Builds the satellite basemap layer for 2D plan view.
        /// </summary>
        static public GpuFieldFrame BuildBaseFrame
            (
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SKRect Viewport,
            SatelliteTileService Satellite,
            int SatelliteCacheGeneration
            )
        {
            List<GpuTextureMesh> TextureMeshes = new List<GpuTextureMesh>();
            List<GpuVertex> PlaceholderMeshes = new List<GpuVertex>();
            SatelliteMapViewport Layout = SatelliteMapLayout.Compute(
                CentreLat,
                CentreLon,
                Viewport.Width,
                Viewport.Height,
                MetresPerPixel);

            foreach (SatelliteTileRequest Tile in Layout.Tiles)
            {
                float Left = Tile.ScreenX;
                float Top = Tile.ScreenY;
                float Right = Left + Tile.ScreenSize;
                float Bottom = Top + Tile.ScreenSize;
                byte[]? Data = Satellite.GetTile(Layout.Zoom, Tile.TileX, Tile.TileY);
                if (Data == null)
                {
                    AppendScreenQuad(
                        PlaceholderMeshes,
                        Left,
                        Top,
                        Right,
                        Bottom,
                        PlanViewProjection.DEPTH_BACKGROUND,
                        PLACEHOLDER_COLOR_R,
                        PLACEHOLDER_COLOR_G,
                        PLACEHOLDER_COLOR_B);
                    continue;
                }

                using SKBitmap? Bitmap = SKBitmap.Decode(Data);
                if (Bitmap == null)
                {
                    AppendScreenQuad(
                        PlaceholderMeshes,
                        Left,
                        Top,
                        Right,
                        Bottom,
                        PlanViewProjection.DEPTH_BACKGROUND,
                        PLACEHOLDER_COLOR_R,
                        PLACEHOLDER_COLOR_G,
                        PLACEHOLDER_COLOR_B);
                    continue;
                }

                byte[] Pixels = CopyBitmapPixels(Bitmap);
                GpuTexturedVertex[] Vertices =
                [
                    PlanViewProjection.ScreenTexturedVertex(Left, Bottom, PlanViewProjection.DEPTH_BACKGROUND, 0f, 1f),
                    PlanViewProjection.ScreenTexturedVertex(Right, Bottom, PlanViewProjection.DEPTH_BACKGROUND, 1f, 1f),
                    PlanViewProjection.ScreenTexturedVertex(Right, Top, PlanViewProjection.DEPTH_BACKGROUND, 1f, 0f),
                    PlanViewProjection.ScreenTexturedVertex(Left, Bottom, PlanViewProjection.DEPTH_BACKGROUND, 0f, 1f),
                    PlanViewProjection.ScreenTexturedVertex(Right, Top, PlanViewProjection.DEPTH_BACKGROUND, 1f, 0f),
                    PlanViewProjection.ScreenTexturedVertex(Left, Top, PlanViewProjection.DEPTH_BACKGROUND, 0f, 0f)
                ];

                TextureMeshes.Add(new GpuTextureMesh
                {
                    Width = Bitmap.Width,
                    Height = Bitmap.Height,
                    TileZoom = Layout.Zoom,
                    TileX = Tile.TileX,
                    TileY = Tile.TileY,
                    Pixels = Pixels,
                    Vertices = Vertices
                });
            }

            (GpuTexturedVertex[] Combined, GpuTextureDrawSpec[] Draws) = CombineTextureMeshes(TextureMeshes);
            return CreateFrame(
                Viewport,
                PlaceholderMeshes.ToArray(),
                TextureMeshes.ToArray(),
                Combined,
                Draws,
                Array.Empty<GpuVertex>(),
                Array.Empty<GpuVertex>(),
                Array.Empty<GpuVertex>(),
                SatelliteCacheGeneration,
                CLEAR_COLOR_OPAQUE,
                CLEAR_COLOR_OPAQUE,
                CLEAR_COLOR_OPAQUE,
                CLEAR_COLOR_OPAQUE,
                1f);
        }

        /// <summary>
        /// Builds a solid-colour 2D base layer when satellite imagery is disabled.
        /// </summary>
        static public GpuFieldFrame BuildClearBaseFrame
            (
            SKRect Viewport
            )
        {
            return CreateFrame(
                Viewport,
                Array.Empty<GpuVertex>(),
                Array.Empty<GpuTextureMesh>(),
                Array.Empty<GpuTexturedVertex>(),
                Array.Empty<GpuTextureDrawSpec>(),
                Array.Empty<GpuVertex>(),
                Array.Empty<GpuVertex>(),
                Array.Empty<GpuVertex>(),
                0,
                CLEAR_COLOR_OPAQUE,
                CLEAR_COLOR_OPAQUE,
                CLEAR_COLOR_OPAQUE,
                CLEAR_COLOR_OPAQUE,
                1f);
        }

        /// <summary>
        /// Builds the colormap surface and optional contour overlay for 2D plan view.
        /// </summary>
        static public GpuFieldFrame BuildSurfaceOverlayFrame
            (
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SKRect Viewport,
            IProgress<int>? Progress = null
            )
        {
            Progress?.Report(0);
            List<GpuTextureMesh> TextureMeshes = new List<GpuTextureMesh>();
            List<GpuVertex> LineVertices = new List<GpuVertex>();
            Surface.ComputeScalarRange(ContentView, out double VMin, out double VMax);

            bool ApplyHillshade = Display.Hillshade && ContentView != FieldContentView.CutFill;
            SKBitmap ColorMap = Surface.GetPlanViewColorMap(ContentView, VMin, VMax, ApplyHillshade, Progress);
            AppendColormapMesh(
                TextureMeshes,
                Surface,
                ColorMap,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport);

            if (Display.Contours && VMax > VMin)
            {
                double BandOffset = (VMax - VMin) * CONTOUR_BAND_OFFSET_FRACTION;
                MapViewport MapViewport = new MapViewport { Width = Viewport.Width, Height = Viewport.Height };
                IReadOnlyList<(double Level, IReadOnlyList<(double Lat, double Lon)> Points)> Polylines =
                    Surface.GetContourPolylinesByLevel(ContentView, VMin, VMax, Progress);
                foreach ((double Level, IReadOnlyList<(double Lat, double Lon)> Polyline) in Polylines)
                {
                    if (Polyline.Count < MIN_POLYLINE_POINTS)
                        continue;

                    SKColor Color = FieldRenderer.ArgbToSkia(NineBandColorScale.GetLightenedColor(Level - BandOffset, VMin, VMax));
                    AppendScreenPolyline(
                        LineVertices,
                        Polyline,
                        CentreLat,
                        CentreLon,
                        MetresPerPixel,
                        MapViewport,
                        PlanViewProjection.DEPTH_CONTOUR,
                        Color);
                }
            }

            (GpuTexturedVertex[] Combined, GpuTextureDrawSpec[] Draws) = CombineTextureMeshes(TextureMeshes);
            float OverlayOpacity = (float)Math.Clamp(
                Display.TopoCutFillOpacityPercent / OPACITY_PERCENT_TO_UNIT,
                0.0,
                1.0);
            Progress?.Report(100);
            return CreateFrame(
                Viewport,
                Array.Empty<GpuVertex>(),
                TextureMeshes.ToArray(),
                Combined,
                Draws,
                Array.Empty<GpuVertex>(),
                LineVertices.ToArray(),
                Array.Empty<GpuVertex>(),
                0,
                CLEAR_COLOR_TRANSPARENT,
                CLEAR_COLOR_TRANSPARENT,
                CLEAR_COLOR_TRANSPARENT,
                CLEAR_COLOR_TRANSPARENT,
                OverlayOpacity);
        }

        /// <summary>
        /// Builds field border lines for the 2D decorations layer.
        /// </summary>
        static public GpuFieldFrame BuildDecorationsFrame
            (
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SKRect Viewport
            )
        {
            List<GpuVertex> BorderLines = new List<GpuVertex>();
            if (Project.Survey != null && Surface.SampleWidth > 0)
            {
                MapViewport MapViewport = new MapViewport { Width = Viewport.Width, Height = Viewport.Height };
                if (Project.Survey.BoundaryPoints.Count >= MIN_BOUNDARY_POINTS)
                {
                    List<(double Lat, double Lon)> Ring = FieldRenderer.BuildOrderedBoundaryLatLon(Project.Survey);
                    AppendScreenPolyline(
                        BorderLines,
                        Ring,
                        CentreLat,
                        CentreLon,
                        MetresPerPixel,
                        MapViewport,
                        PlanViewProjection.DEPTH_BORDER,
                        SKColors.Black,
                        CloseLoop: true);
                }
                else
                {
                    AppendValidRegionBorder(BorderLines, Surface, CentreLat, CentreLon, MetresPerPixel, MapViewport);
                }
            }

            return CreateFrame(
                Viewport,
                Array.Empty<GpuVertex>(),
                Array.Empty<GpuTextureMesh>(),
                Array.Empty<GpuTexturedVertex>(),
                Array.Empty<GpuTextureDrawSpec>(),
                Array.Empty<GpuVertex>(),
                Array.Empty<GpuVertex>(),
                BorderLines.ToArray(),
                0,
                CLEAR_COLOR_TRANSPARENT,
                CLEAR_COLOR_TRANSPARENT,
                CLEAR_COLOR_TRANSPARENT,
                CLEAR_COLOR_TRANSPARENT,
                1f);
        }

        static private GpuFieldFrame CreateFrame
            (
            SKRect Viewport,
            GpuVertex[] BackgroundMeshVertices,
            GpuTextureMesh[] TextureMeshes,
            GpuTexturedVertex[] CombinedTextureVertices,
            GpuTextureDrawSpec[] TextureDraws,
            GpuVertex[] MeshVertices,
            GpuVertex[] LineVertices,
            GpuVertex[] BorderLineVertices,
            int SatelliteCacheGeneration,
            float ClearR,
            float ClearG,
            float ClearB,
            float ClearA,
            float SurfaceOverlayOpacity
            )
        {
            return new GpuFieldFrame
            {
                UsePlanViewProjection = true,
                BackgroundMeshVertices = BackgroundMeshVertices,
                TextureMeshes = TextureMeshes,
                CombinedTextureVertices = CombinedTextureVertices,
                TextureDraws = TextureDraws,
                MeshVertices = MeshVertices,
                LineVertices = LineVertices,
                BorderLineVertices = BorderLineVertices,
                Camera = PlanViewProjection.CreateUniforms(Viewport.Width, Viewport.Height),
                SatelliteCacheGeneration = SatelliteCacheGeneration,
                ClearR = ClearR,
                ClearG = ClearG,
                ClearB = ClearB,
                ClearA = ClearA,
                SurfaceOverlayOpacity = SurfaceOverlayOpacity
            };
        }

        static private void AppendColormapMesh
            (
            List<GpuTextureMesh> TextureMeshes,
            FieldSurfaceSampler Surface,
            SKBitmap ColorMap,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SKRect Viewport
            )
        {
            byte[] Pixels = CopyBitmapPixels(ColorMap);
            int Subdivisions = Math.Clamp(
                Math.Max(Surface.SampleWidth, Surface.SampleHeight) / COLORMAP_SUBDIVISION_DIVISOR,
                RenderingConstants.TILE_SUBDIVISIONS,
                MAX_COLORMAP_SUBDIVISIONS);
            int N = Subdivisions;
            float BmpW = ColorMap.Width;
            float BmpH = ColorMap.Height;
            MapViewport MapViewport = new MapViewport { Width = Viewport.Width, Height = Viewport.Height };
            List<GpuTexturedVertex> Vertices = new List<GpuTexturedVertex>(N * N * VERTICES_PER_TEXTURE_TRIANGLE);

            for (int Row = 0; Row < N; Row++)
            {
                double LatTop = Surface.MaxLat - (double)Row / N * (Surface.MaxLat - Surface.MinLat);
                double LatBottom = Surface.MaxLat - (double)(Row + 1) / N * (Surface.MaxLat - Surface.MinLat);
                float TexYTop = (float)Row / N * BmpH;
                float TexYBottom = (float)(Row + 1) / N * BmpH;

                for (int Col = 0; Col < N; Col++)
                {
                    double LonLeft = Surface.MinLon + (double)Col / N * (Surface.MaxLon - Surface.MinLon);
                    double LonRight = Surface.MinLon + (double)(Col + 1) / N * (Surface.MaxLon - Surface.MinLon);
                    float TexXLeft = (float)Col / N * BmpW;
                    float TexXRight = (float)(Col + 1) / N * BmpW;

                    PlanViewProjection.LatLonToScreen(LatBottom, LonLeft, CentreLat, CentreLon, MetresPerPixel, MapViewport, out float BlX, out float BlY);
                    PlanViewProjection.LatLonToScreen(LatBottom, LonRight, CentreLat, CentreLon, MetresPerPixel, MapViewport, out float BrX, out float BrY);
                    PlanViewProjection.LatLonToScreen(LatTop, LonRight, CentreLat, CentreLon, MetresPerPixel, MapViewport, out float TrX, out float TrY);
                    PlanViewProjection.LatLonToScreen(LatTop, LonLeft, CentreLat, CentreLon, MetresPerPixel, MapViewport, out float TlX, out float TlY);

                    Vertices.Add(PlanViewProjection.ScreenTexturedVertex(BlX, BlY, PlanViewProjection.DEPTH_SURFACE, TexXLeft / BmpW, TexYBottom / BmpH));
                    Vertices.Add(PlanViewProjection.ScreenTexturedVertex(BrX, BrY, PlanViewProjection.DEPTH_SURFACE, TexXRight / BmpW, TexYBottom / BmpH));
                    Vertices.Add(PlanViewProjection.ScreenTexturedVertex(TrX, TrY, PlanViewProjection.DEPTH_SURFACE, TexXRight / BmpW, TexYTop / BmpH));
                    Vertices.Add(PlanViewProjection.ScreenTexturedVertex(BlX, BlY, PlanViewProjection.DEPTH_SURFACE, TexXLeft / BmpW, TexYBottom / BmpH));
                    Vertices.Add(PlanViewProjection.ScreenTexturedVertex(TrX, TrY, PlanViewProjection.DEPTH_SURFACE, TexXRight / BmpW, TexYTop / BmpH));
                    Vertices.Add(PlanViewProjection.ScreenTexturedVertex(TlX, TlY, PlanViewProjection.DEPTH_SURFACE, TexXLeft / BmpW, TexYTop / BmpH));
                }
            }

            TextureMeshes.Add(new GpuTextureMesh
            {
                Width = ColorMap.Width,
                Height = ColorMap.Height,
                TileZoom = -1,
                TileX = 0,
                TileY = 0,
                Pixels = Pixels,
                Vertices = Vertices.ToArray()
            });
        }

        static private void AppendValidRegionBorder
            (
            List<GpuVertex> BorderLines,
            FieldSurfaceSampler Surface,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport Viewport
            )
        {
            int Width = Surface.SampleWidth;
            int Height = Surface.SampleHeight;
            for (int Row = 0; Row < Height; Row++)
            {
                for (int Col = 0; Col < Width; Col++)
                {
                    int Idx = Row * Width + Col;
                    if (!Surface.Valid[Idx])
                        continue;

                    double Lat = Surface.MaxLat - (double)Row / Math.Max(Height - 1, 1) * (Surface.MaxLat - Surface.MinLat);
                    double Lon = Surface.MinLon + (double)Col / Math.Max(Width - 1, 1) * (Surface.MaxLon - Surface.MinLon);
                    if (Col + 1 < Width)
                    {
                        int Neighbor = Row * Width + Col + 1;
                        if (!Surface.Valid[Neighbor])
                        {
                            double LonR = Surface.MinLon + (double)(Col + 1) / Math.Max(Width - 1, 1) * (Surface.MaxLon - Surface.MinLon);
                            AppendScreenLine(BorderLines, Lat, Lon, Lat, LonR, CentreLat, CentreLon, MetresPerPixel, Viewport, SKColors.Black);
                        }
                    }

                    if (Row + 1 < Height)
                    {
                        int Neighbor = (Row + 1) * Width + Col;
                        if (!Surface.Valid[Neighbor])
                        {
                            double LatB = Surface.MaxLat - (double)(Row + 1) / Math.Max(Height - 1, 1) * (Surface.MaxLat - Surface.MinLat);
                            AppendScreenLine(BorderLines, Lat, Lon, LatB, Lon, CentreLat, CentreLon, MetresPerPixel, Viewport, SKColors.Black);
                        }
                    }
                }
            }
        }

        static private void AppendScreenPolyline
            (
            List<GpuVertex> Vertices,
            IReadOnlyList<(double Lat, double Lon)> Points,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport Viewport,
            float Depth,
            SKColor Color,
            bool CloseLoop = false
            )
        {
            int Count = Points.Count;
            if (Count < MIN_POLYLINE_POINTS)
                return;

            int SegmentCount = CloseLoop ? Count : Count - 1;
            for (int I = 0; I < SegmentCount; I++)
            {
                (double Lat0, double Lon0) = Points[I];
                (double Lat1, double Lon1) = Points[(I + 1) % Count];
                AppendScreenLine(Vertices, Lat0, Lon0, Lat1, Lon1, CentreLat, CentreLon, MetresPerPixel, Viewport, Color, Depth);
            }
        }

        static private void AppendScreenLine
            (
            List<GpuVertex> Vertices,
            double Lat0,
            double Lon0,
            double Lat1,
            double Lon1,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport Viewport,
            SKColor Color,
            float Depth = PlanViewProjection.DEPTH_BORDER
            )
        {
            PlanViewProjection.LatLonToScreen(Lat0, Lon0, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X0, out float Y0);
            PlanViewProjection.LatLonToScreen(Lat1, Lon1, CentreLat, CentreLon, MetresPerPixel, Viewport, out float X1, out float Y1);
            Vertices.Add(PlanViewProjection.ScreenColoredVertex(X0, Y0, Depth, Color.Red, Color.Green, Color.Blue, Color.Alpha));
            Vertices.Add(PlanViewProjection.ScreenColoredVertex(X1, Y1, Depth, Color.Red, Color.Green, Color.Blue, Color.Alpha));
        }

        static private void AppendScreenQuad
            (
            List<GpuVertex> Vertices,
            float Left,
            float Top,
            float Right,
            float Bottom,
            float Depth,
            byte Red,
            byte Green,
            byte Blue
            )
        {
            Vertices.Add(PlanViewProjection.ScreenColoredVertex(Left, Bottom, Depth, Red, Green, Blue));
            Vertices.Add(PlanViewProjection.ScreenColoredVertex(Right, Bottom, Depth, Red, Green, Blue));
            Vertices.Add(PlanViewProjection.ScreenColoredVertex(Right, Top, Depth, Red, Green, Blue));
            Vertices.Add(PlanViewProjection.ScreenColoredVertex(Left, Bottom, Depth, Red, Green, Blue));
            Vertices.Add(PlanViewProjection.ScreenColoredVertex(Right, Top, Depth, Red, Green, Blue));
            Vertices.Add(PlanViewProjection.ScreenColoredVertex(Left, Top, Depth, Red, Green, Blue));
        }

        static private (GpuTexturedVertex[] Combined, GpuTextureDrawSpec[] Draws) CombineTextureMeshes
            (
            List<GpuTextureMesh> TextureMeshes
            )
        {
            if (TextureMeshes.Count == 0)
                return (Array.Empty<GpuTexturedVertex>(), Array.Empty<GpuTextureDrawSpec>());

            List<GpuTexturedVertex> Combined = new List<GpuTexturedVertex>();
            List<GpuTextureDrawSpec> Specs = new List<GpuTextureDrawSpec>(TextureMeshes.Count);
            for (int MeshIndex = 0; MeshIndex < TextureMeshes.Count; MeshIndex++)
            {
                GpuTextureMesh Mesh = TextureMeshes[MeshIndex];
                if (Mesh.Vertices.Length == 0)
                    continue;

                Specs.Add(new GpuTextureDrawSpec
                {
                    MeshIndex = MeshIndex,
                    FirstVertex = (uint)Combined.Count,
                    VertexCount = (uint)Mesh.Vertices.Length
                });
                Combined.AddRange(Mesh.Vertices);
            }

            return (Combined.ToArray(), Specs.ToArray());
        }

        static private byte[] CopyBitmapPixelsToBgra
            (
            SKBitmap Bitmap
            )
        {
            if (Bitmap.Width <= 0 || Bitmap.Height <= 0)
                return Array.Empty<byte>();

            SKImageInfo BgraInfo = new SKImageInfo(
                Bitmap.Width,
                Bitmap.Height,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using SKBitmap BgraBitmap = new SKBitmap(BgraInfo);
            if (!Bitmap.CopyTo(BgraBitmap))
                return Array.Empty<byte>();

            nint SourcePtr = BgraBitmap.GetPixels();
            if (SourcePtr == nint.Zero)
                return Array.Empty<byte>();

            int TightRowBytes = Bitmap.Width * 4;
            int ByteCount = TightRowBytes * Bitmap.Height;
            byte[] Pixels = new byte[ByteCount];
            int RowBytes = BgraBitmap.RowBytes;
            if (RowBytes == TightRowBytes)
            {
                System.Runtime.InteropServices.Marshal.Copy(SourcePtr, Pixels, 0, ByteCount);
                return Pixels;
            }

            for (int Row = 0; Row < Bitmap.Height; Row++)
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    SourcePtr + (Row * RowBytes),
                    Pixels,
                    Row * TightRowBytes,
                    TightRowBytes);
            }

            return Pixels;
        }

        static private byte[] CopyBitmapPixels
            (
            SKBitmap Bitmap
            )
        {
            return CopyBitmapPixelsToBgra(Bitmap);
        }

        /// <summary>
        /// Returns true when a BGRA readback buffer contains any non-background pixel.
        /// </summary>
        static internal bool BgraPixelsLookBlank
            (
            byte[]? Pixels
            )
        {
            if (Pixels == null || Pixels.Length < 4)
                return true;

            for (int I = 0; I < Pixels.Length; I += 4)
            {
                byte B = Pixels[I];
                byte G = Pixels[I + 1];
                byte R = Pixels[I + 2];
                byte A = Pixels[I + 3];
                if (A > 8 && (R > 8 || G > 8 || B > 8))
                    return false;
            }

            return true;
        }
    }
}
