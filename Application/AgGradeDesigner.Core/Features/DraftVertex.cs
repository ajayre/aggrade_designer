namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// A movable latitude and longitude used while a road or well is being edited.
    /// </summary>
    public sealed class DraftVertex
    {
        /// <summary>
        /// Creates a movable vertex.
        /// </summary>
        /// <param name="Latitude">WGS-84 latitude in decimal degrees.</param>
        /// <param name="Longitude">WGS-84 longitude in decimal degrees.</param>
        public DraftVertex
            (
            double Latitude,
            double Longitude
            )
        {
            this.Latitude = Latitude;
            this.Longitude = Longitude;
        }

        /// <summary>WGS-84 latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>WGS-84 longitude in decimal degrees.</summary>
        public double Longitude { get; set; }
    }
}
