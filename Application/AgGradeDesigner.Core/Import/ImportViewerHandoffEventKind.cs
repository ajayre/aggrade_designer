namespace AgGradeDesigner.Core.Import
{
    /// <summary>
    /// Ordered milestones recorded during AGD import viewer preparation and handoff.
    /// </summary>
    public enum ImportViewerHandoffEventKind
    {
        /// <summary>Viewer busy overlay shown.</summary>
        OverlayShown,
        /// <summary>Synchronous render fallback disabled on the viewer.</summary>
        SuppressEnabled,
        /// <summary>Surface build started on a worker thread.</summary>
        SurfaceBuildStarted,
        /// <summary>Preview frame pixels stored in the cache.</summary>
        PreviewFrameStored,
        /// <summary>Preview layer bitmaps materialized on the UI thread.</summary>
        PreviewMaterialized,
        /// <summary>Early present requested when preview is ready.</summary>
        EarlyPresentRequested,
        /// <summary>Viewer busy overlay dismissed before import handoff completes.</summary>
        OverlayDismissed,
        /// <summary>Progressive prepare returned to the caller.</summary>
        PrepareReturned,
        /// <summary>Composite layer materialization requested during handoff completion.</summary>
        CompositeMaterializeRequested,
        /// <summary>Composite layers were already materialized when handoff completed.</summary>
        CompositeAlreadyMaterialized,
        /// <summary>Composite layers were still not materialized after a present attempt.</summary>
        CompositeMaterializationIncomplete,
        /// <summary>Viewer surface marked clean after import prepare.</summary>
        SurfaceMarkedClean,
        /// <summary>Synchronous render fallback re-enabled on the viewer.</summary>
        SuppressReleased,
        /// <summary>Synchronous frame build observed while suppress was active.</summary>
        SyncFrameBuildDuringSuppress,
        /// <summary>Synchronous frame build observed on first render after handoff.</summary>
        SyncFrameBuildAfterHandoff,
        /// <summary>First render completed after import handoff.</summary>
        FirstRenderAfterHandoff,
        /// <summary>Import viewer handoff completed.</summary>
        Complete
    }
}
