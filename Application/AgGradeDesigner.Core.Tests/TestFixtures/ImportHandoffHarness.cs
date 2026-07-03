using System.Diagnostics;
using System.Text;

using AgGradeDesigner.Controls;
using AgGradeDesigner.Core.Import;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Tests.Import;

using Avalonia;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Runs progressive AGD import prepare and handoff against a headless viewer control.
    /// </summary>
    internal static class ImportHandoffHarness
    {
        /// <summary>Default headless viewport width.</summary>
        internal const int DefaultViewportWidth = 1280;

        /// <summary>Default headless viewport height.</summary>
        internal const int DefaultViewportHeight = 720;

        /// <summary>Maximum allowed AGD import prepare plus handoff time in milliseconds.</summary>
        internal const int MaxAgdProcessingMs = 60_000;

        /// <summary>
        /// Configures a viewer for AGD import handoff testing.
        /// </summary>
        internal static FieldViewerControl CreateViewer
            (
            ProjectDocument Project,
            int Width = DefaultViewportWidth,
            int Height = DefaultViewportHeight
            )
        {
            FieldViewerControl Viewer = new FieldViewerControl
            {
                Width = Width,
                Height = Height,
                Project = Project,
                DisplaySettings = Project.ViewerState.Display,
                SuppressSynchronousPrepare = true
            };

            Viewer.Measure(new Size(Width, Height));
            Viewer.Arrange(new Rect(0, 0, Width, Height));
            Viewer.UpdateLayout();
            return Viewer;
        }

        /// <summary>
        /// Runs progressive prepare and simulates the AGD import handoff contract.
        /// </summary>
        internal static Task<ImportHandoffRunResult> RunPrepareAndHandoffAsync
            (
            ImportHandoffScenario Scenario,
            int? ResizeBeforeFirstRenderWidth = null,
            int? ResizeBeforeFirstRenderHeight = null
            )
        {
            FieldViewerControl Viewer = CreateViewer(
                Scenario.Project,
                Scenario.ViewerWidth,
                Scenario.ViewerHeight);

            if (Scenario.FitViewportWidth.HasValue && Scenario.FitViewportHeight.HasValue)
            {
                ImportHandoffProjectFactory.ApplyImportCameraFit(
                    Scenario.Project,
                    Viewer,
                    Scenario.FitViewportWidth.Value,
                    Scenario.FitViewportHeight.Value);
            }

            return RunPrepareAndHandoffAsync(
                Viewer,
                Scenario.FieldName,
                Scenario,
                ResizeBeforeFirstRenderWidth,
                ResizeBeforeFirstRenderHeight);
        }

        /// <summary>
        /// Runs progressive prepare and simulates the AGD import handoff contract.
        /// </summary>
        internal static Task<ImportHandoffRunResult> RunPrepareAndHandoffAsync
            (
            ImportHandoffScenario Scenario
            )
        {
            return RunPrepareAndHandoffAsync(Scenario, null, null);
        }

        /// <summary>
        /// Runs progressive prepare and simulates the AGD import handoff contract.
        /// </summary>
        internal static async Task<ImportHandoffRunResult> RunPrepareAndHandoffAsync
            (
            FieldViewerControl Viewer,
            string FieldName = "",
            ImportHandoffScenario? Scenario = null,
            int? ResizeBeforeFirstRenderWidth = null,
            int? ResizeBeforeFirstRenderHeight = null
            )
        {
            ImportViewerHandoffTelemetry.ResetForTests();
            ImportViewerHandoffRecorder Recorder = ImportViewerHandoffTelemetry.Recorder;
            Viewer.TestResetImportInstrumentation();
            ImportViewerHandoff.BeginImport(Recorder, () => Viewer.SuppressSynchronousPrepare = true);

            bool EarlyCallbackInvoked = false;
            Stopwatch HandoffWatch = Stopwatch.StartNew();
            Stopwatch PrepareWatch = Stopwatch.StartNew();
            await Viewer.PrepareViewProgressiveAsync(
                OnEarlyDisplayReady: () =>
                {
                    EarlyCallbackInvoked = true;
                    ImportViewerHandoff.NotifyEarlyPreviewReady(
                        Recorder,
                        () =>
                        {
                            Viewer.MaterializeCachedLayerBitmapsForCurrentView();
                            Viewer.TryPresentCachedFrame();
                        },
                        DismissOverlay: null,
                        Width: Viewer.GetViewportSize().Width,
                        Height: Viewer.GetViewportSize().Height);
                },
                TargetMode: ViewMode.TwoD);
            PrepareWatch.Stop();

            Recorder.Record(ImportViewerHandoffEventKind.PrepareReturned);

            if (ResizeBeforeFirstRenderWidth.HasValue && ResizeBeforeFirstRenderHeight.HasValue)
            {
                Viewer.Width = ResizeBeforeFirstRenderWidth.Value;
                Viewer.Height = ResizeBeforeFirstRenderHeight.Value;
                Viewer.Measure(new Size(ResizeBeforeFirstRenderWidth.Value, ResizeBeforeFirstRenderHeight.Value));
                Viewer.Arrange(new Rect(0, 0, ResizeBeforeFirstRenderWidth.Value, ResizeBeforeFirstRenderHeight.Value));
                Viewer.UpdateLayout();
            }

            Stopwatch EnsureWatch = Stopwatch.StartNew();
            await Viewer.EnsureImportHandoffReadyAsync();
            EnsureWatch.Stop();
            FieldViewerControl.ImportHandoffDiagnosticSnapshot Diagnostics =
                Viewer.TestCollectImportHandoffDiagnostics();
            Assert.True(Viewer.TestCompleteImportHandoff());
            ImportViewerHandoff.NotifyOverlayDismissed(Recorder);
            Viewer.TestSimulatePostHandoffRender();
            Viewer.TestNotifyFirstCompositeDrawAfterHandoff();
            HandoffWatch.Stop();

            ImportHandoffRunResult Result = new ImportHandoffRunResult
            {
                FieldName = FieldName,
                Scenario = Scenario,
                EarlyCallbackInvoked = EarlyCallbackInvoked,
                HasMaterializedComposite = Viewer.HasMaterializedCompositeFrameForCurrentView(),
                PrepareDurationMs = PrepareWatch.ElapsedMilliseconds,
                EnsureDurationMs = EnsureWatch.ElapsedMilliseconds,
                HandoffDurationMs = HandoffWatch.ElapsedMilliseconds,
                Diagnostics = Diagnostics,
                Events = Recorder.Events
            };

            Viewer.TestResetImportInstrumentation();
            Viewer.TestMarkImportHandoffComplete();

            Result.FirstRenderUsedSyncFallback = Viewer.TestSimulatePostHandoffRender();
            Result.FirstRenderObserved = Viewer.TestFirstRenderAfterHandoffObserved;
            Result.SyncFrameBuildCount = Viewer.TestSyncFrameBuildCount;
            Result.SurfaceRebuildCount = Viewer.TestSurfaceRebuildCount;
            Result.DiagnosticsAfterHandoff = Viewer.TestCollectImportHandoffDiagnostics();
            Result.ContractViolations = ImportViewerHandoffRules.GetViolations(Result.Events);
            Result.Events = Recorder.Events;

            return Result;
        }

        /// <summary>
        /// Asserts AGD import prepare plus handoff stayed within the processing budget.
        /// </summary>
        internal static void AssertWithinProcessingBudget
            (
            ImportHandoffRunResult Result,
            string? ComparisonReport = null
            )
        {
            string Prefix = string.IsNullOrEmpty(ComparisonReport)
                ? $"[{Result.FieldName}] "
                : ComparisonReport + Environment.NewLine;

            Assert.True(
                Result.HandoffDurationMs <= MaxAgdProcessingMs,
                Prefix +
                $"AGD import handoff exceeded {MaxAgdProcessingMs} ms budget: {Result.HandoffDurationMs} ms " +
                $"(prepare={Result.PrepareDurationMs} ms, ensure={Result.EnsureDurationMs} ms).");
        }

        /// <summary>
        /// Builds a diagnostic report comparing two handoff runs.
        /// </summary>
        internal static string BuildComparisonReport
            (
            ImportHandoffRunResult Left,
            ImportHandoffRunResult Right
            )
        {
            StringBuilder Builder = new StringBuilder();
            AppendResultSummary(Builder, Left);
            Builder.AppendLine();
            AppendResultSummary(Builder, Right);
            return Builder.ToString();
        }

        static private void AppendResultSummary
            (
            StringBuilder Builder,
            ImportHandoffRunResult Result
            )
        {
            FieldViewerControl.ImportHandoffDiagnosticSnapshot Diagnostics = Result.DiagnosticsAfterHandoff;
            ImportViewerHandoffEvent? PreviewMaterialized = Result.Events
                .FirstOrDefault(Event => Event.Kind == ImportViewerHandoffEventKind.PreviewMaterialized);
            Builder.AppendLine(
                $"[{Result.FieldName}] handoff={Result.HandoffDurationMs} ms prepare={Result.PrepareDurationMs} ms ensure={Result.EnsureDurationMs} ms");
            Builder.AppendLine(
                $"  syncFallback={Result.FirstRenderUsedSyncFallback}, syncBuilds={Result.SyncFrameBuildCount}, surfaceRebuilds={Result.SurfaceRebuildCount}");
            Builder.AppendLine(
                $"  viewport={Diagnostics.ViewportWidth}x{Diagnostics.ViewportHeight}, composite={Diagnostics.HasMaterializedComposite}, cacheMatch={Diagnostics.CacheKeysMatch}");
            Builder.AppendLine(
                $"  layers base/topo/decor bitmap={Diagnostics.HasBaseBitmap}/{Diagnostics.HasTopoBitmap}/{Diagnostics.HasDecorationsBitmap} packet={Diagnostics.HasBasePacket}/{Diagnostics.HasTopoPacket}/{Diagnostics.HasDecorationsPacket}");
            Builder.AppendLine($"  storedKey={Diagnostics.StoredProposedCacheKey}");
            Builder.AppendLine($"  currentKey={Diagnostics.CurrentCacheKey}");
            if (PreviewMaterialized != null)
            {
                Builder.AppendLine(
                    $"  previewMaterialized={PreviewMaterialized.Width}x{PreviewMaterialized.Height} key={PreviewMaterialized.CacheKey}");
            }

            if (Result.ContractViolations.Count > 0)
                Builder.AppendLine($"  violations: {string.Join("; ", Result.ContractViolations)}");
        }
    }

    /// <summary>
    /// Outcome of a headless AGD import handoff harness run.
    /// </summary>
    internal sealed class ImportHandoffRunResult
    {
        /// <summary>Field label for comparison reports.</summary>
        public string FieldName { get; init; } = "";

        /// <summary>Scenario settings used for this run.</summary>
        public ImportHandoffScenario? Scenario { get; init; }

        /// <summary>Whether the early preview callback fired.</summary>
        public bool EarlyCallbackInvoked { get; init; }

        /// <summary>Whether all composite layer bitmaps were materialized before first render.</summary>
        public bool HasMaterializedComposite { get; init; }

        /// <summary>Whether the first render observed after handoff used sync fallback.</summary>
        public bool FirstRenderUsedSyncFallback { get; set; }

        /// <summary>Whether a post-handoff render was recorded.</summary>
        public bool FirstRenderObserved { get; set; }

        /// <summary>Sync frame builds observed during the harness run.</summary>
        public int SyncFrameBuildCount { get; set; }

        /// <summary>Sync surface rebuilds observed during the harness run.</summary>
        public int SurfaceRebuildCount { get; set; }

        /// <summary>Progressive prepare duration in milliseconds.</summary>
        public long PrepareDurationMs { get; set; }

        /// <summary>Live-viewport ensure duration in milliseconds.</summary>
        public long EnsureDurationMs { get; set; }

        /// <summary>Total prepare plus handoff duration in milliseconds.</summary>
        public long HandoffDurationMs { get; set; }

        /// <summary>Diagnostics captured immediately before handoff completion.</summary>
        public FieldViewerControl.ImportHandoffDiagnosticSnapshot Diagnostics { get; set; }

        /// <summary>Diagnostics captured after handoff and simulated first render.</summary>
        public FieldViewerControl.ImportHandoffDiagnosticSnapshot DiagnosticsAfterHandoff { get; set; }

        /// <summary>Contract violations detected in the timeline.</summary>
        public IReadOnlyList<string> ContractViolations { get; set; } = Array.Empty<string>();

        /// <summary>Recorded handoff timeline.</summary>
        public IReadOnlyList<ImportViewerHandoffEvent> Events { get; set; } =
            Array.Empty<ImportViewerHandoffEvent>();
    }
}
