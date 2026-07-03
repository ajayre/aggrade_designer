using System.Diagnostics;

using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Performance
{
    /// <summary>
    /// Compares AGD import prepare phase costs between fields that do and do not freeze in the app.
    /// </summary>
    [Trait("Category", "ImportHandoff")]
    [Trait("Category", "ImportHandoffComparison")]
    public sealed class ImportHandoffPhaseComparisonTests
    {
        static private string TheShop2AgdPath { get; } =
            Path.Combine(TestDataPaths.TestDataDirectory, "TheShop2_2ft.agd");

        /// <summary>
        /// Documents fixture differences that affect progressive prepare work.
        /// </summary>
        [Fact]
        public void FieldFixtures_DocumentStructuralDifferences
            (
            )
        {
            AgdImportResult FakeField = AgdService.Import(TestDataPaths.FakeField01Agd);
            AgdImportResult TheShop2 = AgdService.Import(TheShop2AgdPath);

            Assert.True(FakeField.Grid.Bins.Count > 10_000);
            Assert.True(TheShop2.Grid.Bins.Count > 0);
            Assert.True(FakeField.Survey.BoundaryPoints.Count > 0);
            Assert.Equal(0, TheShop2.Survey.BoundaryPoints.Count);
        }

        /// <summary>
        /// FakeField_01 spends more time in Existing-view prewarm than TheShop2 during import finishing.
        /// </summary>
        [Fact]
        public void FinishingDisplay_ExistingPrewarm_FakeField01_IsHeavierThanTheShop2
            (
            )
        {
            long FakeFieldMs = MeasureExistingPrewarmMs(CreateProject(TestDataPaths.FakeField01Agd));
            long TheShop2Ms = MeasureExistingPrewarmMs(CreateProject(TheShop2AgdPath));

            Assert.True(
                FakeFieldMs > TheShop2Ms,
                $"Expected FakeField_01 Existing prewarm to exceed TheShop2. fake={FakeFieldMs} ms shop={TheShop2Ms} ms.");
        }

        /// <summary>
        /// FakeField_01 full-quality frame build is heavier than TheShop2 after preview caches are warm.
        /// </summary>
        [Fact]
        public void FinishingDisplay_FullQualityFrameBuild_FakeField01_IsHeavierThanTheShop2
            (
            )
        {
            long FakeFieldMs = MeasureFullQualityFrameBuildMs(CreateProject(TestDataPaths.FakeField01Agd));
            long TheShop2Ms = MeasureFullQualityFrameBuildMs(CreateProject(TheShop2AgdPath));

            Assert.True(
                FakeFieldMs > TheShop2Ms,
                $"Expected FakeField_01 full frame build to exceed TheShop2. fake={FakeFieldMs} ms shop={TheShop2Ms} ms.");
        }

        static private ProjectDocument CreateProject
            (
            string Path
            )
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

        static private long MeasureExistingPrewarmMs
            (
            ProjectDocument Project
            )
        {
            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);
            DisplaySettings Display = new DisplaySettings();

            Stopwatch Watch = Stopwatch.StartNew();
            Surface.PrewarmDisplayCaches(
                FieldContentView.Existing,
                Display,
                null,
                PlanViewColorMapBuilder.PreviewSupersampleFactor,
                IncludeContours: false);
            Watch.Stop();
            return Watch.ElapsedMilliseconds;
        }

        static private long MeasureFullQualityFrameBuildMs
            (
            ProjectDocument Project
            )
        {
            FieldSurfaceSampler Surface = new FieldSurfaceSampler();
            Surface.BuildFromProject(Project);
            DisplaySettings Display = new DisplaySettings();

            Surface.PrewarmDisplayCaches(
                FieldContentView.Proposed,
                Display,
                null,
                PlanViewColorMapBuilder.PreviewSupersampleFactor,
                IncludeContours: false);

            Surface.UpgradeDisplayCacheQuality(FieldContentView.Proposed, Display);

            Stopwatch Watch = Stopwatch.StartNew();
            Surface.PrewarmDisplayCaches(
                FieldContentView.Existing,
                Display,
                null,
                PlanViewColorMapBuilder.PreviewSupersampleFactor,
                IncludeContours: false);
            Surface.UpgradeDisplayCacheQuality(FieldContentView.Proposed, Display);
            Watch.Stop();
            return Watch.ElapsedMilliseconds;
        }
    }
}
