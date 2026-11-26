using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Fingerprint for cached 2D/3D viewer frames.
    /// </summary>
    public readonly struct ViewerFrameState
    {
        /// <summary>Active 2D plan or 3D orbit view mode.</summary>
        public ViewMode ViewMode { get; init; }

        /// <summary>Viewport width in pixels.</summary>
        public int Width { get; init; }

        /// <summary>Viewport height in pixels.</summary>
        public int Height { get; init; }

        /// <summary>Scalar channel rendered in the field viewer.</summary>
        public FieldContentView ContentView { get; init; }

        /// <summary>Overlay and exaggeration display toggles.</summary>
        public DisplaySettings Display { get; init; }

        /// <summary>Stable project identity string for cache invalidation.</summary>
        public string ProjectKey { get; init; }

        /// <summary>2D map centre latitude in decimal degrees.</summary>
        public double CentreLat { get; init; }

        /// <summary>2D map centre longitude in decimal degrees.</summary>
        public double CentreLon { get; init; }

        /// <summary>2D zoom multiplier applied to the base metres-per-pixel estimate.</summary>
        public double ZoomFactor { get; init; }

        /// <summary>3D orbit camera heading in degrees.</summary>
        public double OrbitHeadingDeg { get; init; }

        /// <summary>3D orbit camera pitch in degrees.</summary>
        public double OrbitPitchDeg { get; init; }

        /// <summary>3D orbit camera distance from the field centre in metres.</summary>
        public double OrbitDistanceM { get; init; }

        /// <summary>3D orbit pivot latitude in decimal degrees.</summary>
        public double OrbitCentreLat { get; init; }

        /// <summary>3D orbit pivot longitude in decimal degrees.</summary>
        public double OrbitCentreLon { get; init; }

        /// <summary>Pinned haul route scrub index for haul 3D frames, or -1 when not applicable.</summary>
        public int HaulRouteScrubIndex { get; init; }
    }

    /// <summary>
    /// Builds stable cache keys and plans viewer preparation work.
    /// </summary>
    static public class ViewerFrameCacheKey
    {
        /// <summary>
        /// Bumped when cached frame contents change incompatibly (tool data is drawn as a live overlay, not baked).
        /// </summary>
        private const int CACHE_SCHEMA_VERSION = 3;

        /// <summary>
        /// Returns true when the elevation mesh must be rebuilt before rendering.
        /// </summary>
        static public bool NeedsSurfaceRebuild
            (
            bool SurfaceDirty,
            int SampleWidth
            )
        {
            return SurfaceDirty || (SampleWidth <= 0);
        }

        /// <summary>
        /// Builds a cache fingerprint for the supplied viewer frame state.
        /// </summary>
        /// <param name="State">Camera, display, and viewport inputs.</param>
        /// <returns>Fingerprint that changes when the picture must be rendered again.</returns>
        static public string Build
            (
            ViewerFrameState State
            )
        {
            return Format(State, IncludePixelSize: true);
        }

        /// <summary>
        /// Builds a fingerprint of the field view without the viewport pixel size.
        /// </summary>
        /// <param name="State">Camera, display, and viewport inputs.</param>
        /// <returns>Fingerprint that stays the same when only the window size changes.</returns>
        static public string BuildViewIdentity
            (
            ViewerFrameState State
            )
        {
            // Wells, roads, and a shorter viewport do not change the survey or design surface.
            return Format(State, IncludePixelSize: false);
        }

        /// <summary>
        /// Formats a cache fingerprint, optionally including the viewport pixel size.
        /// </summary>
        /// <param name="State">Camera, display, and viewport inputs.</param>
        /// <param name="IncludePixelSize">True when the key must miss after a resize.</param>
        /// <returns>Fingerprint string.</returns>
        static private string Format
            (
            ViewerFrameState State,
            bool IncludePixelSize
            )
        {
            DisplaySettings Display = State.Display;
            string DisplayKey = String.Join(
                "|",
                Display.Satellite,
                Display.Contours,
                Display.Hillshade,
                Display.SurveyPoints,
                Display.Benchmarks,
                Display.HaulFlow,
                Display.Water,
                Display.DesignOverlay,
                Display.VerticalExaggeration.ToString("G4"),
                Display.TopoCutFillOpacityPercent.ToString("G4"));

            string HaulKey = State.HaulRouteScrubIndex >= 0
                ? State.HaulRouteScrubIndex.ToString()
                : "";

            if (State.ViewMode == ViewMode.TwoD)
            {
                if (IncludePixelSize)
                {
                    return String.Join(
                        "|",
                        "2D",
                        State.Width,
                        State.Height,
                        State.CentreLat.ToString("G8"),
                        State.CentreLon.ToString("G8"),
                        State.ZoomFactor.ToString("G6"),
                        State.ContentView,
                        DisplayKey,
                        State.ProjectKey,
                        HaulKey,
                        CACHE_SCHEMA_VERSION);
                }

                return String.Join(
                    "|",
                    "2D",
                    State.CentreLat.ToString("G8"),
                    State.CentreLon.ToString("G8"),
                    State.ZoomFactor.ToString("G6"),
                    State.ContentView,
                    DisplayKey,
                    State.ProjectKey,
                    HaulKey,
                    CACHE_SCHEMA_VERSION);
            }

            if (IncludePixelSize)
            {
                return String.Join(
                    "|",
                    "3D",
                    State.Width,
                    State.Height,
                    State.OrbitHeadingDeg.ToString("G6"),
                    State.OrbitPitchDeg.ToString("G6"),
                    State.OrbitDistanceM.ToString("G6"),
                    State.OrbitCentreLat.ToString("G8"),
                    State.OrbitCentreLon.ToString("G8"),
                    State.ContentView,
                    DisplayKey,
                    State.ProjectKey,
                    HaulKey,
                    CACHE_SCHEMA_VERSION);
            }

            return String.Join(
                "|",
                "3D",
                State.OrbitHeadingDeg.ToString("G6"),
                State.OrbitPitchDeg.ToString("G6"),
                State.OrbitDistanceM.ToString("G6"),
                State.OrbitCentreLat.ToString("G8"),
                State.OrbitCentreLon.ToString("G8"),
                State.ContentView,
                DisplayKey,
                State.ProjectKey,
                HaulKey,
                CACHE_SCHEMA_VERSION);
        }
    }
}
