namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// One geographic position on a well or road, stored in WGS-84 degrees.
    /// </summary>
    public readonly struct FeatureVertex
    {
        /// <summary>
        /// Creates a vertex from a latitude and longitude.
        /// </summary>
        /// <param name="Latitude">WGS-84 latitude in decimal degrees.</param>
        /// <param name="Longitude">WGS-84 longitude in decimal degrees.</param>
        public FeatureVertex
            (
            double Latitude,
            double Longitude
            )
        {
            this.Latitude = Latitude;
            this.Longitude = Longitude;
        }

        /// <summary>WGS-84 latitude in decimal degrees.</summary>
        public double Latitude { get; }

        /// <summary>WGS-84 longitude in decimal degrees.</summary>
        public double Longitude { get; }
    }
}
