namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// A reference point that has been clicked but not saved yet.
    /// </summary>
    public sealed class ReferencePointDraft
    {
        /// <summary>
        /// Creates a draft reference point.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <param name="Name">Name that will be saved with the point.</param>
        public ReferencePointDraft
            (
            double Latitude,
            double Longitude,
            string Name
            )
        {
            this.Latitude = Latitude;
            this.Longitude = Longitude;
            this.Name = Name;
        }

        /// <summary>Latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>Name that will be saved with the point.</summary>
        public string Name { get; set; }
    }
}
