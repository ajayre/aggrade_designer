namespace AgGradeDesigner.Core.Models
{
    /// <summary>
    /// North-up 2D map camera centre and zoom stored in the project viewer state.
    /// </summary>
    public sealed class Camera2DState
    {
        /// <summary>Default map centre latitude when no camera state is persisted.</summary>
        public const double DEFAULT_CENTRE_LAT = 36.0;

        /// <summary>Default map centre longitude when no camera state is persisted.</summary>
        public const double DEFAULT_CENTRE_LON = -90.0;

        /// <summary>Default zoom multiplier relative to the base metres-per-pixel estimate.</summary>
        public const double DEFAULT_ZOOM = 1.0;

        /// <summary>Map centre latitude in decimal degrees.</summary>
        public double CentreLat { get; set; } = DEFAULT_CENTRE_LAT;

        /// <summary>Map centre longitude in decimal degrees.</summary>
        public double CentreLon { get; set; } = DEFAULT_CENTRE_LON;

        /// <summary>Zoom multiplier relative to the default metres-per-pixel estimate.</summary>
        public double Zoom { get; set; } = DEFAULT_ZOOM;
    }

    /// <summary>
    /// Orbit camera parameters for the 3D terrain viewer.
    /// </summary>
    public sealed class Camera3DState
    {
        /// <summary>Default orbit heading in degrees clockwise from north.</summary>
        public const double DEFAULT_ORBIT_HEADING_DEG = 45.0;

        /// <summary>Default orbit pitch in degrees above the horizon.</summary>
        public const double DEFAULT_ORBIT_PITCH_DEG = 45.0;

        /// <summary>Default camera distance from the orbit pivot in metres.</summary>
        public const double DEFAULT_DISTANCE_M = 500.0;

        /// <summary>Orbit heading in degrees clockwise from north.</summary>
        /// <summary>Orbit heading in degrees clockwise from north (45° = north-east oblique).</summary>
        public double OrbitHeadingDeg { get; set; } = DEFAULT_ORBIT_HEADING_DEG;

        /// <summary>Orbit pitch in degrees above the horizon.</summary>
        public double OrbitPitchDeg { get; set; } = DEFAULT_ORBIT_PITCH_DEG;

        /// <summary>Camera distance from the orbit pivot in metres.</summary>
        public double DistanceM { get; set; } = DEFAULT_DISTANCE_M;

        /// <summary>Orbit pivot latitude; NaN uses the field geometric centre.</summary>
        public double OrbitCentreLat { get; set; } = double.NaN;

        /// <summary>Orbit pivot longitude; NaN uses the field geometric centre.</summary>
        public double OrbitCentreLon { get; set; } = double.NaN;
    }

    /// <summary>
    /// Persisted viewer mode, overlay toggles, and camera settings for a project document.
    /// </summary>
    public sealed class ViewerState
    {
        /// <summary>Primary 2D versus 3D presentation mode.</summary>
        public ViewMode ViewMode { get; set; } = ViewMode.TwoD;

        /// <summary>Which elevation surface is shown on the field overlay.</summary>
        public FieldContentView FieldContentView { get; set; } = FieldContentView.None;

        /// <summary>Layer visibility toggles for survey and design overlays.</summary>
        public DisplaySettings Display { get; set; } = new DisplaySettings();

        /// <summary>North-up plan map camera state.</summary>
        public Camera2DState Camera2D { get; set; } = new Camera2DState();

        /// <summary>Orbit camera state for 3D viewing.</summary>
        public Camera3DState Camera3D { get; set; } = new Camera3DState();
    }
}
