using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Discards water layer caches when basis surfaces or haul data change (WATER-SPEC §6.3).
    /// </summary>
    public static class WaterLayerInvalidation
    {
        /// <summary>
        /// Discards the existing-surface water cache after survey or existing topology changes.
        /// </summary>
        /// <param name="Project">Open project.</param>
        public static void DiscardExistingSurfaceCache
            (
            ProjectDocument Project
            )
        {
            if (Project.Water.ExistingSurfaceCache == null)
                return;

            Project.Water.ExistingSurfaceCache = null;
            Project.MarkModified();
        }

        /// <summary>
        /// Discards the proposed-surface water cache after design replacement or proposed topology changes.
        /// </summary>
        /// <param name="Project">Open project.</param>
        public static void DiscardProposedSurfaceCache
            (
            ProjectDocument Project
            )
        {
            if (Project.Water.ProposedSurfaceCache == null)
                return;

            Project.Water.ProposedSurfaceCache = null;
            Project.MarkModified();
        }

        /// <summary>
        /// Discards all haul cut/fill water caches when haul data is replaced or cleared.
        /// </summary>
        /// <param name="Project">Open project.</param>
        public static void DiscardHaulCaches
            (
            ProjectDocument Project
            )
        {
            WaterSessionCache.DiscardAllHaulCaches();
            WaterSessionCache.BindHaulData(Project.HaulData);
        }

        /// <summary>
        /// Discards all water caches when resetting a project to empty.
        /// </summary>
        /// <param name="Project">Open project.</param>
        public static void ResetForEmptyProject
            (
            ProjectDocument Project
            )
        {
            Project.Water = new WaterLayerState();
            WaterSessionCache.DiscardAllHaulCaches();
        }
    }
}
