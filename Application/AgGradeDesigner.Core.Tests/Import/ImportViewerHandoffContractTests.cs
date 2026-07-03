using AgGradeDesigner.Core.Import;

namespace AgGradeDesigner.Core.Tests.Import
{
    /// <summary>
    /// Layer 1 contract tests for AGD import viewer handoff ordering rules.
    /// </summary>
    [Trait("Category", "ImportHandoff")]
    [Trait("Category", "ImportHandoffContract")]
    public sealed class ImportViewerHandoffContractTests
    {
        /// <summary>
        /// A valid import handoff timeline satisfies all contract rules.
        /// </summary>
        [Fact]
        public void ValidTimeline_PassesValidation
            (
            )
        {
            ImportViewerHandoffRecorder Recorder = new ImportViewerHandoffRecorder();
            ImportViewerHandoff.BeginImport(Recorder, () => { });
            Recorder.Record(ImportViewerHandoffEventKind.SurfaceBuildStarted);
            Recorder.Record(ImportViewerHandoffEventKind.PreviewFrameStored, "cache-a", 1280, 720);
            Recorder.Record(ImportViewerHandoffEventKind.PreviewMaterialized, "cache-a", 1280, 720);
            ImportViewerHandoff.NotifyEarlyPreviewReady(Recorder, () => { }, DismissOverlay: null, "cache-a", 1280, 720);
            Recorder.Record(ImportViewerHandoffEventKind.PrepareReturned);
            ImportViewerHandoff.CompleteImport(
                Recorder,
                () => true,
                () => throw new InvalidOperationException("Materialize should be skipped."),
                () => { },
                () => { });
            ImportViewerHandoff.NotifyOverlayDismissed(Recorder);

            ImportViewerHandoffRules.Validate(Recorder.Events);
        }

        /// <summary>
        /// Releasing suppress before preview materialization violates the contract.
        /// </summary>
        [Fact]
        public void SuppressReleasedBeforePreviewMaterialized_FailsValidation
            (
            )
        {
            ImportViewerHandoffRecorder Recorder = new ImportViewerHandoffRecorder();
            ImportViewerHandoff.BeginImport(Recorder, () => { });
            Recorder.Record(ImportViewerHandoffEventKind.SuppressReleased);

            IReadOnlyList<string> Violations = ImportViewerHandoffRules.GetViolations(Recorder.Events);
            Assert.Contains(Violations, Violation => Violation.Contains("PreviewMaterialized"));
        }

        /// <summary>
        /// Releasing suppress before composite materialization violates the contract.
        /// </summary>
        [Fact]
        public void SuppressReleasedBeforeCompositeMaterialized_FailsValidation
            (
            )
        {
            ImportViewerHandoffRecorder Recorder = new ImportViewerHandoffRecorder();
            ImportViewerHandoff.BeginImport(Recorder, () => { });
            Recorder.Record(ImportViewerHandoffEventKind.PreviewMaterialized, "cache-a", 1280, 720);
            Recorder.Record(ImportViewerHandoffEventKind.SuppressReleased);

            IReadOnlyList<string> Violations = ImportViewerHandoffRules.GetViolations(Recorder.Events);
            Assert.Contains(
                Violations,
                Violation => Violation.Contains("CompositeAlreadyMaterialized", StringComparison.Ordinal)
                    || Violation.Contains("CompositeMaterializeRequested", StringComparison.Ordinal)
                    || Violation.Contains("composite layers", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Sync frame build during suppress is flagged as a contract violation.
        /// </summary>
        [Fact]
        public void SyncFrameBuildDuringSuppress_FailsValidation
            (
            )
        {
            ImportViewerHandoffRecorder Recorder = BuildValidTimelineThroughPreview();
            Recorder.Record(ImportViewerHandoffEventKind.SyncFrameBuildDuringSuppress);

            IReadOnlyList<string> Violations = ImportViewerHandoffRules.GetViolations(Recorder.Events);
            Assert.Contains(
                Violations,
                Violation => Violation.Contains("SuppressSynchronousPrepare", StringComparison.Ordinal));
        }

        /// <summary>
        /// Sync frame build on first render after handoff is flagged as a contract violation.
        /// </summary>
        [Fact]
        public void SyncFrameBuildAfterHandoff_FailsValidation
            (
            )
        {
            ImportViewerHandoffRecorder Recorder = BuildValidTimelineThroughPreview();
            ImportViewerHandoff.CompleteImport(
                Recorder,
                () => true,
                () => throw new InvalidOperationException("Materialize should be skipped."),
                () => { },
                () => { });
            Recorder.Record(ImportViewerHandoffEventKind.SyncFrameBuildAfterHandoff);

            IReadOnlyList<string> Violations = ImportViewerHandoffRules.GetViolations(Recorder.Events);
            Assert.Contains(
                Violations,
                Violation => Violation.Contains("synchronous frame build", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Overlay dismissal before preview materialization violates the contract.
        /// </summary>
        [Fact]
        public void OverlayDismissedBeforePreviewMaterialized_FailsValidation
            (
            )
        {
            ImportViewerHandoffRecorder Recorder = new ImportViewerHandoffRecorder();
            ImportViewerHandoff.BeginImport(Recorder, () => { });
            Recorder.Record(ImportViewerHandoffEventKind.OverlayDismissed);
            Recorder.Record(ImportViewerHandoffEventKind.PreviewMaterialized);

            IReadOnlyList<string> Violations = ImportViewerHandoffRules.GetViolations(Recorder.Events);
            Assert.Contains(
                Violations,
                Violation => Violation.Contains("PreviewMaterialized must occur before OverlayDismissed"));
        }

        /// <summary>
        /// CompleteImport records the expected milestones when composite layers are missing.
        /// </summary>
        [Fact]
        public void CompleteImport_WhenCompositeMissing_RecordsMaterializeRequest
            (
            )
        {
            ImportViewerHandoffRecorder Recorder = BuildValidTimelineThroughPreview();
            bool Materialized = false;
            bool HandoffComplete = ImportViewerHandoff.CompleteImport(
                Recorder,
                () => false,
                () =>
                {
                    Materialized = true;
                    return false;
                },
                () => { },
                () => { });

            Assert.True(Materialized);
            Assert.False(HandoffComplete);
            Assert.True(Recorder.Contains(ImportViewerHandoffEventKind.CompositeMaterializeRequested));
            Assert.True(Recorder.Contains(ImportViewerHandoffEventKind.CompositeMaterializationIncomplete));
            Assert.False(Recorder.Contains(ImportViewerHandoffEventKind.SuppressReleased));
            ImportViewerHandoffRules.Validate(Recorder.Events);
        }

        static private ImportViewerHandoffRecorder BuildValidTimelineThroughPreview
            (
            )
        {
            ImportViewerHandoffRecorder Recorder = new ImportViewerHandoffRecorder();
            ImportViewerHandoff.BeginImport(Recorder, () => { });
            Recorder.Record(ImportViewerHandoffEventKind.PreviewMaterialized, "cache-a", 1280, 720);
            ImportViewerHandoff.NotifyEarlyPreviewReady(Recorder, () => { }, DismissOverlay: null, "cache-a", 1280, 720);
            Recorder.Record(ImportViewerHandoffEventKind.PrepareReturned);
            return Recorder;
        }
    }
}
