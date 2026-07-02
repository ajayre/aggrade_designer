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
    }
}
