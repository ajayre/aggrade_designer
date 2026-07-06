using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Design
{
    /// <summary>
    /// Verifies AGD import infers design plane metadata for smooth proposed-surface rendering.
    /// </summary>
    public sealed class AgdImportPlaneInferenceTests
    {
        /// <summary>Expected TheShop2 main grade percent within tolerance.</summary>
        private const double SHOP2_EXPECTED_GRADE_PERCENT = 0.1;

        /// <summary>Allowed absolute error when matching TheShop2 fitted grade percent.</summary>
        private const double SHOP2_GRADE_TOLERANCE_PERCENT = 0.02;

        /// <summary>Expected TheShop2 downhill heading in degrees clockwise from north.</summary>
        private const double SHOP2_EXPECTED_HEADING_DEG = 270.0;

        /// <summary>Allowed absolute error when matching TheShop2 fitted heading.</summary>
        private const double SHOP2_HEADING_TOLERANCE_DEG = 5.0;

        /// <summary>Maximum deviation from the fitted plane treated as uniform.</summary>
        private const double PLANE_BIN_TOLERANCE_M = 0.001;

        /// <summary>Maximum viewer sample error against the fitted plane on TheShop2.</summary>
        private const double SHOP2_VIEWER_PLANE_TOLERANCE_M = 0.002;

        /// <summary>Surface resolution used when sampling the viewer raster.</summary>
        private const int SURFACE_RESOLUTION = 256;

        /// <summary>Path to the TheShop2 AGD regression fixture.</summary>
        static private string Shop2Path => Path.Combine(TestDataPaths.TestDataDirectory, "TheShop2_2ft.agd");

        /// <summary>
        /// Verifies minimal AGD import populates non-zero plane coefficients and composite grade.
        /// </summary>
        [Fact]
        public void Import_MinimalAgd_InfersPlaneMetadata
            (
            )
        {
            string Path = TempFile.Write(".agd", SurveySamples.MINIMAL_AGD);
            try
            {
                AgdImportResult Result = AgdService.Import(Path);
                DesignGrid Grid = Result.Grid;

                Assert.NotEqual(0.0, Grid.PlaneA + Grid.PlaneB + Grid.PlaneC);
                Assert.True(Grid.CompositeSlopePercent >= 0.0);
                Assert.True(Result.Parameters.MainSlopePercent >= 0.0);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies TheShop2 AGD import recovers a uniform 0.1% west-sloping design plane.
        /// </summary>
        [Fact]
        public void Import_TheShop2_InfersUniformPlaneDesign
            (
            )
        {
            if (!File.Exists(Shop2Path))
                return;

            AgdImportResult Result = AgdService.Import(Shop2Path);
            DesignGrid Grid = Result.Grid;

            Assert.InRange(
                Grid.CompositeSlopePercent,
                SHOP2_EXPECTED_GRADE_PERCENT - SHOP2_GRADE_TOLERANCE_PERCENT,
                SHOP2_EXPECTED_GRADE_PERCENT + SHOP2_GRADE_TOLERANCE_PERCENT);
            Assert.InRange(
                Grid.CompositeSlopeHeadingDeg,
                SHOP2_EXPECTED_HEADING_DEG - SHOP2_HEADING_TOLERANCE_DEG,
                SHOP2_EXPECTED_HEADING_DEG + SHOP2_HEADING_TOLERANCE_DEG);
            Assert.True(DesignGridProposedSurfaceBuilder.IsUniformPlaneDesign(Grid));

            DisplaySettings Display = new DisplaySettings { Contours = true };
            ProjectDocument Project = new ProjectDocument { DesignGrid = Grid };
            Assert.True(ContourVisibility.ShouldDrawContours(Display, Project, FieldContentView.Proposed));
        }

        /// <summary>
        /// Verifies TheShop2 AGD import renders proposed elevations from the fitted plane, not sparse bins.
        /// </summary>
        [Fact]
        public void Import_TheShop2_ProposedSurfaceMatchesFittedPlane
            (
            )
        {
            if (!File.Exists(Shop2Path))
                return;

            AgdImportResult Result = AgdService.Import(Shop2Path);
            ProjectDocument Project = new ProjectDocument
            {
                Survey = Result.Survey,
                DesignParameters = Result.Parameters,
                DesignGrid = Result.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project, Resolution: SURFACE_RESOLUTION);

            for (int Sy = 0; Sy < Surface.SampleHeight; Sy++)
            {
                double Lat = Surface.MaxLat -
                    (Surface.MaxLat - Surface.MinLat) * Sy / Math.Max(1, Surface.SampleHeight - 1);
                for (int Sx = 0; Sx < Surface.SampleWidth; Sx++)
                {
                    int Idx = Sy * Surface.SampleWidth + Sx;
                    if (!Surface.Valid[Idx])
                        continue;

                    double Lon = Surface.MinLon +
                        (Surface.MaxLon - Surface.MinLon) * Sx / Math.Max(1, Surface.SampleWidth - 1);
                    UTM.UTMCoordinate Utm = UTM.FromLatLon(Lat, Lon);
                    double ExpectedM = DesignGridProposedSurfaceBuilder.EvaluateProposedPlaneAtUtm(
                        Result.Grid,
                        Utm.Easting,
                        Utm.Northing);

                    Assert.Equal(
                        ExpectedM,
                        Surface.Proposed[Idx],
                        SHOP2_VIEWER_PLANE_TOLERANCE_M);
                }
            }
        }

        /// <summary>
        /// Verifies export and re-import preserve fitted plane coefficients within bin tolerance.
        /// </summary>
        [Fact]
        public void Export_AndImport_PreservesInferredPlane
            (
            )
        {
            string ImportPath = TempFile.Write(".agd", SurveySamples.MINIMAL_AGD);
            string ExportPath = TempFile.Write(".agd.out", string.Empty);
            try
            {
                AgdImportResult Original = AgdService.Import(ImportPath);
                AgdService.Export(ExportPath, Original.Survey, Original.Grid, Original.Parameters);
                AgdImportResult Reloaded = AgdService.Import(ExportPath);

                Assert.Equal(Original.Grid.PlaneA, Reloaded.Grid.PlaneA, PLANE_BIN_TOLERANCE_M);
                Assert.Equal(Original.Grid.PlaneB, Reloaded.Grid.PlaneB, PLANE_BIN_TOLERANCE_M);
                Assert.Equal(Original.Grid.PlaneC, Reloaded.Grid.PlaneC, PLANE_BIN_TOLERANCE_M);
                Assert.Equal(
                    Original.Grid.CompositeSlopePercent,
                    Reloaded.Grid.CompositeSlopePercent,
                    SHOP2_GRADE_TOLERANCE_PERCENT);
            }
            finally
            {
                TempFile.DeleteIfExists(ImportPath);
                TempFile.DeleteIfExists(ExportPath);
            }
        }
    }
}
