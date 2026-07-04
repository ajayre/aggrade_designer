namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// One user-placed tool point stored in the session overlay (TOOLS-SPEC §3.1.6, §4.0.1).
    /// </summary>
    public sealed class ToolPointRecord
    {
        /// <summary>
        /// Creates a tool point with a stable session identifier and frozen proposed elevation.
        /// </summary>
        /// <param name="Id">Stable identifier (<c>tp_n</c>).</param>
        /// <param name="Latitude">Horizontal map latitude in decimal degrees.</param>
        /// <param name="Longitude">Horizontal map longitude in decimal degrees.</param>
        /// <param name="ElevationM">Proposed surface elevation in metres at creation.</param>
        public ToolPointRecord
            (
            string Id,
            double Latitude,
            double Longitude,
            double ElevationM
            )
        {
            this.Id = Id;
            this.Latitude = Latitude;
            this.Longitude = Longitude;
            this.ElevationM = ElevationM;
        }

        /// <summary>Stable session identifier unique among points and polylines.</summary>
        public string Id { get; }

        /// <summary>Horizontal map latitude in decimal degrees.</summary>
        public double Latitude { get; }

        /// <summary>Horizontal map longitude in decimal degrees.</summary>
        public double Longitude { get; }

        /// <summary>Stored proposed surface elevation in metres at creation time.</summary>
        public double ElevationM { get; }
    }
}
