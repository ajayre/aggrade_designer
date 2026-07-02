using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Unit tests for <see cref="FieldMapView"/> camera fit and satellite rendering integration.
    /// </summary>
    public sealed class FieldMapViewTests
    {
        /// <summary>
        /// Default centre lat used by this component.
        /// </summary>

        private const double DEFAULT_CENTRE_LAT = 36.0;
        /// <summary>
        /// Default centre lon used by this component.
        /// </summary>

        private const double DEFAULT_CENTRE_LON = -90.0;
        /// <summary>
        /// Default zoom factor used by this component.
        /// </summary>

        private const double DEFAULT_ZOOM_FACTOR = 1.0;
        /// <summary>
        /// Viewport width used by this component.
        /// </summary>

        private const int VIEWPORT_WIDTH = 800;
        /// <summary>
        /// Viewport height used by this component.
        /// </summary>

        private const int VIEWPORT_HEIGHT = 600;
        /// <summary>
        /// Fit lat min used by this component.
        /// </summary>

        private const double FIT_LAT_MIN = 39.5;
        /// <summary>
        /// Fit lat max used by this component.
        /// </summary>

        private const double FIT_LAT_MAX = 40.5;
        /// <summary>
        /// Fit lon min used by this component.
        /// </summary>

        private const double FIT_LON_MIN = -93.6;
        /// <summary>
        /// Fit lon max used by this component.
        /// </summary>

        private const double FIT_LON_MAX = -93.4;
        /// <summary>
        /// Min metres per pixel used by this component.
        /// </summary>

        private const double MIN_METRES_PER_PIXEL = 0.1;
        /// <summary>
        /// Max metres per pixel used by this component.
        /// </summary>

        private const double MAX_METRES_PER_PIXEL = 50.0;
        /// <summary>
        /// Min zoom factor used by this component.
        /// </summary>

        private const double MIN_ZOOM_FACTOR = 1.0;
        /// <summary>
        /// Expected value used when asserting placeholder count.
        /// </summary>

        private const int EXPECTED_PLACEHOLDER_COUNT = 0;
        /// <summary>
        /// Screen margin px used by this component.
        /// </summary>

        private const int SCREEN_MARGIN_PX = 16;

        /// <summary>
        /// Loads a minimal AGD fixture into a project document for map view tests.
        /// </summary>
        /// <returns>Project with survey, parameters, and grid from the minimal AGD sample.</returns>
        static private ProjectDocument LoadMinimalAgdProject
            (
            )
        {
            string Path = TempFile.Write(".agd", SurveySamples.MINIMAL_AGD);
            try
            {
                AgdImportResult Result = AgdService.Import(Path);
                return new ProjectDocument
                {
                    Survey = Result.Survey,
                    DesignParameters = Result.Parameters,
                    DesignGrid = Result.Grid,
                    SurveySourceFormat = SurveySourceFormat.Agd
                };
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies the default camera does not frame an imported AGD field.
        /// </summary>
        [Fact]
        public void DefaultCamera_MissesImportedAgdField
            (
            )
        {
            ProjectDocument Project = LoadMinimalAgdProject();
            FieldMapView View = new FieldMapView
            {
                CentreLat = DEFAULT_CENTRE_LAT,
                CentreLon = DEFAULT_CENTRE_LON,
                ZoomFactor = DEFAULT_ZOOM_FACTOR
            };

            // Startup camera centre is far from the Iowa fixture bounds.
            Assert.False(View.FieldFillsViewport(Project));
        }

        /// <summary>
        /// Verifies fit-to-project centres the camera on an imported AGD field.
        /// </summary>
        [Fact]
        public void FitToProject_CentresOnImportedAgdField
            (
            )
        {
            ProjectDocument Project = LoadMinimalAgdProject();
            FieldMapView View = new FieldMapView();
            View.FitToProject(Project);

            // Fit should move centre near the fixture and fill the viewport.
            Assert.InRange(View.CentreLat, FIT_LAT_MIN, FIT_LAT_MAX);
            Assert.InRange(View.CentreLon, FIT_LON_MIN, FIT_LON_MAX);
            Assert.True(View.FieldFillsViewport(Project));
        }

        /// <summary>
        /// Verifies map view fitter yields sensible metres per pixel for a small field.
        /// </summary>
        [Fact]
        public void MapViewFitter_ProducesVisibleMetresPerPixelForSmallField
            (
            )
        {
            ProjectDocument Project = LoadMinimalAgdProject();
            Assert.True(FieldBounds.TryFromProject(Project, out FieldBounds Bounds));

            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);

            // Fit result must land in a visible scale band with zoom above unity.
            Assert.InRange(Fit.MetresPerPixel, MIN_METRES_PER_PIXEL, MAX_METRES_PER_PIXEL);
            Assert.True(Fit.ZoomFactor > MIN_ZOOM_FACTOR);
        }

        /// <summary>
        /// Verifies AGD import with fitted view renders live satellite imagery.
        /// </summary>
        [Fact]
        public async Task AgdImport_FittedView_RendersSatelliteImagery
            (
            )
        {
            using HttpClient Client = new HttpClient();
            Client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "AgGradeDesigner-Tests/0.1");

            ProjectDocument Project = LoadMinimalAgdProject();
            FieldMapView View = new FieldMapView { ShowSatellite = true };
            View.FitToProject(Project);

            SatelliteMapRenderResult Result = await View.RenderSatelliteAsync(new SatelliteTileService(Client));

            // Live render must draw tiles without placeholders and produce varied pixels.
            Assert.True(View.FieldFillsViewport(Project));
            Assert.True(Result.HasImagery);
            Assert.Equal(EXPECTED_PLACEHOLDER_COUNT, Result.PlaceholdersDrawn);
            Assert.True(Result.TilesDrawn > 0);
            Assert.NotNull(Result.PngBytes);
            Assert.True(ImageBytes.HasVariedPixels(Result.PngBytes!));
        }

        /// <summary>
        /// Verifies field corners project inside the viewport after fit.
        /// </summary>
        [Fact]
        public void FieldCorners_ProjectInsideViewportAfterFit
            (
            )
        {
            ProjectDocument Project = LoadMinimalAgdProject();
            Assert.True(FieldBounds.TryFromProject(Project, out FieldBounds Bounds));

            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, VIEWPORT_WIDTH, VIEWPORT_HEIGHT);
            MapViewport Viewport = new MapViewport { Width = VIEWPORT_WIDTH, Height = VIEWPORT_HEIGHT };

            (double Lat, double Lon)[] Corners = new[]
            {
                (Bounds.MinLat, Bounds.MinLon),
                (Bounds.MinLat, Bounds.MaxLon),
                (Bounds.MaxLat, Bounds.MinLon),
                (Bounds.MaxLat, Bounds.MaxLon)
            };
            foreach ((double Lat, double Lon) in Corners)
            {
                MapProjection.LatLonToScreen(
                    Lat,
                    Lon,
                    Fit.CentreLat,
                    Fit.CentreLon,
                    Fit.MetresPerPixel,
                    Viewport,
                    out float X,
                    out float Y);

                // Each corner must remain on-screen with a small margin after fit.
                Assert.True(MapProjection.IsOnScreen(X, Y, Viewport, SCREEN_MARGIN_PX));
            }
        }
    }
}
