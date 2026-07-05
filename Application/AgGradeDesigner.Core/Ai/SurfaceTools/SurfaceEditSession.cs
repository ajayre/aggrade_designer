using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Lifecycle events raised when preview state changes.
    /// </summary>
    public enum SurfaceEditLifecycleKind
    {
        /// <summary>A new preview was applied to the live grid.</summary>
        PreviewUpdated = 1,

        /// <summary>The pending preview was committed to the project.</summary>
        Committed = 2,

        /// <summary>The pending preview was discarded and the live grid restored.</summary>
        Discarded = 3
    }

    /// <summary>
    /// Manages the in-memory preview grid and committed proposed snapshot for Phase 2 edits.
    /// </summary>
    public sealed class SurfaceEditSession
    {
        /// <summary>Preview grid containing candidate proposed elevations.</summary>
        private DesignGrid? _PreviewGrid;

        /// <summary>Committed proposed elevations captured when preview began.</summary>
        private Dictionary<(int X, int Y), double>? _CommittedProposedSnapshot;

        /// <summary>Last mutating operation that produced the active preview.</summary>
        private string _LastOperation = string.Empty;

        /// <summary>Optional human-readable description for commit reporting.</summary>
        private string? _PendingDescription;

        /// <summary>
        /// True when a preview is active.
        /// </summary>
        public bool PreviewActive => _PreviewGrid != null;

        /// <summary>
        /// Last mutating operation that produced the active preview.
        /// </summary>
        public string LastOperation => _LastOperation;

        /// <summary>
        /// Begins or replaces a preview session from the committed project grid.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Operation">Mutating tool name producing the preview.</param>
        public void BeginPreviewFromProject
            (
            ProjectDocument Project,
            string Operation
            )
        {
            if (Project.DesignGrid == null)
                throw new InvalidOperationException("No design grid is loaded.");

            // intent: a new mutating request replaces any pending preview without committing
            if (PreviewActive)
                RestoreCommittedProposed(Project.DesignGrid);

            _CommittedProposedSnapshot = DesignGridCloner.CaptureProposedElevations(Project.DesignGrid);
            _PreviewGrid = DesignGridCloner.Clone(Project.DesignGrid);
            _LastOperation = Operation;
            _PendingDescription = null;
            SurfaceToolDiagnostics.LogPreviewSession("BeginPreview", Operation, Project, _PreviewGrid);
        }

        /// <summary>
        /// Returns the active preview grid.
        /// </summary>
        /// <returns>Preview grid instance.</returns>
        public DesignGrid RequirePreviewGrid
            (
            )
        {
            if (_PreviewGrid == null)
                throw new InvalidOperationException("No preview is active.");

            return _PreviewGrid;
        }

        /// <summary>
        /// Applies the preview proposed elevations to the live project grid for viewer display.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        public void ApplyPreviewToLiveGrid
            (
            ProjectDocument Project
            )
        {
            if (_PreviewGrid == null || Project.DesignGrid == null)
                return;

            DesignGrid LiveGrid = Project.DesignGrid;
            DesignGridCloner.CopyProposedElevations(LiveGrid, _PreviewGrid);
            Project.TopoGeometryDirty.MarkProposedDirty();
            SurfaceToolDiagnostics.LogPreviewSession(
                "ApplyPreview",
                _LastOperation,
                Project,
                _PreviewGrid);
        }

        /// <summary>
        /// Restores the live grid proposed elevations from the committed snapshot.
        /// </summary>
        /// <param name="LiveGrid">Committed project design grid.</param>
        public void RestoreCommittedProposed
            (
            DesignGrid LiveGrid
            )
        {
            if (_CommittedProposedSnapshot == null)
                return;

            DesignGridCloner.RestoreProposedElevations(LiveGrid, _CommittedProposedSnapshot);
        }

        /// <summary>
        /// Commits the active preview into the live project grid and clears preview state.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        /// <param name="Description">Optional commit description.</param>
        /// <returns>Cut/fill volumes after commit.</returns>
        public SurfaceCutFillVolumes Commit
            (
            ProjectDocument Project,
            string? Description
            )
        {
            if (Project.DesignGrid == null || _PreviewGrid == null || _CommittedProposedSnapshot == null)
                throw new InvalidOperationException("No preview is active.");

            DesignGridCloner.CopyProposedElevations(Project.DesignGrid, _PreviewGrid);
            SurfaceCutFillVolumes Volumes = SurfaceVolumeCalculator.CalculateCutFill(
                Project.DesignGrid,
                SurfaceLayerKind.Existing,
                SurfaceLayerKind.Proposed);

            Project.MarkModified();
            Project.TopoGeometryDirty.MarkProposedDirty();
            Clear();
            return Volumes;
        }

        /// <summary>
        /// Discards the active preview and restores the committed proposed snapshot on the live grid.
        /// </summary>
        /// <param name="Project">Active project document.</param>
        public void Discard
            (
            ProjectDocument Project
            )
        {
            if (Project.DesignGrid == null || !PreviewActive)
                return;

            RestoreCommittedProposed(Project.DesignGrid);
            Project.TopoGeometryDirty.MarkProposedDirty();
            Clear();
        }

        /// <summary>
        /// Stores an optional description echoed on commit.
        /// </summary>
        /// <param name="Description">Commit description text.</param>
        public void SetPendingDescription
            (
            string? Description
            )
        {
            _PendingDescription = Description;
        }

        /// <summary>
        /// Returns the pending commit description when set.
        /// </summary>
        /// <returns>Description text or an empty string.</returns>
        public string ResolveCommitDescription
            (
            )
        {
            return string.IsNullOrWhiteSpace(_PendingDescription) ? string.Empty : _PendingDescription!;
        }

        /// <summary>
        /// Builds a temporary grid representing the committed proposed surface before preview.
        /// </summary>
        /// <param name="LiveGrid">Live project design grid.</param>
        /// <returns>Grid clone with committed proposed elevations.</returns>
        public DesignGrid BuildCommittedProposedGrid
            (
            DesignGrid LiveGrid
            )
        {
            DesignGrid Grid = DesignGridCloner.Clone(LiveGrid);
            if (_CommittedProposedSnapshot != null)
                DesignGridCloner.RestoreProposedElevations(Grid, _CommittedProposedSnapshot);

            return Grid;
        }

        /// <summary>
        /// Clears preview state without touching the live grid.
        /// </summary>
        public void Clear
            (
            )
        {
            _PreviewGrid = null;
            _CommittedProposedSnapshot = null;
            _LastOperation = string.Empty;
            _PendingDescription = null;
        }
    }
}
