using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Headless 2D plan-view frames via <see cref="FieldRenderer.Draw3D"/> with <c>UsePlanViewSatellite</c>.
    /// </summary>
    static internal class FieldRenderer2DBenchmark
    {
        /// <summary>
        /// Viewport width used by this component.
        /// </summary>
        internal const int ViewportWidth = 1280;

        /// <summary>
        /// Viewport height used by this component.
        /// </summary>
        internal const int ViewportHeight = 720;

        /// <summary>
        /// North-up plan-view heading matching production plan-view camera lock.
        /// </summary>
        internal const double PLAN_VIEW_HEADING_DEG = 180.0;

        /// <summary>
        /// Near-nadir plan-view pitch matching production plan-view camera lock.
        /// </summary>
        internal const double PLAN_VIEW_PITCH_DEG = 89.0;

        /// <summary>
        /// Minimum orbit distance used when deriving plan-view camera height.
        /// </summary>
        internal const double MIN_ORBIT_DISTANCE_M = 50.0;

        /// <summary>
        /// Zoom factor multiplier for zoomed-in regression scenarios.
        /// </summary>
        internal const double ZOOM_IN_MULTIPLIER = 2.0;

        /// <summary>
        /// Zoom factor multiplier for zoomed-out regression scenarios.
        /// </summary>
        internal const double ZOOM_OUT_MULTIPLIER = 0.5;

        /// <summary>
        /// Opacity percent divisor converting percent to 0–1 alpha.
        /// </summary>
        internal const double OPACITY_PERCENT_TO_UNIT = 100.0;

        /// <summary>
        /// Minimum topo overlay opacity in unit range.
        /// </summary>
        internal const double MIN_OPACITY_UNIT = 0.0;

        /// <summary>
        /// Renders one monolithic 2D plan-view frame and returns pixel bytes.
        /// </summary>
        /// <param name="Surface">Built field surface.</param>
        /// <param name="Project">Project document.</param>
        /// <param name="Scenario">Pan/zoom and display parameters.</param>
        /// <returns>BGRA8888 pixel buffer for the composited frame.</returns>
        static public byte[] Render2DMonolithicBytes
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldRenderer2DScenario Scenario
            )
        {
            using SKBitmap FrameBitmap = Render2DMonolithicBitmap(Surface, Project, Scenario);
            return RenderFrameAnalyzer.CopyBitmapToBgraBytes(FrameBitmap);
        }

        /// <summary>
        /// Renders one monolithic 2D plan-view frame and returns an owned bitmap copy.
        /// </summary>
        /// <param name="Surface">Built field surface.</param>
        /// <param name="Project">Project document.</param>
        /// <param name="Scenario">Pan/zoom and display parameters.</param>
        /// <returns>Composited monolithic frame bitmap.</returns>
        static public SKBitmap Render2DMonolithicBitmap
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldRenderer2DScenario Scenario
            )
        {
            GraphicsRuntime.Initialize();
            using SKBitmap FrameBitmap = new SKBitmap(
                ViewportWidth,
                ViewportHeight,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(FrameBitmap);
            SKRect Viewport = SKRect.Create(0, 0, ViewportWidth, ViewportHeight);

            ResolvePlanViewOrbit(Scenario, out double HeadingDeg, out double PitchDeg, out double DistanceM, out double OrbitLat, out double OrbitLon);

            FieldRenderer.Draw3D(
                Canvas,
                Viewport,
                Project,
                Surface,
                FieldContentView.Existing,
                Scenario.Display,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                Scenario.Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                Scenario.Display.Satellite ? Scenario.Satellite : null,
                FieldRenderPass.Monolithic,
                UsePlanViewSatellite: true);

            Canvas.Flush();
            SKBitmap Copy = new SKBitmap(ViewportWidth, ViewportHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            FrameBitmap.CopyTo(Copy);
            return Copy;
        }

        /// <summary>
        /// Renders base, topo, and decoration layers separately then composites with topo opacity (layered 2D path).
        /// </summary>
        /// <param name="Surface">Built field surface.</param>
        /// <param name="Project">Project document.</param>
        /// <param name="Scenario">Pan/zoom and display parameters.</param>
        /// <returns>BGRA8888 pixel buffer matching viewer layered compositing.</returns>
        static public byte[] Render2DLayeredCompositeBytes
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldRenderer2DScenario Scenario
            )
        {
            GraphicsRuntime.Initialize();
            using SKBitmap BaseBitmap = RenderSinglePass(Surface, Project, Scenario, FieldRenderPass.Base);
            using SKBitmap TopoBitmap = RenderSinglePass(Surface, Project, Scenario, FieldRenderPass.SurfaceOverlay);
            using SKBitmap DecorationsBitmap = RenderSinglePass(Surface, Project, Scenario, FieldRenderPass.Decorations);

            using SKBitmap Composite = new SKBitmap(ViewportWidth, ViewportHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(Composite);
            Canvas.Clear(SKColors.Black);

            using (SKPaint BasePaint = new SKPaint { IsAntialias = true })
                Canvas.DrawBitmap(BaseBitmap, 0, 0, BasePaint);

            double Opacity = Math.Clamp(
                Scenario.Display.TopoCutFillOpacityPercent,
                MIN_OPACITY_UNIT,
                OPACITY_PERCENT_TO_UNIT) / OPACITY_PERCENT_TO_UNIT;
            if (Opacity > MIN_OPACITY_UNIT)
            {
                using SKPaint TopoPaint = new SKPaint
                {
                    IsAntialias = true,
                    Color = SKColors.White.WithAlpha((byte)(Opacity * byte.MaxValue))
                };
                Canvas.DrawBitmap(TopoBitmap, 0, 0, TopoPaint);
            }

            using (SKPaint DecorationsPaint = new SKPaint { IsAntialias = true })
                Canvas.DrawBitmap(DecorationsBitmap, 0, 0, DecorationsPaint);

            Canvas.Flush();
            return RenderFrameAnalyzer.CopyBitmapToBgraBytes(Composite);
        }

        /// <summary>
        /// Seeds flat map satellite tiles and returns a service ready for offline 2D rendering.
        /// </summary>
        /// <param name="Scenario">Pan/zoom scenario defining visible tile layout.</param>
        /// <param name="TileColor">Solid colour written into each required tile.</param>
        /// <returns>Tile service with all visible map tiles seeded.</returns>
        static public SatelliteTileService CreateSeededSatelliteMapService
            (
            FieldRenderer2DScenario Scenario,
            SKColor TileColor
            )
        {
            SatelliteTileService Satellite = new SatelliteTileService();
            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(Scenario.CentreLat, Scenario.ZoomFactor);
            SatelliteMapViewport Layout = SatelliteMapLayout.Compute(
                Scenario.CentreLat,
                Scenario.CentreLon,
                ViewportWidth,
                ViewportHeight,
                MetresPerPixel);
            foreach (SatelliteTileRequest Tile in Layout.Tiles)
                FieldRenderer3DBenchmark.SeedSolidSatelliteTile(Satellite, Layout.Zoom, Tile.TileX, Tile.TileY, TileColor);

            return Satellite;
        }

        /// <summary>
        /// Renders one 2D layer pass to an isolated bitmap.
        /// </summary>
        static private SKBitmap RenderSinglePass
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldRenderer2DScenario Scenario,
            FieldRenderPass Pass
            )
        {
            using SKBitmap FrameBitmap = new SKBitmap(
                ViewportWidth,
                ViewportHeight,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(FrameBitmap);
            SKRect Viewport = SKRect.Create(0, 0, ViewportWidth, ViewportHeight);
            ResolvePlanViewOrbit(Scenario, out double HeadingDeg, out double PitchDeg, out double DistanceM, out double OrbitLat, out double OrbitLon);

            FieldRenderer.Draw3D(
                Canvas,
                Viewport,
                Project,
                Surface,
                FieldContentView.Existing,
                Scenario.Display,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                Scenario.Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                Scenario.Display.Satellite ? Scenario.Satellite : null,
                Pass,
                UsePlanViewSatellite: true);

            Canvas.Flush();
            SKBitmap Copy = new SKBitmap(ViewportWidth, ViewportHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            FrameBitmap.CopyTo(Copy);
            return Copy;
        }

        /// <summary>
        /// Maps 2D pan/zoom state to locked nadir orbit parameters (mirrors production plan-view camera).
        /// </summary>
        static private void ResolvePlanViewOrbit
            (
            FieldRenderer2DScenario Scenario,
            out double HeadingDeg,
            out double PitchDeg,
            out double DistanceM,
            out double OrbitLat,
            out double OrbitLon
            )
        {
            HeadingDeg = PLAN_VIEW_HEADING_DEG;
            PitchDeg = PLAN_VIEW_PITCH_DEG;
            OrbitLat = Scenario.CentreLat;
            OrbitLon = Scenario.CentreLon;
            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(Scenario.CentreLat, Scenario.ZoomFactor);
            DistanceM = Math.Max(MIN_ORBIT_DISTANCE_M, MetresPerPixel * ViewportHeight);
        }
    }
}
