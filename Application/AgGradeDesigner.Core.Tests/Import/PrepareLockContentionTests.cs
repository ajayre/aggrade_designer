using System.Diagnostics;

using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Tests.Integration;
using AgGradeDesigner.Core.Tests.TestFixtures;

namespace AgGradeDesigner.Core.Tests.Import
{
    /// <summary>
    /// Regression tests for UI-thread prepare lock contention during import handoff.
    /// </summary>
    [Collection("ImportHandoff")]
    [Trait("Category", "ImportHandoff")]
    [Trait("Category", "ImportHandoffContract")]
    public sealed class PrepareLockContentionTests
    {
        /// <summary>
        /// UI diagnostics must time out instead of blocking while a worker holds the prepare lock.
        /// </summary>
        [Fact]
        public async Task UiDiagnostics_ReturnWithinTimeoutWhileBackgroundHoldsPrepareLock
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                FieldViewerControl Viewer = ImportHandoffHarness.CreateViewer(
                    ImportHandoffProjectFactory.CreateFakeField01Project());
                Viewer.EnsureSurfaceBuiltForFit();

                Task HoldTask = Viewer.TestHoldPrepareLockAsync(3000);
                await Task.Delay(100);

                Stopwatch Watch = Stopwatch.StartNew();
                FieldViewerControl.ImportHandoffDiagnosticSnapshot Diagnostics =
                    Viewer.TestCollectImportHandoffDiagnostics();
                Watch.Stop();

                Assert.True(
                    Watch.ElapsedMilliseconds < ViewerPrepareLock.UiAcquireTimeoutMs + 250,
                    $"Diagnostics blocked for {Watch.ElapsedMilliseconds} ms.");
                Assert.True(
                    Viewer.TestPrepareLockUiTimeoutCount >= 1,
                    "Expected at least one timed-out UI prepare lock wait.");
                Assert.True(Diagnostics.ViewportWidth > 0);

                await HoldTask;
                return true;
            });
        }

        /// <summary>
        /// Off-lock CutFill prewarm must not block UI diagnostics collection.
        /// </summary>
        [Fact]
        public async Task OffLockCutFillPrewarm_DoesNotBlockDiagnosticsCollection
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                FieldViewerControl Viewer = ImportHandoffHarness.CreateViewer(
                    ImportHandoffProjectFactory.CreateFakeField01Project());
                Viewer.EnsureSurfaceBuiltForFit();
                Viewer.TestScheduleLockedBackgroundPrewarm(2000);
                await Task.Delay(150);

                Stopwatch Watch = Stopwatch.StartNew();
                _ = Viewer.TestCollectImportHandoffDiagnostics();
                Watch.Stop();

                Assert.True(
                    Watch.ElapsedMilliseconds < ViewerPrepareLock.UiAcquireTimeoutMs + 250,
                    $"Diagnostics blocked for {Watch.ElapsedMilliseconds} ms during off-lock CutFill prewarm.");
                Assert.Equal(0, Viewer.TestPrepareLockUiTimeoutCount);
                return true;
            });
        }

        /// <summary>
        /// Simulated post-handoff render must not block indefinitely when the prepare lock is contended.
        /// </summary>
        [Fact]
        public async Task PostHandoffRenderSimulation_ReturnsWhileBackgroundHoldsPrepareLock
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                FieldViewerControl Viewer = ImportHandoffHarness.CreateViewer(
                    ImportHandoffProjectFactory.CreateFakeField01Project());
                Viewer.EnsureSurfaceBuiltForFit();
                Viewer.SuppressSynchronousPrepare = false;
                Viewer.TestMarkImportHandoffComplete();

                Task HoldTask = Viewer.TestHoldPrepareLockAsync(3000);
                await Task.Delay(100);

                Stopwatch Watch = Stopwatch.StartNew();
                bool UsedSyncFallback = Viewer.TestSimulatePostHandoffRender();
                Watch.Stop();

                Assert.True(
                    Watch.ElapsedMilliseconds < ViewerPrepareLock.UiRenderAcquireTimeoutMs + 250,
                    $"Post-handoff render simulation blocked for {Watch.ElapsedMilliseconds} ms.");
                Assert.False(UsedSyncFallback);

                await HoldTask;
                return true;
            });
        }
        /// <summary>
        /// Background prewarm resumes after UI pause is released instead of restarting from scratch.
        /// </summary>
        [Fact]
        public async Task BackgroundPrewarm_ResumesAfterUiPauseWithoutGenerationBump
            (
            )
        {
            await HeadlessAvaloniaBootstrap.RunOnUiThreadAsync(async () =>
            {
                FieldViewerControl Viewer = ImportHandoffHarness.CreateViewer(
                    ImportHandoffProjectFactory.CreateFakeField01Project());
                Viewer.EnsureSurfaceBuiltForFit();

                int GenerationBefore = Viewer.TestBackgroundPrewarmGeneration;
                Viewer.TestRequestBackgroundPrewarmPause("test");
                await Task.Delay(150);
                Viewer.TestReleaseBackgroundPrewarmPause("test");

                Assert.Equal(GenerationBefore, Viewer.TestBackgroundPrewarmGeneration);
                return true;
            });
        }
    }
}
