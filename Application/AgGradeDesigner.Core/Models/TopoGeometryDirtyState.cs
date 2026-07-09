namespace AgGradeDesigner.Core.Models
{
    /// <summary>
    /// Tracks whether existing, proposed, or haul elevation geometry needs a GPU mesh rebuild.
    /// </summary>
    public sealed class TopoGeometryDirtyState
    {
        /// <summary>True when existing-surface mesh geometry must be rebuilt.</summary>
        public bool ExistingIsDirty { get; set; } = true;

        /// <summary>True when proposed-surface mesh geometry must be rebuilt.</summary>
        public bool ProposedIsDirty { get; set; } = true;

        /// <summary>True when haul-leveled 3D mesh geometry must be rebuilt.</summary>
        public bool HaulIsDirty { get; set; } = true;

        /// <summary>Marks existing, proposed, and haul geometry stale.</summary>
        public void MarkAllDirty
            (
            )
        {
            ExistingIsDirty = true;
            ProposedIsDirty = true;
            HaulIsDirty = true;
        }

        /// <summary>Marks existing-surface geometry stale.</summary>
        public void MarkExistingDirty
            (
            )
        {
            ExistingIsDirty = true;
        }

        /// <summary>Marks proposed-surface geometry stale.</summary>
        public void MarkProposedDirty
            (
            )
        {
            ProposedIsDirty = true;
        }

        /// <summary>Marks haul-leveled 3D geometry stale.</summary>
        public void MarkHaulDirty
            (
            )
        {
            HaulIsDirty = true;
        }

        /// <summary>
        /// Returns true when the supplied content view requires a geometry rebuild.
        /// </summary>
        public bool IsDirtyForContentView
            (
            FieldContentView View
            )
        {
            return View switch
            {
                FieldContentView.Existing => ExistingIsDirty,
                FieldContentView.Proposed => ProposedIsDirty,
                FieldContentView.CutFill => ExistingIsDirty || ProposedIsDirty,
                FieldContentView.Haul => HaulIsDirty,
                _ => true
            };
        }

        /// <summary>
        /// Clears dirty flags for the content view whose GPU geometry was just built.
        /// </summary>
        public void ClearAfterBuild
            (
            FieldContentView View
            )
        {
            switch (View)
            {
                case FieldContentView.Existing:
                    ExistingIsDirty = false;
                    break;
                case FieldContentView.Proposed:
                    ProposedIsDirty = false;
                    break;
                case FieldContentView.CutFill:
                    ExistingIsDirty = false;
                    ProposedIsDirty = false;
                    break;
                case FieldContentView.Haul:
                    HaulIsDirty = false;
                    break;
            }
        }

        /// <summary>Resets dirty flags when the project has no loaded geometry.</summary>
        public void ResetForEmptyProject
            (
            )
        {
            ExistingIsDirty = true;
            ProposedIsDirty = true;
            HaulIsDirty = true;
        }
    }
}
