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
                CentreLat = 36.0,
                CentreLon = -90.0,
                ZoomFactor = 1.0
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
            Assert.InRange(View.CentreLat, 39.5, 40.5);
            Assert.InRange(View.CentreLon, -93.6, -93.4);
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

            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, 800, 600);

            // Fit result must land in a visible scale band with zoom above unity.
            Assert.InRange(Fit.MetresPerPixel, 0.1, 50.0);
            Assert.True(Fit.ZoomFactor > 1.0);
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
            Assert.Equal(0, Result.PlaceholdersDrawn);
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

            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, 800, 600);
            MapViewport Viewport = new MapViewport { Width = 800, Height = 600 };

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
                Assert.True(MapProjection.IsOnScreen(X, Y, Viewport, 16));
            }
        }
    }
}
