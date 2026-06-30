using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Models
{
    /// <summary>
    /// A named survey benchmark with geodetic location and stored elevation.
    /// </summary>
    public class Benchmark
    {
        /// <summary>Benchmark location as latitude and longitude.</summary>
        public Coordinate Location;

        /// <summary>Benchmark name (for example MB, BM1).</summary>
        public string Name;

        /// <summary>Stored elevation in metres (multiplane-relative or absolute per source format).</summary>
        public double Elevation;

        /// <summary>
        /// Creates a benchmark from a geodetic location and elevation.
        /// </summary>
        /// <param name="Location">Benchmark latitude and longitude.</param>
        /// <param name="Name">Benchmark identifier.</param>
        /// <param name="Elevation">Elevation in metres.</param>
        public Benchmark
            (
            Coordinate Location,
            string Name,
            double Elevation
            )
        {
            // Capture immutable survey control-point identity for persistence and map display.
            this.Location = Location;
            this.Name = Name;
            this.Elevation = Elevation;
        }

        /// <summary>
        /// Creates a benchmark from decimal-degree coordinates and elevation.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <param name="Name">Benchmark identifier.</param>
        /// <param name="Elevation">Elevation in metres.</param>
        public Benchmark
            (
            double Latitude,
            double Longitude,
            string Name,
            double Elevation
            ) : this(new Coordinate(Latitude, Longitude), Name, Elevation)
        {
        }
    }
}
