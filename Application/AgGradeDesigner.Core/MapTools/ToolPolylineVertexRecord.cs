namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// One vertex on a tool polyline with frozen proposed elevation (TOOLS-SPEC §3.2.3).
    /// </summary>
    public sealed class ToolPolylineVertexRecord
    {
        /// <summary>
        /// Creates a polyline vertex at a geographic location with stored elevation.
        /// </summary>
        /// <param name="Latitude">Horizontal map latitude in decimal degrees.</param>
        /// <param name="Longitude">Horizontal map longitude in decimal degrees.</param>
        /// <param name="ElevationM">Proposed surface elevation in metres at creation.</param>
        public ToolPolylineVertexRecord
            (
            double Latitude,
            double Longitude,
            double ElevationM
            )
        {
            this.Latitude = Latitude;
            this.Longitude = Longitude;
            this.ElevationM = ElevationM;
        }

        /// <summary>Horizontal map latitude in decimal degrees.</summary>
        public double Latitude { get; }

        /// <summary>Horizontal map longitude in decimal degrees.</summary>
        public double Longitude { get; }

        /// <summary>Stored proposed surface elevation in metres at creation time.</summary>
        public double ElevationM { get; }
    }
}
