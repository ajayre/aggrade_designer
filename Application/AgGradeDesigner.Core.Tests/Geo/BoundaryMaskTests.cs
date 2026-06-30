using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Unit tests for <see cref="BoundaryMask"/> polygon ordering and inside tests.
    /// </summary>
    public sealed class BoundaryMaskTests
    {
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
                (40.0000, -93.5000),
                (40.0010, -93.4990),
                (40.0000, -93.4990),
                (40.0010, -93.5000)
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
            if (Points.Count < 2)
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
