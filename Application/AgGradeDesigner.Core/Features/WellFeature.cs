namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// One committed well: a pad centre and the pad diameter.
    /// </summary>
    public sealed class WellFeature
    {
        /// <summary>
        /// Creates a well with a stable identifier.
        /// </summary>
        /// <param name="Id">Stable identifier such as well_1.</param>
        /// <param name="Latitude">Pad centre latitude in decimal degrees.</param>
        /// <param name="Longitude">Pad centre longitude in decimal degrees.</param>
        /// <param name="PadDiameterM">Pad diameter in metres.</param>
        public WellFeature
            (
            string Id,
            double Latitude,
            double Longitude,
            double PadDiameterM
            )
        {
            this.Id = Id;
            this.Latitude = Latitude;
            this.Longitude = Longitude;
            this.PadDiameterM = PadDiameterM;
        }

        /// <summary>Stable identifier unique among wells in the project.</summary>
        public string Id { get; }

        /// <summary>Pad centre latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Pad centre longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>Pad diameter in metres.</summary>
        public double PadDiameterM { get; set; }
    }
}
