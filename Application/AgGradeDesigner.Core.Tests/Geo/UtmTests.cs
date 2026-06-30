using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Unit tests for <see cref="UTM"/> coordinate conversion helpers.
    /// </summary>
    public sealed class UtmTests
    {
        /// <summary>
        /// Verifies lat/lon to UTM and back stays within tolerance.
        /// </summary>
        [Fact]
        public void FromLatLon_AndToLatLon_RoundTripWithinTolerance
            (
            )
        {
            double Lat = 36.0;
            double Lon = -90.0;
            UTM.UTMCoordinate Utm = UTM.FromLatLon(Lat, Lon);
            UTM.ToLatLon(Utm.Zone, Utm.IsNorthernHemisphere, Utm.Easting, Utm.Northing, out double OutLat, out double OutLon);
            // Round-trip error must stay within sub-metre geographic tolerance at six decimals.
            Assert.Equal(Lat, OutLat, 6);
            Assert.Equal(Lon, OutLon, 6);
        }

        /// <summary>
        /// Verifies the default view centre resolves to the expected UTM zone.
        /// </summary>
        [Fact]
        public void FromLatLon_DefaultViewCentre_MatchesSpecZone
            (
            )
        {
            UTM.UTMCoordinate Utm = UTM.FromLatLon(36.0, -90.0);
            // Product default camera sits in northern hemisphere zone 16.
            Assert.Equal(16, Utm.Zone);
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
            UTM.UTMCoordinate Origin = UTM.FromLatLon(36.0, -90.0);
            Coordinate Moved = UTM.OffsetLocation(new Coordinate(36.0, -90.0), 100.0, 50.0);
            UTM.UTMCoordinate MovedUtm = UTM.FromLatLon(Moved.Latitude, Moved.Longitude);
            // Easting and northing deltas should match the requested metre offsets.
            Assert.Equal(Origin.Easting + 100.0, MovedUtm.Easting, 1);
            Assert.Equal(Origin.Northing + 50.0, MovedUtm.Northing, 1);
        }
    }
}
