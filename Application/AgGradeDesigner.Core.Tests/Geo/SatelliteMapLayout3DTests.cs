using AgGradeDesigner.Core.Geo;



namespace AgGradeDesigner.Core.Tests.Geo

{

    /// <summary>

    /// Verifies 3D satellite plinth layout.

    /// </summary>

    public sealed class SatelliteMapLayout3DTests

    {

        /// <summary>

        /// Plinth side length equals camera distance divided by sqrt(2).

        /// </summary>

        [Fact]

        public void ComputePlinthSquareBounds_DiagonalMatchesCameraDistance

            (

            )

        {

            const double FieldCentreLat = 40.0;

            const double FieldCentreLon = -93.5;

            const double DiagonalM = 800.0;



            (double MinLat, double MaxLat, double MinLon, double MaxLon) = SatelliteMapLayout3D.ComputePlinthSquareBounds(

                FieldCentreLat,

                FieldCentreLon,

                DiagonalM);



            UTM.UTMCoordinate Sw = UTM.FromLatLon(MinLat, MinLon);

            UTM.UTMCoordinate Ne = UTM.FromLatLon(MaxLat, MaxLon);

            double SideM = Math.Max(Ne.Easting - Sw.Easting, Ne.Northing - Sw.Northing);

            double ExpectedSideM = DiagonalM / Math.Sqrt(2.0);



            Assert.InRange(SideM, ExpectedSideM - 1.0, ExpectedSideM + 1.0);

        }



        /// <summary>

        /// A farther camera produces a larger plinth and more tiles.

        /// </summary>

        [Fact]

        public void ComputePlinthTiles_FartherCameraRequestsMoreTiles

            (

            )

        {

            const double FieldCentreLat = 40.0;

            const double FieldCentreLon = -93.5;



            (_, IReadOnlyList<(int TileX, int TileY)> NearTiles) = SatelliteMapLayout3D.ComputePlinthTiles(

                FieldCentreLat, FieldCentreLon, 400.0, 2.0);

            (_, IReadOnlyList<(int TileX, int TileY)> FarTiles) = SatelliteMapLayout3D.ComputePlinthTiles(

                FieldCentreLat, FieldCentreLon, 1200.0, 2.0);



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
            const double Lat = 40.0;
            const double Lon = -93.5;

            double Distance = SatelliteMapLayout3D.ComputeCameraToFieldDistanceM(
                Lat, Lon, Lat, Lon, 0.0, 45.0 * Math.PI / 180.0, 800.0);

            Assert.InRange(Distance, 799.0, 801.0);
        }

    }

}


