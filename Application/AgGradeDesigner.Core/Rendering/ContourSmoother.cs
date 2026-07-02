using AgGradeDesigner.Core.Geo;



namespace AgGradeDesigner.Core.Rendering

{

    /// <summary>

    /// Chaikin smoothing for contour polylines.

    /// </summary>

    static public class ContourSmoother

    {

        /// <summary>
        /// Default Chaikin smoothing pass count.
        /// </summary>

        private const int DEFAULT_SMOOTHING_ITERATIONS = 2;

        /// <summary>
        /// Weight applied to the nearer vertex in one Chaikin subdivision step.
        /// </summary>

        private const double CHAIKIN_NEAR_WEIGHT = 0.75;

        /// <summary>
        /// Weight applied to the farther vertex in one Chaikin subdivision step.
        /// </summary>

        private const double CHAIKIN_FAR_WEIGHT = 0.25;

        /// <summary>
        /// Vertex count multiplier per Chaikin smoothing pass.
        /// </summary>

        private const int CHAIKIN_RESOLUTION_FACTOR = 2;



        /// <summary>

        /// Applies Chaikin corner-cutting iterations to a lat/lon polyline.

        /// </summary>

        /// <param name="Points">Input vertices in geographic order.</param>

        /// <param name="Iterations">Number of smoothing passes to apply.</param>

        /// <returns>Smoothed vertex list; short inputs are returned unchanged.</returns>

        static public List<(double Lat, double Lon)> Smooth

            (

            IReadOnlyList<(double Lat, double Lon)> Points,

            int Iterations = DEFAULT_SMOOTHING_ITERATIONS

            )

        {

            // Preserve endpoints when the polyline is too short to subdivide.

            if (Points.Count < GeoConstants.MIN_POLYGON_VERTICES)

                return new List<(double Lat, double Lon)>(Points);



            List<(double Lat, double Lon)> Current = new List<(double Lat, double Lon)>(Points);

            for (int Iter = 0; Iter < Iterations; Iter++)

            {

                // Each pass doubles interior resolution while pinning first and last vertices.

                List<(double Lat, double Lon)> Next = new List<(double Lat, double Lon)>(Current.Count * CHAIKIN_RESOLUTION_FACTOR) { Current[0] };

                for (int I = 0; I < Current.Count - 1; I++)

                {

                    (double Lat0, double Lon0) = Current[I];

                    (double Lat1, double Lon1) = Current[I + 1];

                    Next.Add((Lat0 * CHAIKIN_NEAR_WEIGHT + Lat1 * CHAIKIN_FAR_WEIGHT, Lon0 * CHAIKIN_NEAR_WEIGHT + Lon1 * CHAIKIN_FAR_WEIGHT));

                    Next.Add((Lat0 * CHAIKIN_FAR_WEIGHT + Lat1 * CHAIKIN_NEAR_WEIGHT, Lon0 * CHAIKIN_FAR_WEIGHT + Lon1 * CHAIKIN_NEAR_WEIGHT));

                }



                Next.Add(Current[^1]);

                Current = Next;

            }



            return Current;

        }

    }

}


