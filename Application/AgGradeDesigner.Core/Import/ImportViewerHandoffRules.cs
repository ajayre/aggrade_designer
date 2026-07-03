namespace AgGradeDesigner.Core.Import
{
    /// <summary>
    /// Validates AGD import viewer handoff timelines against the contract that prevents UI freezes.
    /// </summary>
    public static class ImportViewerHandoffRules
    {
        /// <summary>
        /// Validates the timeline and throws <see cref="ImportViewerHandoffViolationException"/> on failure.
        /// </summary>
        public static void Validate
            (
            IReadOnlyList<ImportViewerHandoffEvent> Events
            )
        {
            IReadOnlyList<string> Violations = GetViolations(Events);
            if (Violations.Count > 0)
                throw new ImportViewerHandoffViolationException(Violations);
        }

        /// <summary>
        /// Returns human-readable contract violations for the given timeline.
        /// </summary>
        public static IReadOnlyList<string> GetViolations
            (
            IReadOnlyList<ImportViewerHandoffEvent> Events
            )
        {
            List<string> Violations = new List<string>();
            if (Events.Count == 0)
            {
                Violations.Add("Handoff timeline is empty.");
                return Violations;
            }

            RequireBefore(Events, Violations, ImportViewerHandoffEventKind.SuppressEnabled, ImportViewerHandoffEventKind.SuppressReleased);
            RequireBefore(Events, Violations, ImportViewerHandoffEventKind.PreviewMaterialized, ImportViewerHandoffEventKind.SuppressReleased);
            RequireBefore(Events, Violations, ImportViewerHandoffEventKind.SurfaceMarkedClean, ImportViewerHandoffEventKind.SuppressReleased);
            RequireBefore(Events, Violations, ImportViewerHandoffEventKind.SuppressReleased, ImportViewerHandoffEventKind.OverlayDismissed);

            int MaterializedIndex = FirstIndex(Events, ImportViewerHandoffEventKind.CompositeAlreadyMaterialized);
            int MaterializeRequestedIndex = FirstIndex(Events, ImportViewerHandoffEventKind.CompositeMaterializeRequested);
            int SuppressReleasedIndex = FirstIndex(Events, ImportViewerHandoffEventKind.SuppressReleased);
            if (SuppressReleasedIndex >= 0 &&
                MaterializedIndex < 0 &&
                MaterializeRequestedIndex < 0)
            {
                Violations.Add("SuppressReleased occurred before composite layers were materialized or requested.");
            }

            if (MaterializedIndex >= 0 && SuppressReleasedIndex >= 0 && MaterializedIndex > SuppressReleasedIndex)
                Violations.Add("CompositeAlreadyMaterialized must occur before SuppressReleased.");

            if (MaterializeRequestedIndex >= 0 && SuppressReleasedIndex >= 0 && MaterializeRequestedIndex > SuppressReleasedIndex)
                Violations.Add("CompositeMaterializeRequested must occur before SuppressReleased.");

            int OverlayDismissedIndex = FirstIndex(Events, ImportViewerHandoffEventKind.OverlayDismissed);
            int PreviewMaterializedIndex = FirstIndex(Events, ImportViewerHandoffEventKind.PreviewMaterialized);
            if (OverlayDismissedIndex >= 0 &&
                PreviewMaterializedIndex >= 0 &&
                PreviewMaterializedIndex > OverlayDismissedIndex)
            {
                Violations.Add("PreviewMaterialized must occur before OverlayDismissed.");
            }

            if (Contains(Events, ImportViewerHandoffEventKind.SyncFrameBuildDuringSuppress))
                Violations.Add("Synchronous frame build occurred while SuppressSynchronousPrepare was active.");

            if (Contains(Events, ImportViewerHandoffEventKind.SyncFrameBuildAfterHandoff))
                Violations.Add("First render after handoff fell back to synchronous frame build.");

            return Violations;
        }

        static private void RequireBefore
            (
            IReadOnlyList<ImportViewerHandoffEvent> Events,
            List<string> Violations,
            ImportViewerHandoffEventKind RequiredFirst,
            ImportViewerHandoffEventKind RequiredSecond
            )
        {
            int FirstIndexValue = FirstIndex(Events, RequiredFirst);
            int SecondIndexValue = FirstIndex(Events, RequiredSecond);
            if (SecondIndexValue < 0)
                return;

            if (FirstIndexValue < 0)
            {
                Violations.Add($"{RequiredFirst} must be recorded before {RequiredSecond}.");
                return;
            }

            if (FirstIndexValue > SecondIndexValue)
                Violations.Add($"{RequiredFirst} must occur before {RequiredSecond}.");
        }

        static private int FirstIndex
            (
            IReadOnlyList<ImportViewerHandoffEvent> Events,
            ImportViewerHandoffEventKind Kind
            )
        {
            for (int Index = 0; Index < Events.Count; Index++)
            {
                if (Events[Index].Kind == Kind)
                    return Index;
            }

            return -1;
        }

        static private bool Contains
            (
            IReadOnlyList<ImportViewerHandoffEvent> Events,
            ImportViewerHandoffEventKind Kind
            )
        {
            return FirstIndex(Events, Kind) >= 0;
        }
    }
}
