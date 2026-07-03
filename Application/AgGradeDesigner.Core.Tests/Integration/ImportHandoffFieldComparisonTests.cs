using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Import;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.Integration;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Integration
{
    /// <summary>
    /// Compares AGD import handoff behaviour between fields that do and do not freeze in the app.
    /// </summary>
    [Collection("ImportHandoff")]
    [Trait("Category", "ImportHandoff")]
    [Trait("Category", "ImportHandoffIntegration")]
    [Trait("Category", "ImportHandoffComparison")]
    public sealed class ImportHandoffFieldComparisonTests
    {
        /// <summary>
        /// TheShop2_2ft matches the handoff contract under the default headless scenario.
        /// </summary>
        [Fact]
        public async Task TheShop2_DefaultScenario_AfterHandoff_FirstRenderDoesNotSyncBuild
            (
            )
        {
            ImportHandoffRunResult Result = await RunScenarioAsync(CreateDefaultScenario(
                ImportHandoffProjectFactory.CreateTheShop2Project(),
                "TheShop2"));

            AssertHandoffSuccess(Result);
        }

        /// <summary>
        /// FakeField_01 must behave at least as well as TheShop2 under the default headless scenario.
        /// </summary>
        [Fact]
        public async Task FakeField01_DefaultScenario_AfterHandoff_FirstRenderDoesNotSyncBuild
            (
            )
        {
            ImportHandoffRunResult Result = await RunScenarioAsync(CreateDefaultScenario(
                ImportHandoffProjectFactory.CreateFakeField01Project(),
                "FakeField01"));

            AssertHandoffSuccess(Result);
        }

        /// <summary>
        /// Mirrors production import: camera fit uses the 800x600 fallback before the viewer is fully laid out.
        /// </summary>
        [Fact]
        public async Task ProductionImportScenario_TheShop2_DoesNotSyncBuildOnFirstRender
            (
            )
        {
            ImportHandoffRunResult Result = await RunScenarioAsync(CreateProductionImportScenario(
                ImportHandoffProjectFactory.CreateTheShop2Project(),
                "TheShop2"));

            AssertHandoffSuccess(Result);
        }

        /// <summary>
        /// FakeField_01 is the field that freezes in the app; this scenario should expose the regression.
        /// </summary>
        [Fact]
        public async Task ProductionImportScenario_FakeField01_DoesNotSyncBuildOnFirstRender
            (
            )
        {
            ImportHandoffRunResult FakeField = await RunScenarioAsync(CreateProductionImportScenario(
                ImportHandoffProjectFactory.CreateFakeField01Project(),
                "FakeField01"));
            ImportHandoffRunResult TheShop2 = await RunScenarioAsync(CreateProductionImportScenario(
                ImportHandoffProjectFactory.CreateTheShop2Project(),
                "TheShop2"));

            string Report = ImportHandoffHarness.BuildComparisonReport(FakeField, TheShop2);
            AssertHandoffSuccess(FakeField, Report);
            AssertHandoffSuccess(TheShop2, Report);
        }

        /// <summary>
        /// When only FakeField_01 breaks handoff, the comparison report must show a field-specific divergence.
        /// </summary>
        [Fact]
        public async Task ProductionImportScenario_FakeField01_MatchesTheShop2HandoffQuality
            (
            )
        {
            ImportHandoffRunResult FakeField = await RunScenarioAsync(CreateProductionImportScenario(
                ImportHandoffProjectFactory.CreateFakeField01Project(),
                "FakeField01"));
            ImportHandoffRunResult TheShop2 = await RunScenarioAsync(CreateProductionImportScenario(
                ImportHandoffProjectFactory.CreateTheShop2Project(),
                "TheShop2"));

            string Report = ImportHandoffHarness.BuildComparisonReport(FakeField, TheShop2);

            Assert.False(
                FakeField.FirstRenderUsedSyncFallback && !TheShop2.FirstRenderUsedSyncFallback,
                "FakeField_01 regressed relative to TheShop2_2ft under the production import scenario." +
                Environment.NewLine +
                Report);

            Assert.False(
                !FakeField.DiagnosticsAfterHandoff.CacheKeysMatch &&
                TheShop2.DiagnosticsAfterHandoff.CacheKeysMatch,
                "FakeField_01 lost cache-key alignment while TheShop2_2ft remained aligned." +
                Environment.NewLine +
                Report);
        }

        /// <summary>
        /// After a post-prepare viewport shrink, import handoff rebuilds at the live viewport size.
        /// </summary>
        [Fact]
        public async Task ProductionImportScenario_AfterViewportResize_BothFieldsCompleteHandoffWithoutSyncBuild
            (
            )
        {
            (ImportHandoffRunResult FakeFieldAfterResize, ImportHandoffRunResult TheShop2AfterResize) =
                await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
                {
                    ImportHandoffRunResult FakeField = await ImportHandoffHarness.RunPrepareAndHandoffAsync(
                        CreateProductionImportScenario(
                            ImportHandoffProjectFactory.CreateFakeField01Project(),
                            "FakeField01"),
                        ImportHandoffProjectFactory.ProductionDefaultViewportWidth,
                        ImportHandoffProjectFactory.ProductionDefaultViewportHeight);
                    ImportHandoffRunResult TheShop2 = await ImportHandoffHarness.RunPrepareAndHandoffAsync(
                        CreateProductionImportScenario(
                            ImportHandoffProjectFactory.CreateTheShop2Project(),
                            "TheShop2"),
                        ImportHandoffProjectFactory.ProductionDefaultViewportWidth,
                        ImportHandoffProjectFactory.ProductionDefaultViewportHeight);
                    return (FakeField, TheShop2);
                });

            string Report = ImportHandoffHarness.BuildComparisonReport(FakeFieldAfterResize, TheShop2AfterResize);

            AssertHandoffSuccess(FakeFieldAfterResize, Report);
            AssertHandoffSuccess(TheShop2AfterResize, Report);
        }

        /// <summary>
        /// Documents structural differences between the two AGD fixtures relevant to prepare cost.
        /// </summary>
        [Fact]
        public void FieldFixtures_HaveDistinctPrepareProfiles
            (
            )
        {
            AgdImportResult FakeField = AgdService.Import(TestDataPaths.FakeField01Agd);
            AgdImportResult TheShop2 = AgdService.Import(Path.Combine(
                TestDataPaths.TestDataDirectory,
                "TheShop2_2ft.agd"));

            Assert.True(FakeField.Grid.Bins.Count > 0);
            Assert.True(TheShop2.Grid.Bins.Count > 0);

            int FakeBoundaryPoints = FakeField.Survey.BoundaryPoints.Count;
            int ShopBoundaryPoints = TheShop2.Survey.BoundaryPoints.Count;
            Assert.True(FakeBoundaryPoints > 0);
            Assert.Equal(0, ShopBoundaryPoints);
        }

        static private ImportHandoffScenario CreateDefaultScenario
            (
            ProjectDocument Project,
            string FieldName
            )
        {
            return new ImportHandoffScenario
            {
                Project = Project,
                FieldName = FieldName
            };
        }

        static private ImportHandoffScenario CreateProductionImportScenario
            (
            ProjectDocument Project,
            string FieldName
            )
        {
            return new ImportHandoffScenario
            {
                Project = Project,
                FieldName = FieldName,
                ViewerWidth = ImportHandoffHarness.DefaultViewportWidth,
                ViewerHeight = ImportHandoffHarness.DefaultViewportHeight,
                FitViewportWidth = ImportHandoffProjectFactory.ProductionDefaultViewportWidth,
                FitViewportHeight = ImportHandoffProjectFactory.ProductionDefaultViewportHeight
            };
        }

        static private Task<ImportHandoffRunResult> RunScenarioAsync
            (
            ImportHandoffScenario Scenario
            )
        {
            return HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(
                () => ImportHandoffHarness.RunPrepareAndHandoffAsync(Scenario));
        }

        static private Task<ImportHandoffRunResult> RunScenarioWithPostHandoffResizeAsync
            (
            ImportHandoffScenario Scenario,
            int ResizeWidth,
            int ResizeHeight
            )
        {
            return HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(
                () => ImportHandoffHarness.RunPrepareAndHandoffAsync(
                    Scenario,
                    ResizeBeforeFirstRenderWidth: ResizeWidth,
                    ResizeBeforeFirstRenderHeight: ResizeHeight));
        }

        static private void AssertHandoffSuccess
            (
            ImportHandoffRunResult Result,
            string? ComparisonReport = null
            )
        {
            string Prefix = string.IsNullOrEmpty(ComparisonReport)
                ? $"[{Result.FieldName}] "
                : ComparisonReport + Environment.NewLine;

            Assert.True(Result.FirstRenderObserved, Prefix + "Post-handoff render was not observed.");
            Assert.False(
                Result.FirstRenderUsedSyncFallback,
                Prefix + "First render after import handoff used synchronous BuildAndStoreFrame fallback.");
            Assert.True(
                Result.DiagnosticsAfterHandoff.HasMaterializedComposite,
                Prefix + "Composite layers were not materialized before the first post-handoff render.");
            Assert.True(
                Result.DiagnosticsAfterHandoff.CacheKeysMatch,
                Prefix +
                $"Cache key mismatch. stored={Result.DiagnosticsAfterHandoff.StoredProposedCacheKey} current={Result.DiagnosticsAfterHandoff.CurrentCacheKey}");
            Assert.Empty(Result.ContractViolations);
            ImportViewerHandoffRules.Validate(Result.Events);
            ImportHandoffHarness.AssertWithinProcessingBudget(Result, ComparisonReport);
        }
    }
}
