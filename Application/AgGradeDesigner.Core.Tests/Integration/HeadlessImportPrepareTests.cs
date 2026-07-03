using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Import;
using AgGradeDesigner.Core.Tests.Import;
using AgGradeDesigner.Core.Tests.Integration;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Integration
{
    /// <summary>
    /// Layer 2 headless integration tests for AGD import viewer handoff.
    /// </summary>
    [Collection("ImportHandoff")]
    [Trait("Category", "ImportHandoff")]
    [Trait("Category", "ImportHandoffIntegration")]
    public sealed class HeadlessImportPrepareTests
    {
        static private Task<ImportHandoffRunResult>? _HarnessTask;

        /// <summary>
        /// Progressive prepare reaches the early preview callback for FakeField_01.
        /// </summary>
        [Fact]
        public async Task FakeField01_Prepare_InvokesEarlyPreviewCallback
            (
            )
        {
            ImportHandoffRunResult Result = await GetHarnessResultAsync();

            ImportHandoffHarness.AssertWithinProcessingBudget(Result);
            Assert.True(Result.EarlyCallbackInvoked);
            Assert.Contains(
                Result.Events,
                Event => Event.Kind == ImportViewerHandoffEventKind.PreviewMaterialized);
        }

        /// <summary>
        /// After handoff, composite layer bitmaps must be ready for the fast render path.
        /// </summary>
        [Fact]
        public async Task FakeField01_AfterHandoff_CompositeLayersMaterialized
            (
            )
        {
            ImportHandoffRunResult Result = await GetHarnessResultAsync();

            Assert.True(
                Result.HasMaterializedComposite,
                "Composite layers were not materialized before the first post-handoff render.");
        }

        /// <summary>
        /// The first render after import handoff must not use synchronous frame build fallback.
        /// </summary>
        [Fact]
        public async Task FakeField01_AfterHandoff_FirstRenderDoesNotSyncBuild
            (
            )
        {
            ImportHandoffRunResult Result = await GetHarnessResultAsync();

            Assert.True(Result.FirstRenderObserved, "Post-handoff render was not observed.");
            Assert.False(
                Result.FirstRenderUsedSyncFallback,
                "First render after import handoff used synchronous BuildAndStoreFrame fallback.");
            Assert.Equal(0, Result.SyncFrameBuildCount);
            Assert.Equal(0, Result.SurfaceRebuildCount);
        }

        /// <summary>
        /// The recorded AGD import handoff timeline satisfies the responsiveness contract.
        /// </summary>
        [Fact]
        public async Task FakeField01_AfterHandoff_TimelineMatchesContract
            (
            )
        {
            ImportHandoffRunResult Result = await GetHarnessResultAsync();

            ImportViewerHandoffRules.Validate(Result.Events);
        }

        static private Task<ImportHandoffRunResult> GetHarnessResultAsync
            (
            )
        {
            return _HarnessTask ??= HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                ImportHandoffScenario Scenario = new ImportHandoffScenario
                {
                    Project = ImportHandoffProjectFactory.CreateFakeField01Project(),
                    FieldName = "FakeField01"
                };
                return await ImportHandoffHarness.RunPrepareAndHandoffAsync(Scenario);
            });
        }
    }
}
