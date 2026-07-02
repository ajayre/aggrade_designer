using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Unit tests for <see cref="BoundaryMask"/> polygon ordering and inside tests.
    /// </summary>
    public sealed class BoundaryMaskTests
    {
        /// <summary>
        /// Corner lat 0 used by this component.
        /// </summary>

        private const double CORNER_LAT_0 = 40.0000;
        /// <summary>
        /// Corner lon 0 used by this component.
        /// </summary>

        private const double CORNER_LON_0 = -93.5000;
        /// <summary>
        /// Corner lat 1 used by this component.
        /// </summary>

        private const double CORNER_LAT_1 = 40.0010;
        /// <summary>
        /// Corner lon 1 used by this component.
        /// </summary>

        private const double CORNER_LON_1 = -93.4990;
        /// <summary>
        /// Corner lat 2 used by this component.
        /// </summary>

        private const double CORNER_LAT_2 = 40.0000;
        /// <summary>
        /// Corner lon 2 used by this component.
        /// </summary>

        private const double CORNER_LON_2 = -93.4990;
        /// <summary>
        /// Corner lat 3 used by this component.
        /// </summary>

        private const double CORNER_LAT_3 = 40.0010;
        /// <summary>
        /// Corner lon 3 used by this component.
        /// </summary>

        private const double CORNER_LON_3 = -93.5000;
        /// <summary>
        /// Min path points used by this component.
        /// </summary>

        private const int MIN_PATH_POINTS = 2;

        /// <summary>
        /// Verifies out-of-order perimeter points are reordered into a short closed ring.
        /// </summary>
        [Fact]
        public void OrderBoundaryLatLon_ReordersZigZagPerimeter
            (
            )
        {
            List<(double Lat, double Lon)> ZigZag = new List<(double Lat, double Lon)>
            {
                (CORNER_LAT_0, CORNER_LON_0),
                (CORNER_LAT_1, CORNER_LON_1),
                (CORNER_LAT_2, CORNER_LON_2),
                (CORNER_LAT_3, CORNER_LON_3)
            };

            List<(double Lat, double Lon)> Ordered = BoundaryMask.OrderBoundaryLatLon(ZigZag);

            Assert.Equal(ZigZag.Count, Ordered.Count);
            double UnorderedLength = PathLength(ZigZag);
            double OrderedLength = PathLength(Ordered);
            Assert.True(OrderedLength < UnorderedLength,
                $"Expected shorter ordered ring, unordered={UnorderedLength:G4}, ordered={OrderedLength:G4}.");
        }

        /// <summary>
        /// Computes the perimeter length of a closed ring through the given vertices in order.
        /// </summary>
        static private double PathLength
            (
            IReadOnlyList<(double Lat, double Lon)> Points
            )
        {
            if (Points.Count < MIN_PATH_POINTS)
                return 0.0;

            double Total = 0.0;
            for (int Index = 0; Index < Points.Count; Index++)
            {
                (double Lat0, double Lon0) = Points[Index];
                (double Lat1, double Lon1) = Points[(Index + 1) % Points.Count];
                UTM.UTMCoordinate A = UTM.FromLatLon(Lat0, Lon0);
                UTM.UTMCoordinate B = UTM.FromLatLon(Lat1, Lon1);
                double Dx = B.Easting - A.Easting;
                double Dy = B.Northing - A.Northing;
                Total += Math.Sqrt((Dx * Dx) + (Dy * Dy));
            }

            return Total;
        }
    }
}
