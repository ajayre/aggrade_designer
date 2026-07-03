namespace AgGradeDesigner.Core.Import

{

    /// <summary>

    /// Shared AGD import viewer handoff orchestration used by the app and regression tests.

    /// </summary>

    public static class ImportViewerHandoff

    {

        /// <summary>

        /// Records import start and enables suppressed synchronous rendering on the viewer.

        /// </summary>

        public static void BeginImport

            (

            ImportViewerHandoffRecorder Recorder,

            Action EnterSuppress

            )

        {

            Recorder.Record(ImportViewerHandoffEventKind.OverlayShown);

            Recorder.Record(ImportViewerHandoffEventKind.SuppressEnabled);

            EnterSuppress();

        }



        /// <summary>

        /// Records early preview presentation during progressive prepare.

        /// </summary>

        /// <param name="DismissOverlay">When non-null, invoked and overlay dismissal is recorded.</param>

        public static void NotifyEarlyPreviewReady

            (

            ImportViewerHandoffRecorder Recorder,

            Action PresentPreview,

            Action? DismissOverlay,

            string? CacheKey = null,

            int? Width = null,

            int? Height = null

            )

        {

            PresentPreview();

            Recorder.Record(

                ImportViewerHandoffEventKind.EarlyPresentRequested,

                CacheKey,

                Width,

                Height);

            if (DismissOverlay == null)

                return;

            DismissOverlay();

            Recorder.Record(ImportViewerHandoffEventKind.OverlayDismissed, CacheKey, Width, Height);

        }



        /// <summary>

        /// Records that the viewer busy overlay was dismissed after import handoff completed.

        /// </summary>

        public static void NotifyOverlayDismissed

            (

            ImportViewerHandoffRecorder Recorder,

            string? CacheKey = null,

            int? Width = null,

            int? Height = null

            )

        {

            Recorder.Record(ImportViewerHandoffEventKind.OverlayDismissed, CacheKey, Width, Height);

        }



        /// <summary>

        /// Completes import handoff: materialize composite bitmaps at the live view, then release suppress.

        /// </summary>

        /// <returns>True when composite layers are materialized and suppress was released.</returns>

        public static bool CompleteImport

            (

            ImportViewerHandoffRecorder Recorder,

            Func<bool> HasMaterializedComposite,

            Func<bool> MaterializeComposite,

            Action MarkSurfaceClean,

            Action ReleaseSuppress

            )

        {

            if (HasMaterializedComposite())

            {

                Recorder.Record(ImportViewerHandoffEventKind.CompositeAlreadyMaterialized);

            }

            else

            {

                MaterializeComposite();

                Recorder.Record(ImportViewerHandoffEventKind.CompositeMaterializeRequested);

            }



            if (!HasMaterializedComposite())

            {

                Recorder.Record(ImportViewerHandoffEventKind.CompositeMaterializationIncomplete);

                return false;

            }



            MarkSurfaceClean();

            Recorder.Record(ImportViewerHandoffEventKind.SurfaceMarkedClean);

            ReleaseSuppress();

            Recorder.Record(ImportViewerHandoffEventKind.SuppressReleased);

            Recorder.Record(ImportViewerHandoffEventKind.Complete);

            return true;

        }

    }

}


