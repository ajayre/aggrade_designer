namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Serializable haul transport flow field and minimal grid metadata for project persistence.
    /// </summary>
    public sealed class HaulFlowFieldDto
    {
        /// <summary>Flow cache format version.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Grid cell size in metres.</summary>
        public double GridSizeM { get; set; }

        /// <summary>Grid width in bins.</summary>
        public int Width { get; set; }

        /// <summary>Grid height in bins.</summary>
        public int Height { get; set; }

        /// <summary>Minimum easting of the haul grid in metres.</summary>
        public double MinX { get; set; }

        /// <summary>Minimum northing of the haul grid in metres.</summary>
        public double MinY { get; set; }

        /// <summary>UTM zone used for geographic conversion.</summary>
        public int Zone { get; set; }

        /// <summary>True when the haul grid lies in the northern hemisphere.</summary>
        public bool IsNorthern { get; set; }

        /// <summary>Mean latitude of the haul grid in decimal degrees.</summary>
        public double MeanLat { get; set; }

        /// <summary>Mean longitude of the haul grid in decimal degrees.</summary>
        public double MeanLon { get; set; }

        /// <summary>Row-major valid-cell mask (<c>[by * Width + bx]</c>).</summary>
        public bool[] Valid { get; set; } = Array.Empty<bool>();

        /// <summary>Row-major unit east flow component.</summary>
        public double[] Fx { get; set; } = Array.Empty<double>();

        /// <summary>Row-major unit north flow component.</summary>
        public double[] Fy { get; set; } = Array.Empty<double>();

        /// <summary>Row-major raw flow magnitude.</summary>
        public double[] Magnitude { get; set; } = Array.Empty<double>();

        /// <summary>Row-major arrow display mask.</summary>
        public bool[] HasArrow { get; set; } = Array.Empty<bool>();

        /// <summary>Transport mode used when the flow field was computed.</summary>
        public string Mode { get; set; } = "poisson";
    }
}
