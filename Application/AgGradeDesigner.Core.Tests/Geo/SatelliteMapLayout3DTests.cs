using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.Tests.Geo
{
    /// <summary>
    /// Verifies 3D satellite tile selection for orbit camera pitch.
    /// </summary>
    public sealed class SatelliteMapLayout3DTests
    {
        /// <summary>
        /// Steep pitch requests more tiles than shallow pitch for the same viewport.
        /// </summary>
        [Fact]
        public void ComputeVisibleTiles_SteepPitchRequestsMoreTilesThanShallowPitch
            (
            )
        {
            const double CentreLat = 40.0;
            const double CentreLon = -93.5;
            const double FieldMinLat = 39.998;
            const double FieldMaxLat = 40.004;
            const double FieldMinLon = -93.502;
            const double FieldMaxLon = -93.494;

            (_, IReadOnlyList<(int TileX, int TileY)> SteepTiles) = SatelliteMapLayout3D.ComputeVisibleTiles(
                CentreLat, CentreLon, 45.0, 45.0, 800.0,
                1024, 768, 2.0,
                FieldMinLat, FieldMaxLat, FieldMinLon, FieldMaxLon);

            (_, IReadOnlyList<(int TileX, int TileY)> ShallowTiles) = SatelliteMapLayout3D.ComputeVisibleTiles(
                CentreLat, CentreLon, 45.0, 20.0, 800.0,
                1024, 768, 2.0,
                FieldMinLat, FieldMaxLat, FieldMinLon, FieldMaxLon);

            Assert.NotEmpty(SteepTiles);
            Assert.NotEmpty(ShallowTiles);
            Assert.True(SteepTiles.Count > ShallowTiles.Count);
        }

        /// <summary>
        /// Ground unproject succeeds for a point near the bottom-centre of a pitched view.
        /// </summary>
        [Fact]
        public void TryUnprojectGround_BottomCentre_SucceedsForTypicalOrbit
            (
            )
        {
            bool Ok = SatelliteMapLayout3D.TryUnprojectGround(
                40.0, -93.5,
                45.0 * Math.PI / 180.0,
                30.0 * Math.PI / 180.0,
                800.0,
                800, 600,
                400, 550,
                out double Lat, out double Lon);

            Assert.True(Ok);
            Assert.InRange(Lat, 39.0, 41.0);
            Assert.InRange(Lon, -94.5, -92.5);
        }
    }
}
