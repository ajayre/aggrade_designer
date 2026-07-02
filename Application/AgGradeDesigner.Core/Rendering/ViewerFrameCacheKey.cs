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
    }

    /// <summary>
    /// Builds stable cache keys and plans viewer preparation work.
    /// </summary>
    static public class ViewerFrameCacheKey
    {
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
        static public string Build
            (
            ViewerFrameState State
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
                Display.DesignOverlay,
                Display.VerticalExaggeration.ToString("G4"));

            if (State.ViewMode == ViewMode.TwoD)
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
                    State.ProjectKey);
            }

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
                State.ProjectKey);
        }
    }
}
