using System.Globalization;

namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// One committed reference point: a named location on the field.
    /// </summary>
    public sealed class ReferencePointFeature
    {
        /// <summary>Prefix of a generated name, including the trailing space.</summary>
        public const string DEFAULT_NAME_PREFIX = "Reference Point ";

        /// <summary>
        /// Creates a reference point with a stable identifier.
        /// </summary>
        /// <param name="Id">Stable identifier such as reference_1.</param>
        /// <param name="Name">Name drawn beside the marker.</param>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        public ReferencePointFeature
            (
            string Id,
            string Name,
            double Latitude,
            double Longitude
            )
        {
            this.Id = Id;
            this.Name = string.IsNullOrWhiteSpace(Name) ? DefaultName(1) : Name.Trim();
            this.Latitude = Latitude;
            this.Longitude = Longitude;
        }

        /// <summary>Stable identifier unique among reference points in the project.</summary>
        public string Id { get; }

        /// <summary>Name drawn beside the marker.</summary>
        public string Name { get; set; }

        /// <summary>Latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>
        /// Builds the name used until the user types one.
        /// </summary>
        /// <param name="Number">Sequence number, starting at 1.</param>
        /// <returns>A name such as "Reference Point 1".</returns>
        static public string DefaultName
            (
            int Number
            )
        {
            int Shown = Number > 0 ? Number : 1;
            return DEFAULT_NAME_PREFIX + Shown.ToString(CultureInfo.InvariantCulture);
        }
    }
}
