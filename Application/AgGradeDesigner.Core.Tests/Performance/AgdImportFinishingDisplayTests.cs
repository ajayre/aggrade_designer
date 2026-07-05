using System.Diagnostics;

using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Guards the AGD import "Finishing display" phase from blocking on alternate content views.
    /// </summary>
    public sealed class AgdImportFinishingDisplayTests
    {
        /// <summary>
        /// Mirrors the post-preview critical path: upgrade the active view and skip Existing prewarm.
        /// </summary>
        [Fact]
        public void FakeField01_FinishingDisplay_ActiveViewUpgrade_CompletesWithinBudget
            (
            )
        {
            ProjectDocument Project = CreateImportedAgdProject();
            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);

            DisplaySettings Display = new DisplaySettings();
            List<int> ProgressValues = new List<int>();
            Stopwatch Watch = Stopwatch.StartNew();

            Surface.PrewarmDisplayCaches(
                FieldContentView.Proposed,
                Display,
                ProgressMapper.Subrange(new Progress<int>(Value => ProgressValues.Add(Value)), 55, 72),
                PlanViewColorMapBuilder.PreviewSupersampleFactor,
                IncludeContours: false,
                Project: Project);

            Surface.UpgradeDisplayCacheQuality(
                FieldContentView.Proposed,
                Display,
                ProgressMapper.Subrange(new Progress<int>(Value => ProgressValues.Add(Value)), 72, 90),
                Project);

            Watch.Stop();

            Assert.Contains(ProgressValues, Value => Value >= 80);

            Assert.True(Watch.ElapsedMilliseconds < 7000,
                $"Active-view finishing phase too slow: {Watch.ElapsedMilliseconds} ms (budget 7000 ms).");
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
    }
}
