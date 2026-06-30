namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Field boundary polygon tests in UTM easting/northing coordinates.
    /// </summary>
    static public class BoundaryMask
    {
        private const double EPS = 1e-12;

        /// <summary>
        /// Tests whether a UTM point lies inside a closed boundary ring using ray casting.
        /// </summary>
        /// <param name="Easting">Point easting in metres.</param>
        /// <param name="Northing">Point northing in metres.</param>
        /// <param name="Ring">Closed polygon vertices in UTM coordinates.</param>
        /// <returns>True when inside the ring; open rings with fewer than three vertices are treated as fully inside.</returns>
        static public bool IsInsideBoundary
            (
            double Easting,
            double Northing,
            IReadOnlyList<(double X, double Y)> Ring
            )
        {
            int Count = Ring.Count;
            if (Count < 3)
                return true;

            bool Inside = false;
            int J = Count - 1;

            for (int I = 0; I < Count; I++)
            {
                double Xi = Ring[I].X;
                double Yi = Ring[I].Y;
                double Xj = Ring[J].X;
                double Yj = Ring[J].Y;

                bool Intersects = (Yi > Northing) != (Yj > Northing);
                if (Intersects)
                {
                    // Count edge crossings to the right of the test point for parity rule.
                    double XAtY = ((Xj - Xi) * (Northing - Yi) / ((Yj - Yi) + EPS)) + Xi;
                    if (Easting <= XAtY)
                        Inside = !Inside;
                }

                J = I;
            }

            return Inside;
        }

        /// <summary>
        /// Converts a lat/lon boundary ring to UTM easting/northing vertices.
        /// </summary>
        /// <param name="BoundaryLatLon">Boundary vertices as latitude and longitude pairs.</param>
        /// <returns>UTM ring in the same vertex order.</returns>
        static public List<(double X, double Y)> BuildUtmRing
            (
            IReadOnlyList<(double Lat, double Lon)> BoundaryLatLon
            )
        {
            List<(double X, double Y)> Ring = new List<(double X, double Y)>(BoundaryLatLon.Count);
            foreach ((double Lat, double Lon) in BoundaryLatLon)
            {
                UTM.UTMCoordinate Utm = UTM.FromLatLon(Lat, Lon);
                Ring.Add((Utm.Easting, Utm.Northing));
            }

            return Ring;
        }
    }
}
