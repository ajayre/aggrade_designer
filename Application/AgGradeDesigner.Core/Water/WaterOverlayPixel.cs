namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// One premultiplied-ready RGBA pixel for the ponding overlay.
    /// </summary>
    public readonly struct WaterOverlayPixel
    {
        /// <summary>Red channel (0–255).</summary>
        public byte R { get; init; }

        /// <summary>Green channel (0–255).</summary>
        public byte G { get; init; }

        /// <summary>Blue channel (0–255).</summary>
        public byte B { get; init; }

        /// <summary>Alpha channel (0–255).</summary>
        public byte A { get; init; }

        /// <summary>True when the pixel should be drawn.</summary>
        public bool IsVisible => A > 0;

        /// <summary>Fully transparent overlay pixel.</summary>
        public static WaterOverlayPixel Transparent => default;
    }
}
