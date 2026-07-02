namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Planar polygon area helpers in projected coordinates.
    /// </summary>
    static public class PolygonArea
    {
        /// <summary>
        /// Computes the absolute area of a closed polygon ring in square metres.
        /// </summary>
        /// <param name="Ring">Closed polygon vertices in projected coordinates.</param>
        /// <returns>Area in square metres, or zero when the ring is too short.</returns>
        static public double ComputeAreaSquareMetres
            (
            IReadOnlyList<(double X, double Y)> Ring
            )
        {
            if (Ring.Count < GeoConstants.MIN_POLYGON_VERTICES)
                return 0.0;

            double Sum = 0.0;
            for (int I = 0; I < Ring.Count; I++)
            {
                int J = (I + 1) % Ring.Count;
                Sum += (Ring[I].X * Ring[J].Y) - (Ring[J].X * Ring[I].Y);
            }

            return Math.Abs(Sum) * GeoConstants.SHOELACE_AREA_DIVISOR;
        }

        /// <summary>
        /// Computes field area in acres from a geographic boundary ring.
        /// </summary>
        /// <param name="BoundaryLatLon">Boundary vertices as latitude and longitude pairs.</param>
        /// <returns>Area in acres, or zero when the boundary is invalid.</returns>
        static public double ComputeAreaAcresFromLatLonRing
            (
            IReadOnlyList<(double Lat, double Lon)> BoundaryLatLon
            )
        {
            if (BoundaryLatLon.Count < GeoConstants.MIN_POLYGON_VERTICES)
                return 0.0;

            List<(double X, double Y)> UtmRing = BoundaryMask.BuildUtmRing(BoundaryLatLon);
            return ComputeAreaSquareMetres(UtmRing) / FieldConstants.SQUARE_METERS_PER_ACRE;
        }
    }
}
