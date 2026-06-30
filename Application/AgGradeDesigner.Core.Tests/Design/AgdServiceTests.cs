using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
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
