using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Verifies 3D satellite plinth layout.
    /// </summary>

    public sealed class SatelliteMapLayout3DTests
    {
        /// <summary>
        /// Field centre lat used by this component.
        /// </summary>

        private const double FIELD_CENTRE_LAT = 40.0;
        /// <summary>
        /// Field centre lon used by this component.
        /// </summary>

        private const double FIELD_CENTRE_LON = -93.5;
        /// <summary>
        /// Near camera diagonal m used by this component.
        /// </summary>

        private const double NEAR_CAMERA_DIAGONAL_M = 400.0;
        /// <summary>
        /// Far camera diagonal m used by this component.
        /// </summary>

        private const double FAR_CAMERA_DIAGONAL_M = 1200.0;
        /// <summary>
        /// Orbit radius m used by this component.
        /// </summary>

        private const double ORBIT_RADIUS_M = 800.0;
        /// <summary>
        /// Orbit pitch rad used by this component.
        /// </summary>

        private const double ORBIT_PITCH_RAD = 45.0 * Math.PI / 180.0;
        /// <summary>
        /// Vertical exaggeration used by this component.
        /// </summary>

        private const double VERTICAL_EXAGGERATION = 2.0;
        /// <summary>
        /// Side tolerance m used by this component.
        /// </summary>

        private const double SIDE_TOLERANCE_M = 1.0;
        /// <summary>
        /// Distance tolerance m used by this component.
        /// </summary>

        private const double DISTANCE_TOLERANCE_M = 1.0;
        /// <summary>
        /// Expected value used when asserting distance min m.
        /// </summary>

        private const double EXPECTED_DISTANCE_MIN_M = ORBIT_RADIUS_M - DISTANCE_TOLERANCE_M;
        /// <summary>
        /// Expected value used when asserting distance max m.
        /// </summary>

        private const double EXPECTED_DISTANCE_MAX_M = ORBIT_RADIUS_M + DISTANCE_TOLERANCE_M;

        /// <summary>
        /// Plinth side length equals camera distance divided by sqrt(2).
        /// </summary>
        [Fact]
        public void ComputePlinthSquareBounds_DiagonalMatchesCameraDistance
            (
            )
        {
            (double MinLat, double MaxLat, double MinLon, double MaxLon) = SatelliteMapLayout3D.ComputePlinthSquareBounds(
                FIELD_CENTRE_LAT,
                FIELD_CENTRE_LON,
                ORBIT_RADIUS_M);

            UTM.UTMCoordinate Sw = UTM.FromLatLon(MinLat, MinLon);
            UTM.UTMCoordinate Ne = UTM.FromLatLon(MaxLat, MaxLon);
            double SideM = Math.Max(Ne.Easting - Sw.Easting, Ne.Northing - Sw.Northing);
            double ExpectedSideM = ORBIT_RADIUS_M / Math.Sqrt(2.0);

            Assert.InRange(SideM, ExpectedSideM - SIDE_TOLERANCE_M, ExpectedSideM + SIDE_TOLERANCE_M);
        }

        /// <summary>
        /// A farther camera produces a larger plinth and more tiles.
        /// </summary>
        [Fact]
        public void ComputePlinthTiles_FartherCameraRequestsMoreTiles
            (
            )
        {
            (_, IReadOnlyList<(int TileX, int TileY)> NearTiles) = SatelliteMapLayout3D.ComputePlinthTiles(
                FIELD_CENTRE_LAT, FIELD_CENTRE_LON, NEAR_CAMERA_DIAGONAL_M, VERTICAL_EXAGGERATION);
            (_, IReadOnlyList<(int TileX, int TileY)> FarTiles) = SatelliteMapLayout3D.ComputePlinthTiles(
                FIELD_CENTRE_LAT, FIELD_CENTRE_LON, FAR_CAMERA_DIAGONAL_M, VERTICAL_EXAGGERATION);

            Assert.NotEmpty(NearTiles);
            Assert.NotEmpty(FarTiles);
            Assert.True(FarTiles.Count > NearTiles.Count);
        }

        /// <summary>
        /// Camera distance equals orbit radius when the field centre is the orbit pivot.
        /// </summary>
        [Fact]
        public void ComputeCameraToFieldDistanceM_EqualsOrbitRadiusWhenFieldAtPivot
            (
            )
        {
            double Distance = SatelliteMapLayout3D.ComputeCameraToFieldDistanceM(
                FIELD_CENTRE_LAT, FIELD_CENTRE_LON, FIELD_CENTRE_LAT, FIELD_CENTRE_LON, 0.0, ORBIT_PITCH_RAD, ORBIT_RADIUS_M);

            Assert.InRange(Distance, EXPECTED_DISTANCE_MIN_M, EXPECTED_DISTANCE_MAX_M);
        }
    }
}
