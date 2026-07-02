namespace AgGradeDesigner.Core.Models
{
    /// <summary>
    /// User-controlled visibility toggles for map and 3D viewer overlays.
    /// </summary>
    public sealed class DisplaySettings
    {
        /// <summary>Minimum topo/cut-fill overlay opacity percentage.</summary>
        public const double OPACITY_PERCENT_MIN = 0.0;

        /// <summary>Maximum topo/cut-fill overlay opacity percentage.</summary>
        public const double OPACITY_PERCENT_MAX = 100.0;

        /// <summary>Default vertical exaggeration multiplier for 3D terrain display.</summary>
        public const double DEFAULT_VERTICAL_EXAGGERATION = 1.0;
        /// <summary>Draw survey point markers.</summary>
        public bool SurveyPoints { get; set; } = true;

        /// <summary>Draw elevation contour lines.</summary>
        public bool Contours { get; set; } = true;

        /// <summary>Draw hillshade relief under the colour map.</summary>
        public bool Hillshade { get; set; } = true;

        /// <summary>Draw benchmark markers and labels.</summary>
        public bool Benchmarks { get; set; } = true;

        /// <summary>Reserved; proposed surface is not drawn over existing/cut-fill views.</summary>
        public bool DesignOverlay { get; set; }

        /// <summary>Draw satellite imagery basemap.</summary>
        public bool Satellite { get; set; }

        /// <summary>Vertical exaggeration multiplier for 3D terrain display.</summary>
        public double VerticalExaggeration { get; set; } = DEFAULT_VERTICAL_EXAGGERATION;

        /// <summary>Opacity percentage (0–100) for the active topo or cut/fill colour surface.</summary>
        public double TopoCutFillOpacityPercent { get; set; } = OPACITY_PERCENT_MAX;
    }
}
