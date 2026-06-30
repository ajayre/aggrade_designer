using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Profiles survey and AGD import on the FakeField_01 fixtures and guards against regressions.
    /// </summary>
    public sealed class ImportPerformanceTests
    {
        /// <summary>
        /// FakeField_01 AGS (~12.8k points) should load quickly after streaming parse optimizations.
        /// </summary>
        [Fact]
        public void FakeField01_AgsImport_CompletesWithinBudget
            (
            )
        {
            Assert.True(File.Exists(TestDataPaths.FakeField01Ags), $"Missing fixture: {TestDataPaths.FakeField01Ags}");

            double MedianMs = ImportBenchmark.MedianMilliseconds(() =>
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(TestDataPaths.FakeField01Ags);
                Assert.True(Survey.InteriorPoints.Count + Survey.BoundaryPoints.Count > 10_000);
            });

            Assert.True(MedianMs < 250.0,
                $"AGS import too slow: {MedianMs:F1} ms (budget 250 ms).");
        }

        /// <summary>
        /// FakeField_01 AGD (~12.8k rows) should import quickly after single-pass parsing.
        /// </summary>
        [Fact]
        public void FakeField01_AgdImport_CompletesWithinBudget
            (
            )
        {
            Assert.True(File.Exists(TestDataPaths.FakeField01Agd), $"Missing fixture: {TestDataPaths.FakeField01Agd}");

            double MedianMs = ImportBenchmark.MedianMilliseconds(() =>
            {
                AgdImportResult Result = AgdService.Import(TestDataPaths.FakeField01Agd);
                Assert.True(Result.Grid.Bins.Count > 10_000);
                Assert.True(Result.Survey.InteriorPoints.Count + Result.Survey.BoundaryPoints.Count > 10_000);
            });

            Assert.True(MedianMs < 800.0,
                $"AGD import too slow: {MedianMs:F1} ms (budget 800 ms).");
        }

        /// <summary>
        /// Verifies AGD import still produces aligned bin centroids after performance changes.
        /// </summary>
        [Fact]
        public void FakeField01_AgdImport_BinsRemainAligned
            (
            )
        {
            AgdImportResult Result = AgdService.Import(TestDataPaths.FakeField01Agd);
            GridBinData Sample = Result.Grid.Bins.Find(B => B.Valid && B.X > 0 && B.Y > 0)!;
            Assert.NotNull(Sample);

            UTM.UTMCoordinate Utm = UTM.FromLatLon(Sample.CentroidLat, Sample.CentroidLon);
            double Ex = (Utm.Easting - Result.Grid.OriginEastingM) / Result.Grid.BinSizeM - 0.5;
            double Ey = (Utm.Northing - Result.Grid.OriginNorthingM) / Result.Grid.BinSizeM - 0.5;
            Assert.Equal(Sample.X, (int)Math.Round(Ex));
            Assert.Equal(Sample.Y, (int)Math.Round(Ey));
        }
    }
}
