using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Features;

namespace AgGradeDesigner.Core.Tests.Features
{
    /// <summary>
    /// Tests for the road bed default and the dashed guide offsets.
    /// </summary>
    public sealed class RoadCrossSectionTests
    {
        /// <summary>Blade width in the worked example, in feet.</summary>
        private const double BLADE_WIDTH_FT = 8.5;

        /// <summary>Side slope in the worked example, as horizontal run per one vertical.</summary>
        private const double SIDE_SLOPE = 3.0;

        /// <summary>Bed width produced by twice the example blade minus the example slope, in feet.</summary>
        private const double EXPECTED_BED_FT = 14.0;

        /// <summary>Toe distance for a 14 ft bed, a 1 ft height, and a 3:1 slope, in feet.</summary>
        private const double EXPECTED_TOE_FT = 10.0;

        /// <summary>Inches in one foot, used to build the example blade width in millimetres.</summary>
        private const double INCHES_PER_FOOT = 12.0;

        /// <summary>Millimetres in one inch.</summary>
        private const double MM_PER_INCH = 25.4;

        /// <summary>Ground offset used to check a parallel line, in metres.</summary>
        private const double OFFSET_M = 10.0;

        /// <summary>Length of each leg of the corner used to check a miter join, in metres.</summary>
        private const double LEG_M = 100.0;

        /// <summary>Latitude of the local origin for the offset checks.</summary>
        private const double ORIGIN_LATITUDE = 40.0;

        /// <summary>Longitude of the local origin for the offset checks.</summary>
        private const double ORIGIN_LONGITUDE = -93.0;

        /// <summary>Accepted error when comparing an offset back to metres.</summary>
        private const int OFFSET_DECIMALS = 2;

        /// <summary>Half of a full width. Each bed line is this fraction of the bed from the centreline.</summary>
        private const double HALF_WIDTH = 0.5;

        /// <summary>
        /// An 8.5 ft blade and a side slope of 3 default the bed to 14 ft.
        /// </summary>
        [Fact]
        public void DefaultBedWidth_IsTwiceBladeMinusSlope
            (
            )
        {
            double BedM = RoadCrossSection.DefaultBedWidthM(BladeWidthMm(), SIDE_SLOPE);

            Assert.Equal(EXPECTED_BED_FT * FieldConstants.METERS_PER_FOOT, BedM, 6);
        }

        /// <summary>
        /// The 2× blade-width circle has a diameter of twice the blade, so its radius is one blade width.
        /// </summary>
        [Fact]
        public void BladePairOffset_IsOneBladeWidth
            (
            )
        {
            double OffsetM = RoadCrossSection.BladePairOffsetM(BladeWidthMm());

            Assert.Equal(BLADE_WIDTH_FT * FieldConstants.METERS_PER_FOOT, OffsetM, 6);
        }

        /// <summary>
        /// Each bed line sits half the bed width from the centreline, so a wider bed moves that pair.
        /// </summary>
        [Fact]
        public void BedEdgeOffset_IsHalfTheBed
            (
            )
        {
            double BedM = EXPECTED_BED_FT * FieldConstants.METERS_PER_FOOT;

            Assert.Equal(BedM * HALF_WIDTH, RoadCrossSection.BedEdgeOffsetM(BedM), 6);
        }

        /// <summary>
        /// A 14 ft bed, a 1 ft height, and a 3:1 slope put each toe 10 ft from the centreline.
        /// </summary>
        [Fact]
        public void ToeOffset_AddsSideRunBeyondHalfBed
            (
            )
        {
            double ToeM = RoadCrossSection.ToeOffsetM(
                EXPECTED_BED_FT * FieldConstants.METERS_PER_FOOT,
                RoadCrossSection.DefaultHeightM,
                SIDE_SLOPE);

            Assert.Equal(EXPECTED_TOE_FT * FieldConstants.METERS_PER_FOOT, ToeM, 6);
        }

        /// <summary>
        /// A line that runs east offsets to the north on the left and stays parallel.
        /// </summary>
        [Fact]
        public void Offset_StraightEastLineMovesNorth
            (
            )
        {
            FeatureVertex[] Line = new FeatureVertex[]
            {
                Plan(0.0, 0.0),
                Plan(LEG_M, 0.0),
            };

            List<FeatureVertex> Left = RoadPlanOffset.Offset(Line, OFFSET_M);

            Assert.Equal(2, Left.Count);
            Assert.Equal(0.0, EastOf(Left[0]), OFFSET_DECIMALS);
            Assert.Equal(OFFSET_M, NorthOf(Left[0]), OFFSET_DECIMALS);
            Assert.Equal(LEG_M, EastOf(Left[1]), OFFSET_DECIMALS);
            Assert.Equal(OFFSET_M, NorthOf(Left[1]), OFFSET_DECIMALS);
        }

        /// <summary>
        /// A right-angle corner miters so both offset legs stay parallel to the centreline.
        /// </summary>
        [Fact]
        public void Offset_RightAngleCornerMiters
            (
            )
        {
            FeatureVertex[] Line = new FeatureVertex[]
            {
                Plan(0.0, 0.0),
                Plan(LEG_M, 0.0),
                Plan(LEG_M, LEG_M),
            };

            List<FeatureVertex> Left = RoadPlanOffset.Offset(Line, OFFSET_M);

            Assert.Equal(0.0, EastOf(Left[0]), OFFSET_DECIMALS);
            Assert.Equal(OFFSET_M, NorthOf(Left[0]), OFFSET_DECIMALS);
            Assert.Equal(LEG_M - OFFSET_M, EastOf(Left[1]), OFFSET_DECIMALS);
            Assert.Equal(OFFSET_M, NorthOf(Left[1]), OFFSET_DECIMALS);
            Assert.Equal(LEG_M - OFFSET_M, EastOf(Left[2]), OFFSET_DECIMALS);
            Assert.Equal(LEG_M, NorthOf(Left[2]), OFFSET_DECIMALS);
        }

        /// <summary>
        /// Example blade width in millimetres.
        /// </summary>
        /// <returns>8.5 ft converted to millimetres.</returns>
        private static double BladeWidthMm
            (
            )
        {
            return BLADE_WIDTH_FT * INCHES_PER_FOOT * MM_PER_INCH;
        }

        /// <summary>
        /// Builds a vertex from metres east and north of the test origin.
        /// </summary>
        /// <param name="EastM">Easting in metres.</param>
        /// <param name="NorthM">Northing in metres.</param>
        /// <returns>Geographic vertex.</returns>
        private static FeatureVertex Plan
            (
            double EastM,
            double NorthM
            )
        {
            return new FeatureVertex(
                ORIGIN_LATITUDE + (NorthM / FieldConstants.METRES_PER_DEGREE_LAT),
                ORIGIN_LONGITUDE + (EastM / MetresPerDegreeLon()));
        }

        /// <summary>
        /// Easting of a vertex relative to the test origin, in metres.
        /// </summary>
        /// <param name="Vertex">Geographic vertex.</param>
        /// <returns>Easting in metres.</returns>
        private static double EastOf
            (
            FeatureVertex Vertex
            )
        {
            return (Vertex.Longitude - ORIGIN_LONGITUDE) * MetresPerDegreeLon();
        }

        /// <summary>
        /// Northing of a vertex relative to the test origin, in metres.
        /// </summary>
        /// <param name="Vertex">Geographic vertex.</param>
        /// <returns>Northing in metres.</returns>
        private static double NorthOf
            (
            FeatureVertex Vertex
            )
        {
            return (Vertex.Latitude - ORIGIN_LATITUDE) * FieldConstants.METRES_PER_DEGREE_LAT;
        }

        /// <summary>
        /// Metres per degree of longitude at the test origin.
        /// </summary>
        /// <returns>Metres per degree.</returns>
        private static double MetresPerDegreeLon
            (
            )
        {
            double CosLat = Math.Cos(ORIGIN_LATITUDE * Math.PI / FieldConstants.DEGREES_PER_RADIAN);
            return FieldConstants.METRES_PER_DEGREE_LAT * CosLat;
        }
    }
}
