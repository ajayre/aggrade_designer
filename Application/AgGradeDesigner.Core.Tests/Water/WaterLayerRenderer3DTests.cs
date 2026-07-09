using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Water;
using AgGradeDesigner.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Water
{
    /// <summary>
    /// Regression tests for 3D water layer draping and camera-view clipping (WATER-SPEC §9.0.23–§9.0.24).
    /// </summary>
    public sealed class WaterLayerRenderer3DTests
    {
        /// <summary>Viewport width used when rasterizing 3D water overlays in tests.</summary>
        private const int VIEWPORT_WIDTH = 1200;

        /// <summary>Viewport height used when rasterizing 3D water overlays in tests.</summary>
        private const int VIEWPORT_HEIGHT = 900;

        /// <summary>Orbit heading used for 3D projection tests.</summary>
        private const double ORBIT_HEADING_DEG = 45.0;

        /// <summary>Orbit pitch used for 3D projection tests.</summary>
        private const double ORBIT_PITCH_DEG = 45.0;

        /// <summary>Orbit distance used for 3D projection tests.</summary>
        private const double ORBIT_DISTANCE_M = 500.0;

        /// <summary>
        /// Verifies a full-screen depth occluder suppresses previously visible water overlay pixels.
        /// </summary>
        [Fact]
        public async Task Draw3D_FullScreenOccluder_SuppressesWaterOverlayPixels
            (
            )
        {
            ProjectDocument Project = ImportHandoffProjectFactory.CreateDesignAgdProject();
            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);
            Assert.True(FieldBounds.TryFromProject(Project, out FieldBounds Bounds));
            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);

            await WaterLayerCalculator.ComputeAsync(
                Project,
                FieldContentView.Existing,
                RouteScrubIndex: 0).ConfigureAwait(true);
            WaterLayerCacheEntry Entry = Project.Water.ExistingSurfaceCache
                ?? throw new InvalidOperationException("Expected computed existing-surface water cache.");

            FieldCamera3D Camera = new FieldCamera3D(
                Surface,
                ORBIT_HEADING_DEG,
                ORBIT_PITCH_DEG,
                ORBIT_DISTANCE_M,
                DisplaySettings.DEFAULT_VERTICAL_EXAGGERATION,
                Fit.CentreLat,
                Fit.CentreLon);

            using SKBitmap UnoccludedBitmap = RenderWaterLayer3D(
                Entry,
                Surface,
                FieldContentView.Existing,
                Camera,
                MeshDepthBuffer.AlwaysVisible);
            using SKBitmap OccludedBitmap = RenderWaterLayer3D(
                Entry,
                Surface,
                FieldContentView.Existing,
                Camera,
                BuildFullScreenOccluder());

            int UnoccludedPixels = CountWaterOverlayPixels(UnoccludedBitmap);
            int OccludedPixels = CountWaterOverlayPixels(OccludedBitmap);
            Assert.True(UnoccludedPixels > 0, "Expected visible water overlay pixels without occlusion.");
            Assert.True(OccludedPixels < UnoccludedPixels, "Expected fewer water pixels behind a full-screen occluder.");
        }

        /// <summary>
        /// Renders the water layer to a bitmap for pixel-level assertions.
        /// </summary>
        /// <param name="Entry">Cache entry to draw.</param>
        /// <param name="Surface">Active basis surface sampler.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="Camera">Orbit camera.</param>
        /// <param name="TopoDepth">Topo depth buffer for occlusion testing.</param>
        /// <returns>Rasterized frame bitmap.</returns>
        private static SKBitmap RenderWaterLayer3D
            (
            WaterLayerCacheEntry Entry,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            MeshDepthBuffer TopoDepth
            )
        {
            SKBitmap Bitmap = new SKBitmap(VIEWPORT_WIDTH, VIEWPORT_HEIGHT, SKColorType.Rgba8888, SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(Bitmap);
            Canvas.Clear(SKColors.White);
            WaterLayerRenderer.Draw3D(
                Canvas,
                new SKRect(0, 0, VIEWPORT_WIDTH, VIEWPORT_HEIGHT),
                Entry,
                Surface,
                ContentView,
                Camera,
                TopoDepth);
            return Bitmap;
        }

        /// <summary>
        /// Builds a depth buffer that occludes the entire viewport at a depth closer than typical overlay samples.
        /// </summary>
        /// <returns>Depth buffer that occludes the entire viewport.</returns>
        private static MeshDepthBuffer BuildFullScreenOccluder
            (
            )
        {
            const double OccluderDepth = 1_000_000.0;
            SKRect Viewport = SKRect.Create(0, 0, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);
            MeshTriangle Upper = new MeshTriangle
            {
                P0 = new SKPoint(0, 0),
                P1 = new SKPoint(VIEWPORT_WIDTH, 0),
                P2 = new SKPoint(0, VIEWPORT_HEIGHT),
                C0 = SKColors.White,
                C1 = SKColors.White,
                C2 = SKColors.White,
                D0 = OccluderDepth,
                D1 = OccluderDepth,
                D2 = OccluderDepth,
                Depth = OccluderDepth,
            };
            MeshTriangle Lower = new MeshTriangle
            {
                P0 = new SKPoint(VIEWPORT_WIDTH, 0),
                P1 = new SKPoint(VIEWPORT_WIDTH, VIEWPORT_HEIGHT),
                P2 = new SKPoint(0, VIEWPORT_HEIGHT),
                C0 = SKColors.White,
                C1 = SKColors.White,
                C2 = SKColors.White,
                D0 = OccluderDepth,
                D1 = OccluderDepth,
                D2 = OccluderDepth,
                Depth = OccluderDepth,
            };

            return MeshDepthBuffer.Build(Viewport, new[] { Upper, Lower });
        }

        /// <summary>
        /// Counts opaque blue-tinted pixels produced by the water overlay renderer.
        /// </summary>
        /// <param name="Bitmap">Rasterized frame.</param>
        /// <returns>Pixel count.</returns>
        private static int CountWaterOverlayPixels
            (
            SKBitmap Bitmap
            )
        {
            const byte MinWaterAlpha = 40;
            int Count = 0;
            for (int Y = 0; Y < Bitmap.Height; Y++)
            {
                for (int X = 0; X < Bitmap.Width; X++)
                {
                    SKColor Color = Bitmap.GetPixel(X, Y);
                    if (Color.Alpha < MinWaterAlpha)
                        continue;

                    // Match water overlay tint (see WaterConstants) and ignore the white canvas background.
                    if ((Color.Red <= WaterConstants.WATER_OVERLAY_RED + 20) &&
                        (Color.Green >= WaterConstants.WATER_OVERLAY_GREEN - 20) &&
                        (Color.Blue >= WaterConstants.WATER_OVERLAY_BLUE - 20))
                    {
                        Count++;
                    }
                }
            }

            return Count;
        }
    }
}
