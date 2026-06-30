namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Camera centre and zoom computed to frame a geographic extent in the 2D map viewer.
    /// </summary>
    public readonly struct MapViewFitResult
    {
        /// <summary>Latitude of the map centre in decimal degrees.</summary>
        public double CentreLat { get; init; }

        /// <summary>Longitude of the map centre in decimal degrees.</summary>
        public double CentreLon { get; init; }

        /// <summary>Zoom multiplier applied to the base metres-per-pixel estimate.</summary>
        public double ZoomFactor { get; init; }

        /// <summary>Ground metres represented by one screen pixel at the fitted zoom.</summary>
        public double MetresPerPixel { get; init; }
    }
}
