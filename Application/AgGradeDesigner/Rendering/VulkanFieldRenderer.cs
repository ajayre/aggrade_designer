using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Rendering.Vulkan;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// GPU path for 2D and 3D field rendering via Vulkan offscreen targets.
    /// </summary>
    static internal class VulkanFieldRenderer
    {
        /// <summary>
        /// Renders the 2D satellite basemap layer via Vulkan.
        /// </summary>
        static public bool TryRenderBase2D
            (
            SKRect Viewport,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SatelliteTileService Satellite,
            out SKBitmap? LayerBitmap,
            out int PlaceholdersDrawn,
            out string? FailureReason
            )
        {
            LayerBitmap = null;
            PlaceholdersDrawn = 0;
            FailureReason = null;

            SatelliteMapViewport Layout = SatelliteMapLayout.Compute(
                CentreLat,
                CentreLon,
                Viewport.Width,
                Viewport.Height,
                MetresPerPixel);
            foreach (SatelliteTileRequest Tile in Layout.Tiles)
            {
                if (Satellite.GetTile(Layout.Zoom, Tile.TileX, Tile.TileY) == null)
                    PlaceholdersDrawn++;
            }

            GpuFieldFrame Frame = Field2DGeometryBuilder.BuildBaseFrame(
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport,
                Satellite,
                Satellite.CacheGeneration);
            return TryRenderToBitmap(Frame, Viewport, out LayerBitmap, out FailureReason);
        }

        /// <summary>
        /// Renders the 2D colormap surface and optional contours via Vulkan.
        /// </summary>
        static public bool TryRenderSurfaceOverlay2D
            (
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            IProgress<int>? Progress,
            out SKBitmap? LayerBitmap,
            out string? FailureReason
            )
        {
            LayerBitmap = null;
            FailureReason = null;

            GpuFieldFrame Frame = Field2DGeometryBuilder.BuildSurfaceOverlayFrame(
                Surface,
                ContentView,
                Display,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport,
                Project,
                Progress);
            return TryRenderToBitmap(Frame, Viewport, out LayerBitmap, out FailureReason);
        }

        /// <summary>
        /// Renders all 2D layer targets in one batched Vulkan session.
        /// </summary>
        static public bool TryRenderAll2DLayers
            (
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            SatelliteTileService? Satellite,
            IProgress<int>? Progress,
            out byte[]? BasePixels,
            out byte[]? SurfacePixels,
            out byte[]? DecorationsPixels,
            out int SatellitePlaceholdersDrawn,
            out bool SatelliteTilesComplete,
            out string? FailureReason
            )
        {
            BasePixels = null;
            SurfacePixels = null;
            DecorationsPixels = null;
            SatellitePlaceholdersDrawn = 0;
            SatelliteTilesComplete = true;
            FailureReason = null;

            List<GpuFieldFrame> Frames = new List<GpuFieldFrame>(3);
            if (Display.Satellite && Satellite != null)
            {
                SatelliteMapViewport Layout = SatelliteMapLayout.Compute(
                    CentreLat,
                    CentreLon,
                    Viewport.Width,
                    Viewport.Height,
                    MetresPerPixel);
                foreach (SatelliteTileRequest Tile in Layout.Tiles)
                {
                    if (Satellite.GetTile(Layout.Zoom, Tile.TileX, Tile.TileY) == null)
                        SatellitePlaceholdersDrawn++;
                }

                Frames.Add(Field2DGeometryBuilder.BuildBaseFrame(
                    CentreLat,
                    CentreLon,
                    MetresPerPixel,
                    Viewport,
                    Satellite,
                    Satellite.CacheGeneration));
            }
            else
            {
                Frames.Add(Field2DGeometryBuilder.BuildClearBaseFrame(Viewport));
            }

            Frames.Add(Field2DGeometryBuilder.BuildSurfaceOverlayFrame(
                Surface,
                ContentView,
                Display,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport,
                Project,
                Progress));
            Frames.Add(Field2DGeometryBuilder.BuildDecorationsFrame(
                Project,
                Surface,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport));

            if (!GraphicsRuntime.TryRenderGpuFieldFrameBatch(
                    Frames,
                    (int)Viewport.Width,
                    (int)Viewport.Height,
                    out byte[]?[]? BatchPixels,
                    out FailureReason) ||
                BatchPixels == null ||
                BatchPixels.Length != Frames.Count)
            {
                return false;
            }

            int BaseIndex = Display.Satellite && Satellite != null ? 0 : 0;
            BasePixels = BatchPixels[BaseIndex];
            SurfacePixels = BatchPixels[1];
            DecorationsPixels = BatchPixels[2];
            SatelliteTilesComplete = !Display.Satellite || SatellitePlaceholdersDrawn == 0;
            return BasePixels != null && SurfacePixels != null && DecorationsPixels != null;
        }

        /// <summary>
        /// Renders 2D field border lines via Vulkan.
        /// </summary>
        static public bool TryRenderDecorations2D
            (
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            out SKBitmap? LayerBitmap,
            out string? FailureReason
            )
        {
            LayerBitmap = null;
            FailureReason = null;

            GpuFieldFrame Frame = Field2DGeometryBuilder.BuildDecorationsFrame(
                Project,
                Surface,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                Viewport);
            return TryRenderToBitmap(Frame, Viewport, out LayerBitmap, out FailureReason);
        }

        /// <summary>
        /// Renders the full 3D scene (satellite plinth, mesh, contours, border) via Vulkan.
        /// </summary>
        /// <param name="haulRouteScrubIndex">Pinned haul route scrub index for haul 3D, or -1 when not applicable.</param>
        static public bool TryRenderScene
            (
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            SatelliteTileService? Satellite,
            bool IncludeColoredMesh,
            bool IncludeLineDecorations,
            bool UsePlanViewSatellite,
            IProgress<int>? Progress,
            out SKBitmap? SceneBitmap,
            out string? FailureReason,
            int haulRouteScrubIndex = -1
            )
        {
            SceneBitmap = null;
            FailureReason = null;

            if (!GraphicsRuntime.TryRenderGpuFieldFrame(
                    Field3DGeometryBuilder.BuildFrame(
                        Surface,
                        Project,
                        ContentView,
                        Display,
                        Camera,
                        Viewport,
                        Satellite,
                        IncludeColoredMesh,
                        IncludeLineDecorations,
                        UsePlanViewSatellite,
                        Progress,
                        haulRouteScrubIndex),
                    (int)Viewport.Width,
                    (int)Viewport.Height,
                    out byte[]? Pixels,
                    out FailureReason))
            {
                return false;
            }

            SceneBitmap = CreateBitmapFromPixels(Pixels!, (int)Viewport.Width, (int)Viewport.Height);
            return true;
        }

        /// <summary>
        /// Renders only the coloured mesh and contour lines on a transparent background.
        /// </summary>
        /// <param name="haulRouteScrubIndex">Pinned haul route scrub index for haul 3D, or -1 when not applicable.</param>
        static public bool TryRenderOverlay
            (
            SKRect Viewport,
            ProjectDocument Project,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            DisplaySettings Display,
            FieldCamera3D Camera,
            IProgress<int>? Progress,
            out SKBitmap? SceneBitmap,
            out string? FailureReason,
            int haulRouteScrubIndex = -1
            )
        {
            SceneBitmap = null;
            FailureReason = null;

            if (!GraphicsRuntime.TryRenderGpuFieldFrame(
                    Field3DGeometryBuilder.BuildOverlayFrame(
                        Surface,
                        Project,
                        ContentView,
                        Display,
                        Camera,
                        Viewport,
                        Progress,
                        haulRouteScrubIndex),
                    (int)Viewport.Width,
                    (int)Viewport.Height,
                    out byte[]? Pixels,
                    out FailureReason))
            {
                return false;
            }

            SceneBitmap = CreateBitmapFromPixels(Pixels!, (int)Viewport.Width, (int)Viewport.Height);
            return true;
        }

        /// <summary>
        /// Renders one GPU field frame and wraps the readback pixels in an SKBitmap.
        /// </summary>
        static private bool TryRenderToBitmap
            (
            GpuFieldFrame Frame,
            SKRect Viewport,
            out SKBitmap? Bitmap,
            out string? FailureReason
            )
        {
            Bitmap = null;
            FailureReason = null;
            if (!GraphicsRuntime.TryRenderGpuFieldFrame(
                    Frame,
                    (int)Viewport.Width,
                    (int)Viewport.Height,
                    out byte[]? Pixels,
                    out FailureReason))
            {
                return false;
            }

            Bitmap = CreateBitmapFromPixels(Pixels!, (int)Viewport.Width, (int)Viewport.Height);
            return true;
        }

        /// <summary>
        /// Copies BGRA8888 readback bytes into a premultiplied SKBitmap.
        /// </summary>
        static private SKBitmap CreateBitmapFromPixels
            (
            byte[] Pixels,
            int Width,
            int Height
            )
        {
            int SafeWidth = Math.Max(1, Width);
            int SafeHeight = Math.Max(1, Height);
            SKBitmap Bitmap = new SKBitmap(SafeWidth, SafeHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKPixmap Pixmap = Bitmap.PeekPixels();
            System.Runtime.InteropServices.Marshal.Copy(Pixels, 0, Pixmap.GetPixels(), Pixels.Length);
            return Bitmap;
        }
    }
}
