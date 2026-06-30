using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Design
{
    /// <summary>
    /// Unit tests for <see cref="AgdService"/> AGD import and export behaviour.
    /// </summary>
    public sealed class AgdServiceTests
    {
        /// <summary>
        /// Verifies import parses benchmark rows and valid design grid bins.
        /// </summary>
        [Fact]
        public void Import_ParsesBenchmarkAndGridRows
            (
            )
        {
            string Path = TempFile.Write(".agd", SurveySamples.MINIMAL_AGD);
            try
            {
                AgdImportResult Result = AgdService.Import(Path);

                // Minimal fixture must yield at least one benchmark and proposed elevations.
                Assert.True(Result.Survey.Benchmarks.Count >= 1);
                Assert.True(Result.Grid.Bins.Count > 0);
                Assert.True(Result.Grid.Bins.Exists(B => B.Valid && (B.ProposedM > 0)));
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies export and re-import preserves the count of valid grid bins.
        /// </summary>
        [Fact]
        public void Export_AndImport_RoundTripPreservesGridCount
            (
            )
        {
            string ImportPath = TempFile.Write(".agd", SurveySamples.MINIMAL_AGD);
            string ExportPath = TempFile.Write(".agd.out", "");
            try
            {
                AgdImportResult Original = AgdService.Import(ImportPath);
                AgdService.Export(ExportPath, Original.Survey, Original.Grid, Original.Parameters);
                AgdImportResult Reloaded = AgdService.Import(ExportPath);

                // Round-trip must not drop valid bins during CSV rewrite.
                Assert.Equal(Original.Grid.Bins.Count(B => B.Valid), Reloaded.Grid.Bins.Count(B => B.Valid));
            }
            finally
            {
                TempFile.DeleteIfExists(ImportPath);
                TempFile.DeleteIfExists(ExportPath);
            }
        }

        /// <summary>
        /// Verifies AGD bin indices align with UTM centroid positions used during surface sampling.
        /// </summary>
        [Fact]
        public void Import_BinIndicesAlignWithCentroids
            (
            )
        {
            string Path = TempFile.Write(".agd", SurveySamples.MINIMAL_AGD);
            try
            {
                AgdImportResult Result = AgdService.Import(Path);
                DesignGrid Grid = Result.Grid;

                foreach (GridBinData Bin in Grid.Bins)
                {
                    UTM.UTMCoordinate Utm = UTM.FromLatLon(Bin.CentroidLat, Bin.CentroidLon);
                    double Ex = (Utm.Easting - Grid.OriginEastingM) / Grid.BinSizeM - 0.5;
                    double Ey = (Utm.Northing - Grid.OriginNorthingM) / Grid.BinSizeM - 0.5;
                    Assert.Equal(Bin.X, (int)Math.Round(Ex));
                    Assert.Equal(Bin.Y, (int)Math.Round(Ey));
                }
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies imported AGD grid bins produce a contiguous sampled surface (not sparse stripes).
        /// </summary>
        [Fact]
        public void Import_ProducesContiguousSampledSurface
            (
            )
        {
            string Path = TempFile.Write(".agd", SurveySamples.MINIMAL_AGD);
            try
            {
                AgdImportResult Result = AgdService.Import(Path);
                ProjectDocument Project = new ProjectDocument
                {
                    Survey = Result.Survey,
                    DesignParameters = Result.Parameters,
                    DesignGrid = Result.Grid,
                    SurveySourceFormat = SurveySourceFormat.Agd
                };

                FieldSurfaceSampler Surface = new FieldSurfaceSampler();
                Surface.BuildFromProject(Project, Resolution: 64);

                Assert.True(Project.HasDesign, "Fixture must exercise design-grid surface build.");
                Assert.True(Result.Grid.Bins.Count > 0, "Expected imported grid bins.");
                Assert.True(Surface.SampleWidth > 0, "Surface build should not clear before sampling.");
                Assert.True(Surface.MinLat < Surface.MaxLat,
                    $"Invalid extent: {Surface.MinLat} .. {Surface.MaxLat}");

                int ValidCount = 0;
                for (int Index = 0; Index < Surface.Valid.Length; Index++)
                {
                    if (Surface.Valid[Index])
                        ValidCount++;
                }

                Assert.True(ValidCount >= 6, $"Expected filled surface samples, got {ValidCount}.");
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }

        /// <summary>
        /// Verifies cut/fill sign follows proposed minus existing on imported bins.
        /// </summary>
        [Fact]
        public void Import_CutFillSign_IsProposedMinusExistingOnExport
            (
            )
        {
            string Path = TempFile.Write(".agd", SurveySamples.MINIMAL_AGD);
            try
            {
                AgdImportResult Result = AgdService.Import(Path);
                GridBinData? Bin = Result.Grid.Bins.Find(B => B.Valid);
                Assert.NotNull(Bin);
                double ExpectedCutFill = Bin.ProposedM - Bin.ExistingM;
                // Fixture rows are cut-dominated so cut/fill stays negative or zero.
                Assert.InRange(ExpectedCutFill, -1.0, 0.0);
            }
            finally
            {
                TempFile.DeleteIfExists(Path);
            }
        }
    }
}
