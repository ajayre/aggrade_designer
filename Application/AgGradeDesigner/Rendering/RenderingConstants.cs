namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Shared rendering constants used across multiple field-view renderers.
    /// </summary>
    internal static class RenderingConstants
    {
        /// <summary>Sample grid dimension above which mesh builders use a coarser step for performance.</summary>
        internal const int MESH_STEP_THRESHOLD = 65536;

        /// <summary>Mesh sampling step for progressive 3D preview entry (coarser than default auto step).</summary>
        internal const int PROGRESSIVE_PREVIEW_MESH_SAMPLING_STEP = 4;

        /// <summary>Sentinel value meaning mesh builders should choose step from grid size.</summary>
        internal const int MESH_SAMPLING_STEP_AUTO = 0;
        /// <summary>Fraction of elevation range used to lift contour lines above the surface mesh.</summary>
        internal const double CONTOUR_Z_LIFT_FRACTION = 0.0005;
        /// <summary>Fraction of elevation range offset applied when lightening contour band colours.</summary>
        internal const double CONTOUR_BAND_OFFSET_FRACTION = 0.002;
        /// <summary>Subdivision count per satellite tile edge for curved ground-plane quads.</summary>
        internal const int TILE_SUBDIVISIONS = 4;
        /// <summary>Exact metres in one international mile (satellite plinth half-span).</summary>
        internal const double METRES_PER_MILE = 1609.344;
        /// <summary>Minimum allowed orbit camera distance from the field centre in metres.</summary>
        internal const double MIN_ORBIT_DISTANCE_M = 50.0;
        /// <summary>Maximum allowed orbit camera distance from the field centre in metres.</summary>
        internal const double MAX_ORBIT_DISTANCE_M = 5000.0;
        /// <summary>Screen margin in pixels between overlay widgets and the viewport edge.</summary>
        internal const float OVERLAY_WIDGET_MARGIN = 14f;
        /// <summary>Ground span in metres used when estimating metres-per-pixel at the field centre.</summary>
        internal const double GROUND_SAMPLE_SPAN_M = 100.0;
        /// <summary>Maximum orbit pitch in degrees (near-horizontal view).</summary>
        internal const double MAX_ORBIT_PITCH_DEG = 89.0;
        /// <summary>Zoom multiplier applied when selecting satellite tiles for the 3D plinth.</summary>
        internal const double PLINTH_TILE_ZOOM_FACTOR = 1.35;
        /// <summary>Red channel of the satellite ground-plane clear colour.</summary>
        internal const byte SATELLITE_GROUND_BACKGROUND_RED = 212;
        /// <summary>Green channel of the satellite ground-plane clear colour.</summary>
        internal const byte SATELLITE_GROUND_BACKGROUND_GREEN = 214;
        /// <summary>Blue channel of the satellite ground-plane clear colour.</summary>
        internal const byte SATELLITE_GROUND_BACKGROUND_BLUE = 218;
        /// <summary>Red channel of the 2D plan-view basemap clear colour (olive green).</summary>
        internal const byte SATELLITE_2D_BACKGROUND_RED = 118;
        /// <summary>Green channel of the 2D plan-view basemap clear colour (olive green).</summary>
        internal const byte SATELLITE_2D_BACKGROUND_GREEN = 132;
        /// <summary>Blue channel of the 2D plan-view basemap clear colour (olive green).</summary>
        internal const byte SATELLITE_2D_BACKGROUND_BLUE = 72;
    }
}
