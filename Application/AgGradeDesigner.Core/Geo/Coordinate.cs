using System;

namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// WGS-84 latitude and longitude pair used across survey and map APIs.
    /// </summary>
    public class Coordinate
    {
        /// <summary>Latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Creates a coordinate at the origin (0°, 0°).
        /// </summary>
        public Coordinate
            (
            ) : this(0.0, 0.0)
        {
        }

        /// <summary>
        /// Creates a coordinate at the given latitude and longitude.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        public Coordinate
            (
            double Latitude,
            double Longitude
            )
        {
            this.Latitude = Latitude;
            this.Longitude = Longitude;
        }
    }
}
