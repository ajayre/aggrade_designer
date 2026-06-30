using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Profiles end-to-end import plus viewer surface build on FakeField_01 fixtures.
    /// </summary>
    public sealed class ImportEndToEndPerformanceTests
    {
        /// <summary>
        /// Survey import plus surface build mirrors the post-import viewer preparation path.
        /// </summary>
        [Fact]
        public void FakeField01_AgsImport_AndSurfaceBuild_CompletesWithinBudget
            (
            )
        {
            double MedianMs = ImportBenchmark.MedianMilliseconds(() =>
            {
                SurveyModel Survey = new SurveyModel();
                Survey.LoadFromAgs(TestDataPaths.FakeField01Ags);

                ProjectDocument Project = new ProjectDocument { Survey = Survey };
                FieldSurfaceSampler Surface = new FieldSurfaceSampler();
                Surface.BuildFromProject(Project);
                Assert.True(Surface.Valid.Count(V => V) > 1000);
            });

            Assert.True(MedianMs < 800.0,
                $"AGS import + surface build too slow: {MedianMs:F1} ms (budget 800 ms).");
        }

        /// <summary>
        /// AGD import plus surface build mirrors the post-import viewer preparation path.
        /// </summary>
        [Fact]
        public void FakeField01_AgdImport_AndSurfaceBuild_CompletesWithinBudget
            (
            )
        {
            double MedianMs = ImportBenchmark.MedianMilliseconds(() =>
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
                int ValidCount = 0;
                for (int Index = 0; Index < Surface.Valid.Length; Index++)
                {
                    if (Surface.Valid[Index])
                        ValidCount++;
                }

                Assert.True(ValidCount > 1000);
            });

            Assert.True(MedianMs < 1600.0,
                $"AGD import + surface build too slow: {MedianMs:F1} ms (budget 1600 ms).");
        }

        /// <summary>
        /// Reports grid shape after AGD import to catch oversized sparse grids.
        /// </summary>
        [Fact]
        public void FakeField01_AgdImport_GridIsCompact
            (
            )
        {
            AgdImportResult Result = AgdService.Import(TestDataPaths.FakeField01Agd);
            DesignGrid Grid = Result.Grid;

            Assert.True(Grid.Bins.Count > 10_000, "Expected populated design bins from FakeField_01.");
            Assert.True(Grid.Width * Grid.Height < 1_000_000,
                $"Grid too large: {Grid.Width}x{Grid.Height} ({Grid.Width * Grid.Height} cells).");
        }
    }
}
