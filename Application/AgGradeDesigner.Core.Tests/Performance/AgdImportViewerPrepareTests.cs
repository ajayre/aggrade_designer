using System.Diagnostics;

using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Replicates the AGD import viewer-preparation path that stalled near 6-8% in the application.
    /// </summary>
    public sealed class AgdImportViewerPrepareTests
    {
        /// <summary>
        /// Mirrors ImportDesignAsync: bounds fit plus surface build with the same 5-42% progress subrange.
        /// </summary>
        [Fact]
        public void FakeField01_AgdImport_ViewerPrepare_Reaches10PercentWithinBudget
            (
            )
        {
            ProjectDocument Project = CreateImportedAgdProject();
            List<int> ProgressValues = new List<int>();
            Stopwatch Watch = Stopwatch.StartNew();

            FieldBounds.TryFromProject(Project, out _);

            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            IProgress<int> Progress = ProgressMapper.Subrange(
                new Progress<int>(Value => ProgressValues.Add(Value)),
                SegmentMin: 5,
                SegmentMax: 42);
            Surface.BuildFromProject(Project, Progress: Progress);

            Watch.Stop();

            Assert.True(
                ProgressValues.Exists(Value => Value >= 10),
                $"Progress never reached 10%. Samples: {string.Join(", ", ProgressValues.Take(40))}. Elapsed {Watch.ElapsedMilliseconds} ms.");

            Assert.True(Watch.ElapsedMilliseconds < 1500,
                $"Viewer prepare too slow: {Watch.ElapsedMilliseconds} ms (budget 1500 ms).");
        }

        /// <summary>
        /// Maximum median build time for the dense-grid warp overlay path in milliseconds.
        /// </summary>
        private const double DENSE_GRID_SURFACE_BUILD_BUDGET_MS = 8000.0;

        /// <summary>
        /// Exercises the dense-grid warp overlay path used after Create Design.
        /// </summary>
        [Fact]
        public void DenseDesignGrid_SampleDesignGridPath_CompletesWithinBudget
            (
            )
        {
            ProjectDocument Project = CreateImportedAgdProject();
            RepackGridForDenseSampling(Project.DesignGrid!);

            double MedianMs = ImportBenchmark.MedianMilliseconds(() =>
            {
                FieldSurfaceSampler Surface = new FieldSurfaceSampler();
                Surface.BuildFromProject(Project);
                Assert.True(Surface.Valid.Any(V => V));
            });

            Assert.True(MedianMs < DENSE_GRID_SURFACE_BUILD_BUDGET_MS,
                $"Dense design-grid surface build too slow: {MedianMs:F1} ms (budget {DENSE_GRID_SURFACE_BUILD_BUDGET_MS} ms).");
        }

        /// <summary>
        /// End-to-end import plus viewer prepare, matching the application flow after file selection.
        /// </summary>
        [Fact]
        public void FakeField01_AgdImport_FullApplicationFlow_CompletesWithinBudget
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

                FieldBounds.TryFromProject(Project, out _);

                FieldSurfaceSampler Surface = new FieldSurfaceSampler();
                Surface.BuildFromProject(
                    Project,
                    Progress: ProgressMapper.Subrange(new Progress<int>(_ => { }), 5, 42));
                Assert.True(Surface.Valid.Any(V => V));
            });

            Assert.True(MedianMs < 2000,
                $"Full AGD application flow too slow: {MedianMs:F1} ms (budget 2000 ms).");
        }

        static private ProjectDocument CreateImportedAgdProject
            (
            )
        {
            AgdImportResult Result = AgdService.Import(TestDataPaths.FakeField01Agd);
            return new ProjectDocument
            {
                Survey = Result.Survey,
                DesignParameters = Result.Parameters,
                DesignGrid = Result.Grid,
                SurveySourceFormat = SurveySourceFormat.Agd
            };
        }

        /// <summary>
        /// Repacks imported bins into a compact grid so fill ratio exceeds 50% and exercises SampleDesignGridInBinSpace.
        /// </summary>
        static private void RepackGridForDenseSampling
            (
            DesignGrid Grid
            )
        {
            List<GridBinData> ValidBins = Grid.Bins.Where(B => B.Valid).ToList();
            int Side = (int)Math.Ceiling(Math.Sqrt(ValidBins.Count * 1.1));
            Side = Math.Max(Side, 1);

            for (int Index = 0; Index < ValidBins.Count; Index++)
            {
                ValidBins[Index].X = Index % Side;
                ValidBins[Index].Y = Index / Side;
            }

            Grid.Width = Side;
            Grid.Height = Side;
            Grid.InvalidateBinIndex();
        }
    }
}
