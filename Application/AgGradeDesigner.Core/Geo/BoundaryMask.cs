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
        /// Tests whether a lat/lon point lies inside a closed boundary ring using ray casting.
        /// </summary>
        /// <param name="Latitude">Point latitude in decimal degrees.</param>
        /// <param name="Longitude">Point longitude in decimal degrees.</param>
        /// <param name="Ring">Closed polygon vertices in geographic coordinates.</param>
        /// <returns>True when inside the ring; open rings with fewer than three vertices are treated as fully inside.</returns>
        static public bool IsInsideBoundaryLatLon
            (
            double Latitude,
            double Longitude,
            IReadOnlyList<(double Lat, double Lon)> Ring
            )
        {
            int Count = Ring.Count;
            if (Count < 3)
                return true;

            bool Inside = false;
            int J = Count - 1;

            for (int I = 0; I < Count; I++)
            {
                double Yi = Ring[I].Lat;
                double Xi = Ring[I].Lon;
                double Yj = Ring[J].Lat;
                double Xj = Ring[J].Lon;

                bool Intersects = (Yi > Latitude) != (Yj > Latitude);
                if (Intersects)
                {
                    double XAtY = ((Xj - Xi) * (Latitude - Yi) / ((Yj - Yi) + EPS)) + Xi;
                    if (Longitude <= XAtY)
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

        /// <summary>
        /// Orders boundary vertices into a closed perimeter ring suitable for drawing and clipping.
        /// </summary>
        /// <param name="BoundaryLatLon">Unordered boundary vertices as latitude and longitude pairs.</param>
        /// <returns>Vertices in traversal order around the field perimeter.</returns>
        static public List<(double Lat, double Lon)> OrderBoundaryLatLon
            (
            IReadOnlyList<(double Lat, double Lon)> BoundaryLatLon
            )
        {
            if (BoundaryLatLon.Count <= 2)
                return new List<(double Lat, double Lon)>(BoundaryLatLon);

            List<(double X, double Y)> UtmRing = ChooseBoundaryRing(BuildUtmRing(BoundaryLatLon));
            UTM.UTMCoordinate ZoneRef = UTM.FromLatLon(BoundaryLatLon[0].Lat, BoundaryLatLon[0].Lon);
            List<(double Lat, double Lon)> Ordered = new List<(double Lat, double Lon)>(UtmRing.Count);
            foreach ((double X, double Y) in UtmRing)
            {
                UTM.ToLatLon(ZoneRef.Zone, ZoneRef.IsNorthernHemisphere, X, Y, out double Lat, out double Lon);
                Ordered.Add((Lat, Lon));
            }

            return Ordered;
        }

        /// <summary>
        /// Picks the shorter closed boundary ring between file order and angle-sorted order.
        /// </summary>
        static private List<(double X, double Y)> ChooseBoundaryRing
            (
            List<(double X, double Y)> Points
            )
        {
            if (Points.Count <= 2)
                return new List<(double X, double Y)>(Points);

            List<(double X, double Y)> Sequential = new List<(double X, double Y)>(Points);
            List<(double X, double Y)> Angular = SortBoundaryRingByAngle(Points);
            return ClosedPathLength(Sequential) <= ClosedPathLength(Angular) ? Sequential : Angular;
        }

        /// <summary>
        /// Orders boundary vertices counter-clockwise around their centroid.
        /// </summary>
        static private List<(double X, double Y)> SortBoundaryRingByAngle
            (
            List<(double X, double Y)> Points
            )
        {
            if (Points.Count <= 2)
                return new List<(double X, double Y)>(Points);

            double Cx = 0;
            double Cy = 0;
            foreach ((double X, double Y) in Points)
            {
                Cx += X;
                Cy += Y;
            }

            Cx /= Points.Count;
            Cy /= Points.Count;
            List<(double X, double Y)> Sorted = new List<(double X, double Y)>(Points);
            Sorted.Sort((A, B) => Math.Atan2(A.Y - Cy, A.X - Cx).CompareTo(Math.Atan2(B.Y - Cy, B.X - Cx)));
            return Sorted;
        }

        /// <summary>
        /// Computes perimeter length of a closed polyline through the given vertices.
        /// </summary>
        static private double ClosedPathLength
            (
            List<(double X, double Y)> Points
            )
        {
            if (Points.Count < 2)
                return 0.0;

            double Total = 0.0;
            int Count = Points.Count;
            for (int Index = 0; Index < Count; Index++)
            {
                (double X0, double Y0) = Points[Index];
                (double X1, double Y1) = Points[(Index + 1) % Count];
                Total += Math.Sqrt((X1 - X0) * (X1 - X0) + (Y1 - Y0) * (Y1 - Y0));
            }

            return Total;
        }
    }
}
