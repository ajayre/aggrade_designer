using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Unit tests for <see cref="UTM"/> coordinate conversion helpers.
    /// </summary>
    public sealed class UtmTests
    {
        /// <summary>
        /// Default centre lat used by this component.
        /// </summary>

        private const double DEFAULT_CENTRE_LAT = 36.0;
        /// <summary>
        /// Default centre lon used by this component.
        /// </summary>

        private const double DEFAULT_CENTRE_LON = -90.0;
        /// <summary>
        /// Expected value used when asserting default zone.
        /// </summary>

        private const int EXPECTED_DEFAULT_ZONE = 16;
        /// <summary>
        /// East offset m used by this component.
        /// </summary>

        private const double EAST_OFFSET_M = 100.0;
        /// <summary>
        /// North offset m used by this component.
        /// </summary>

        private const double NORTH_OFFSET_M = 50.0;
        /// <summary>
        /// Round trip precision used by this component.
        /// </summary>

        private const int ROUND_TRIP_PRECISION = 6;
        /// <summary>
        /// Offset precision used by this component.
        /// </summary>

        private const int OFFSET_PRECISION = 1;

        /// <summary>
        /// Verifies lat/lon to UTM and back stays within tolerance.
        /// </summary>
        [Fact]
        public void FromLatLon_AndToLatLon_RoundTripWithinTolerance
            (
            )
        {
            UTM.UTMCoordinate Utm = UTM.FromLatLon(DEFAULT_CENTRE_LAT, DEFAULT_CENTRE_LON);
            UTM.ToLatLon(Utm.Zone, Utm.IsNorthernHemisphere, Utm.Easting, Utm.Northing, out double OutLat, out double OutLon);
            // Round-trip error must stay within sub-metre geographic tolerance at six decimals.
            Assert.Equal(DEFAULT_CENTRE_LAT, OutLat, ROUND_TRIP_PRECISION);
            Assert.Equal(DEFAULT_CENTRE_LON, OutLon, ROUND_TRIP_PRECISION);
        }

        /// <summary>
        /// Verifies the default view centre resolves to the expected UTM zone.
        /// </summary>
        [Fact]
        public void FromLatLon_DefaultViewCentre_MatchesSpecZone
            (
            )
        {
            UTM.UTMCoordinate Utm = UTM.FromLatLon(DEFAULT_CENTRE_LAT, DEFAULT_CENTRE_LON);
            // Product default camera sits in northern hemisphere zone 16.
            Assert.Equal(EXPECTED_DEFAULT_ZONE, Utm.Zone);
            Assert.True(Utm.IsNorthernHemisphere);
        }

        /// <summary>
        /// Verifies metre offsets in UTM space move the geographic point as expected.
        /// </summary>
        [Fact]
        public void OffsetLocation_MovesPointInUtmSpace
            (
            )
        {
            UTM.UTMCoordinate Origin = UTM.FromLatLon(DEFAULT_CENTRE_LAT, DEFAULT_CENTRE_LON);
            Coordinate Moved = UTM.OffsetLocation(new Coordinate(DEFAULT_CENTRE_LAT, DEFAULT_CENTRE_LON), EAST_OFFSET_M, NORTH_OFFSET_M);
            UTM.UTMCoordinate MovedUtm = UTM.FromLatLon(Moved.Latitude, Moved.Longitude);
            // Easting and northing deltas should match the requested metre offsets.
            Assert.Equal(Origin.Easting + EAST_OFFSET_M, MovedUtm.Easting, OFFSET_PRECISION);
            Assert.Equal(Origin.Northing + NORTH_OFFSET_M, MovedUtm.Northing, OFFSET_PRECISION);
        }
    }
}
