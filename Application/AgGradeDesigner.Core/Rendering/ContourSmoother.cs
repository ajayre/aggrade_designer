namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Chaikin smoothing for contour polylines.
    /// </summary>
    static public class ContourSmoother
    {
        /// <summary>
        /// Applies Chaikin corner-cutting iterations to a lat/lon polyline.
        /// </summary>
        /// <param name="Points">Input vertices in geographic order.</param>
        /// <param name="Iterations">Number of smoothing passes to apply.</param>
        /// <returns>Smoothed vertex list; short inputs are returned unchanged.</returns>
        static public List<(double Lat, double Lon)> Smooth
            (
            IReadOnlyList<(double Lat, double Lon)> Points,
            int Iterations = 2
            )
        {
            // Preserve endpoints when the polyline is too short to subdivide.
            if (Points.Count < 3)
                return new List<(double Lat, double Lon)>(Points);

            List<(double Lat, double Lon)> Current = new List<(double Lat, double Lon)>(Points);
            for (int Iter = 0; Iter < Iterations; Iter++)
            {
                // Each pass doubles interior resolution while pinning first and last vertices.
                List<(double Lat, double Lon)> Next = new List<(double Lat, double Lon)>(Current.Count * 2) { Current[0] };
                for (int I = 0; I < Current.Count - 1; I++)
                {
                    (double Lat0, double Lon0) = Current[I];
                    (double Lat1, double Lon1) = Current[I + 1];
                    Next.Add((Lat0 * 0.75 + Lat1 * 0.25, Lon0 * 0.75 + Lon1 * 0.25));
                    Next.Add((Lat0 * 0.25 + Lat1 * 0.75, Lon0 * 0.25 + Lon1 * 0.75));
                }

                Next.Add(Current[^1]);
                Current = Next;
            }

            return Current;
        }
    }
}
