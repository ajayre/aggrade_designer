namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// A well centre that has been clicked but not saved yet.
    /// </summary>
    public sealed class WellDraft
    {
        /// <summary>
        /// Creates a draft well.
        /// </summary>
        /// <param name="Latitude">Pad centre latitude in decimal degrees.</param>
        /// <param name="Longitude">Pad centre longitude in decimal degrees.</param>
        /// <param name="PadDiameterM">Pad diameter in metres.</param>
        public WellDraft
            (
            double Latitude,
            double Longitude,
            double PadDiameterM
            )
        {
            this.Latitude = Latitude;
            this.Longitude = Longitude;
            this.PadDiameterM = PadDiameterM;
        }

        /// <summary>Pad centre latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Pad centre longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>Pad diameter in metres.</summary>
        public double PadDiameterM { get; set; }
    }
}
