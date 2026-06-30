namespace AgGradeDesigner.Core.Models
{
    /// <summary>
    /// User-controlled visibility toggles for map and 3D viewer overlays.
    /// </summary>
    public sealed class DisplaySettings
    {
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
        public double VerticalExaggeration { get; set; } = 1.0;
    }
}
