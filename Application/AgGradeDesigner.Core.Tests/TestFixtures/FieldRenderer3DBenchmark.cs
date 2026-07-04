using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Profiles full <see cref="FieldRenderer.Draw3D"/> frames for performance regression tests.
    /// </summary>
    static internal class FieldRenderer3DBenchmark
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
        /// Renders one 3D orbit frame through the production renderer path.
        /// </summary>
        static public void Render3DFrame
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            DisplaySettings Display,
            double HeadingDeg = 45.0,
            double PitchDeg = 30.0,
            double DistanceM = 800.0,
            SatelliteTileService? Satellite = null
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

            double OrbitLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double OrbitLon = (Surface.MinLon + Surface.MaxLon) * 0.5;

            FieldRenderer.Draw3D(
                Canvas,
                Viewport,
                Project,
                Surface,
                FieldContentView.Existing,
                Display,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                Display.Satellite ? Satellite : null,
                FieldRenderPass.Base);

            FieldRenderer.Draw3D(
                Canvas,
                Viewport,
                Project,
                Surface,
                FieldContentView.Existing,
                Display,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                Display.Satellite ? Satellite : null,
                FieldRenderPass.SurfaceOverlay);

            FieldRenderer.Draw3D(
                Canvas,
                Viewport,
                Project,
                Surface,
                FieldContentView.Existing,
                Display,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                Display.Satellite ? Satellite : null,
                FieldRenderPass.Decorations);

            Canvas.Flush();
        }

        /// <summary>
        /// Opacity percent divisor converting percent to 0–1 alpha.
        /// </summary>
        internal const double OPACITY_PERCENT_TO_UNIT = 100.0;

        /// <summary>
        /// Minimum topo overlay opacity in unit range.
        /// </summary>
        internal const double MIN_OPACITY_UNIT = 0.0;

        /// <summary>
        /// Renders one monolithic 3D frame (production path at 100% topo opacity) and returns pixel bytes.
        /// </summary>
        /// <param name="Surface">Built field surface.</param>
        /// <param name="Project">Project document.</param>
        /// <param name="Scenario">Camera and display parameters.</param>
        /// <returns>BGRA8888 pixel buffer for the composited frame.</returns>
        static public byte[] Render3DMonolithicBytes
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldRenderer3DScenario Scenario
            )
        {
            using SKBitmap FrameBitmap = Render3DMonolithicBitmap(Surface, Project, Scenario);
            return RenderFrameAnalyzer.CopyBitmapToBgraBytes(FrameBitmap);
        }

        /// <summary>
        /// Renders one monolithic 3D frame and returns an owned bitmap copy.
        /// </summary>
        /// <param name="Surface">Built field surface.</param>
        /// <param name="Project">Project document.</param>
        /// <param name="Scenario">Camera and display parameters.</param>
        /// <returns>Composited monolithic frame bitmap.</returns>
        static public SKBitmap Render3DMonolithicBitmap
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldRenderer3DScenario Scenario
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

            ResolveOrbitCentre(Surface, Scenario, out double OrbitLat, out double OrbitLon);

            FieldRenderer.Draw3D(
                Canvas,
                Viewport,
                Project,
                Surface,
                FieldContentView.Existing,
                Scenario.Display,
                Scenario.HeadingDeg,
                Scenario.PitchDeg,
                Scenario.DistanceM,
                Scenario.Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                Scenario.Display.Satellite ? Scenario.Satellite : null,
                FieldRenderPass.Monolithic);

            Canvas.Flush();
            SKBitmap Copy = new SKBitmap(ViewportWidth, ViewportHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            FrameBitmap.CopyTo(Copy);
            return Copy;
        }

        /// <summary>
        /// Renders base, topo, and decoration layers separately then composites with topo opacity (layered 3D path).
        /// </summary>
        /// <param name="Surface">Built field surface.</param>
        /// <param name="Project">Project document.</param>
        /// <param name="Scenario">Camera and display parameters.</param>
        /// <returns>BGRA8888 pixel buffer matching viewer layered compositing.</returns>
        static public byte[] Render3DLayeredCompositeBytes
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldRenderer3DScenario Scenario
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
                using SKPaint TopoPaint = new SKPaint { IsAntialias = true, Color = SKColors.White.WithAlpha((byte)(Opacity * byte.MaxValue)) };
                Canvas.DrawBitmap(TopoBitmap, 0, 0, TopoPaint);
            }

            using (SKPaint DecorationsPaint = new SKPaint { IsAntialias = true })
                Canvas.DrawBitmap(DecorationsBitmap, 0, 0, DecorationsPaint);

            Canvas.Flush();
            return RenderFrameAnalyzer.CopyBitmapToBgraBytes(Composite);
        }

        /// <summary>
        /// Seeds satellite tiles and returns a service ready for offline 3D plinth rendering.
        /// </summary>
        /// <param name="Surface">Built field surface.</param>
        /// <param name="TileColor">Solid colour written into each required tile.</param>
        /// <returns>Tile service with all plinth tiles seeded.</returns>
        static public SatelliteTileService CreateSeededSatelliteService
            (
            FieldSurfaceSampler Surface,
            SKColor TileColor
            )
        {
            SatelliteTileService Satellite = new SatelliteTileService();
            double OrbitLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double OrbitLon = (Surface.MinLon + Surface.MaxLon) * 0.5;
            const double PLINTH_DIAGONAL_SIDE_MULTIPLIER = 2.0;
            const double METRES_PER_MILE = 1609.344;
            const double METRES_PER_PIXEL = 3.0;
            (int Zoom, IReadOnlyList<(int TileX, int TileY)> Tiles) = SatelliteMapLayout3D.ComputePlinthTiles(
                OrbitLat,
                OrbitLon,
                METRES_PER_MILE * PLINTH_DIAGONAL_SIDE_MULTIPLIER * Math.Sqrt(2.0),
                MetresPerPixel: METRES_PER_PIXEL);
            foreach ((int TileX, int TileY) in Tiles)
                SeedSolidSatelliteTile(Satellite, Zoom, TileX, TileY, TileColor);

            return Satellite;
        }

        /// <summary>
        /// Renders one 3D frame and returns a copy of the bitmap for pixel-level assertions.
        /// </summary>
        static public SKBitmap Render3DFrameBitmap
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            DisplaySettings Display,
            double HeadingDeg = 45.0,
            double PitchDeg = 30.0,
            double DistanceM = 800.0,
            SatelliteTileService? Satellite = null
            )
        {
            SKBitmap Copy = new SKBitmap(ViewportWidth, ViewportHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKBitmap FrameBitmap = new SKBitmap(
                ViewportWidth,
                ViewportHeight,
                SKColorType.Bgra8888,
                SKAlphaType.Premul);
            using SKCanvas Canvas = new SKCanvas(FrameBitmap);
            SKRect Viewport = SKRect.Create(0, 0, ViewportWidth, ViewportHeight);

            double OrbitLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double OrbitLon = (Surface.MinLon + Surface.MaxLon) * 0.5;

            FieldRenderer.Draw3D(
                Canvas,
                Viewport,
                Project,
                Surface,
                FieldContentView.Existing,
                Display,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                Display.Satellite ? Satellite : null,
                FieldRenderPass.Base);

            FieldRenderer.Draw3D(
                Canvas,
                Viewport,
                Project,
                Surface,
                FieldContentView.Existing,
                Display,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                Display.Satellite ? Satellite : null,
                FieldRenderPass.SurfaceOverlay);

            FieldRenderer.Draw3D(
                Canvas,
                Viewport,
                Project,
                Surface,
                FieldContentView.Existing,
                Display,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                Display.Satellite ? Satellite : null,
                FieldRenderPass.Decorations);

            Canvas.Flush();
            FrameBitmap.CopyTo(Copy);
            return Copy;
        }

        /// <summary>
        /// Builds a warm surface and project from FakeField_01.
        /// </summary>
        static public (FieldSurfaceSampler Surface, ProjectDocument Project) BuildFakeField01Scene
            (
            )
        {
            AgdImportResult Result = AgdService.Import(TestDataPaths.FakeField01Agd);
            ProjectDocument Project = new ProjectDocument
            {
                Survey = Result.Survey,
                DesignParameters = Result.Parameters,
                DesignGrid = Result.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);
            Surface.PrewarmDisplayCaches(FieldContentView.Existing, CreateDefaultDisplay());
            return (Surface, Project);
        }

        /// <summary>
        /// Builds The Shop 2 at 256 px resolution for heavier mesh regression coverage.
        /// </summary>
        static public (FieldSurfaceSampler Surface, ProjectDocument Project) BuildShop2Scene
            (
            )
        {
            string Shop2Path = Path.Combine(TestDataPaths.TestDataDirectory, "TheShop2_2ft.agd");
            AgdImportResult Result = AgdService.Import(Shop2Path);
            ProjectDocument Project = new ProjectDocument
            {
                Survey = Result.Survey,
                DesignParameters = Result.Parameters,
                DesignGrid = Result.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project, Resolution: 256);
            DisplaySettings Display = CreateDefaultDisplay();
            Surface.PrewarmDisplayCaches(FieldContentView.Existing, Display);
            return (Surface, Project);
        }

        /// <summary>
        /// Seeds a solid-colour tile so satellite-on 3D frames avoid network I/O.
        /// </summary>
        static public void SeedSolidSatelliteTile
            (
            SatelliteTileService Satellite,
            int Zoom,
            int TileX,
            int TileY,
            SKColor Color
            )
        {
            using SKBitmap Tile = new SKBitmap(256, 256);
            Tile.Erase(Color);
            using SKImage Image = SKImage.FromBitmap(Tile);
            using SKData Data = Image.Encode(SKEncodedImageFormat.Png, 100);
            Satellite.SeedTile(Zoom, TileX, TileY, Data.ToArray());
        }

        static public DisplaySettings CreateDefaultDisplay
            (
            bool Satellite = false
            )
        {
            return new DisplaySettings
            {
                Satellite = Satellite,
                Contours = true,
                Hillshade = true,
                SurveyPoints = false,
                Benchmarks = false
            };
        }

        /// <summary>
        /// Renders one 3D layer pass to an isolated bitmap.
        /// </summary>
        static private SKBitmap RenderSinglePass
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            FieldRenderer3DScenario Scenario,
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
            ResolveOrbitCentre(Surface, Scenario, out double OrbitLat, out double OrbitLon);

            FieldRenderer.Draw3D(
                Canvas,
                Viewport,
                Project,
                Surface,
                FieldContentView.Existing,
                Scenario.Display,
                Scenario.HeadingDeg,
                Scenario.PitchDeg,
                Scenario.DistanceM,
                Scenario.Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                Scenario.Display.Satellite ? Scenario.Satellite : null,
                Pass);

            Canvas.Flush();
            SKBitmap Copy = new SKBitmap(ViewportWidth, ViewportHeight, SKColorType.Bgra8888, SKAlphaType.Premul);
            FrameBitmap.CopyTo(Copy);
            return Copy;
        }

        /// <summary>
        /// Resolves orbit centre from scenario overrides or field midpoint.
        /// </summary>
        static private void ResolveOrbitCentre
            (
            FieldSurfaceSampler Surface,
            FieldRenderer3DScenario Scenario,
            out double OrbitLat,
            out double OrbitLon
            )
        {
            OrbitLat = double.IsNaN(Scenario.OrbitCentreLat)
                ? (Surface.MinLat + Surface.MaxLat) * 0.5
                : Scenario.OrbitCentreLat;
            OrbitLon = double.IsNaN(Scenario.OrbitCentreLon)
                ? (Surface.MinLon + Surface.MaxLon) * 0.5
                : Scenario.OrbitCentreLon;
        }
    }
}
