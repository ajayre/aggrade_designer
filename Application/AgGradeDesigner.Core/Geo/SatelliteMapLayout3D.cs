namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Lays out the square satellite plinth used in the 3D orbit view.
    /// The plinth top is a square whose diagonal equals the camera-to-field-centre distance.
    /// </summary>
    static public class SatelliteMapLayout3D
    {
        /// <summary>Maximum number of plinth tiles returned to cap memory use.</summary>
        private const int MaxTileCount = 192;

        /// <summary>Target ground resolution used when picking plinth tile zoom.</summary>
        private const double PLINTH_TILE_TARGET_METRES_PER_PIXEL = 3.0;

        /// <summary>Maximum Web Mercator zoom level used for plinth imagery.</summary>
        private const int PLINTH_MAX_ZOOM = 15;

        /// <summary>Ratio of square side length to diagonal (√2/2).</summary>
        private static readonly double SQUARE_DIAGONAL_FACTOR = 2.0 * Math.Sqrt(2.0);

        /// <summary>
        /// Returns the straight-line distance from the field centre to the orbit camera.
        /// </summary>
        /// <param name="FieldCentreLat">Field centre latitude in decimal degrees.</param>
        /// <param name="FieldCentreLon">Field centre longitude in decimal degrees.</param>
        /// <param name="OrbitCentreLat">Orbit pivot latitude in decimal degrees.</param>
        /// <param name="OrbitCentreLon">Orbit pivot longitude in decimal degrees.</param>
        /// <param name="HeadingRad">Camera heading in radians clockwise from north.</param>
        /// <param name="PitchRad">Camera pitch in radians above horizontal.</param>
        /// <param name="DistanceM">Camera distance from the orbit pivot in metres.</param>
        /// <returns>Camera-to-field-centre distance in metres, or zero when distance is invalid.</returns>
        static public double ComputeCameraToFieldDistanceM
            (
            double FieldCentreLat,
            double FieldCentreLon,
            double OrbitCentreLat,
            double OrbitCentreLon,
            double HeadingRad,
            double PitchRad,
            double DistanceM
            )
        {
            if (DistanceM <= 0)
                return 0;

            UTM.UTMCoordinate FieldUtm = UTM.FromLatLon(FieldCentreLat, FieldCentreLon);
            UTM.UTMCoordinate OrbitUtm = UTM.FromLatLon(OrbitCentreLat, OrbitCentreLon);

            double CosP = Math.Cos(PitchRad);
            double SinP = Math.Sin(PitchRad);
            double SinH = Math.Sin(HeadingRad);
            double CosH = Math.Cos(HeadingRad);

            double CamEx = -SinH * DistanceM * CosP;
            double CamEy = -CosH * DistanceM * CosP;
            double CamEz = DistanceM * SinP;

            double FieldEx = FieldUtm.Easting - OrbitUtm.Easting;
            double FieldEy = FieldUtm.Northing - OrbitUtm.Northing;

            double Dx = CamEx - FieldEx;
            double Dy = CamEy - FieldEy;
            double Dz = CamEz;
            return Math.Sqrt((Dx * Dx) + (Dy * Dy) + (Dz * Dz));
        }

        /// <summary>
        /// Geographic bounds of the axis-aligned square plinth top centred on the field.
        /// </summary>
        /// <param name="FieldCentreLat">Field centre latitude in decimal degrees.</param>
        /// <param name="FieldCentreLon">Field centre longitude in decimal degrees.</param>
        /// <param name="PlinthDiagonalM">Plinth square diagonal length in metres.</param>
        /// <returns>Minimum and maximum latitude and longitude of the plinth square.</returns>
        static public (double MinLat, double MaxLat, double MinLon, double MaxLon) ComputePlinthSquareBounds
            (
            double FieldCentreLat,
            double FieldCentreLon,
            double PlinthDiagonalM
            )
        {
            if (PlinthDiagonalM <= 0)
                return (FieldCentreLat, FieldCentreLat, FieldCentreLon, FieldCentreLon);

            double HalfSideM = PlinthDiagonalM / SQUARE_DIAGONAL_FACTOR;
            UTM.UTMCoordinate Centre = UTM.FromLatLon(FieldCentreLat, FieldCentreLon);

            UTM.ToLatLon(
                Centre.Zone,
                Centre.IsNorthernHemisphere,
                Centre.Easting - HalfSideM,
                Centre.Northing - HalfSideM,
                out double SwLat,
                out double SwLon);
            UTM.ToLatLon(
                Centre.Zone,
                Centre.IsNorthernHemisphere,
                Centre.Easting + HalfSideM,
                Centre.Northing + HalfSideM,
                out double NeLat,
                out double NeLon);

            return (
                Math.Min(SwLat, NeLat),
                Math.Max(SwLat, NeLat),
                Math.Min(SwLon, NeLon),
                Math.Max(SwLon, NeLon));
        }

        /// <summary>
        /// Returns the four plinth-top corners in south-west, south-east, north-east, north-west order.
        /// </summary>
        /// <param name="FieldCentreLat">Field centre latitude in decimal degrees.</param>
        /// <param name="FieldCentreLon">Field centre longitude in decimal degrees.</param>
        /// <param name="PlinthDiagonalM">Plinth square diagonal length in metres.</param>
        /// <returns>Four corner coordinates in traversal order.</returns>
        static public (double Lat, double Lon)[] ComputePlinthTopCorners
            (
            double FieldCentreLat,
            double FieldCentreLon,
            double PlinthDiagonalM
            )
        {
            (double MinLat, double MaxLat, double MinLon, double MaxLon) = ComputePlinthSquareBounds(
                FieldCentreLat,
                FieldCentreLon,
                PlinthDiagonalM);

            return new[]
            {
                (MinLat, MinLon),
                (MinLat, MaxLon),
                (MaxLat, MaxLon),
                (MaxLat, MinLon)
            };
        }

        /// <summary>
        /// Computes Web Mercator tiles covering the plinth top square.
        /// </summary>
        /// <param name="FieldCentreLat">Field centre latitude in decimal degrees.</param>
        /// <param name="FieldCentreLon">Field centre longitude in decimal degrees.</param>
        /// <param name="PlinthDiagonalM">Plinth square diagonal length in metres.</param>
        /// <param name="MetresPerPixel">Current viewport ground resolution in metres per pixel.</param>
        /// <returns>Selected zoom level and tile indices covering the plinth.</returns>
        static public (int Zoom, IReadOnlyList<(int TileX, int TileY)> Tiles) ComputePlinthTiles
            (
            double FieldCentreLat,
            double FieldCentreLon,
            double PlinthDiagonalM,
            double MetresPerPixel
            )
        {
            (double MinLat, double MaxLat, double MinLon, double MaxLon) = ComputePlinthSquareBounds(
                FieldCentreLat,
                FieldCentreLon,
                PlinthDiagonalM);

            // Keep plinth imagery at a stable source zoom to avoid tile "jumping"
            // as camera distance changes during 3D zoom in/out.
            int Zoom = Math.Min(WebMercator.PickZoomLevel(PLINTH_TILE_TARGET_METRES_PER_PIXEL, FieldCentreLat), PLINTH_MAX_ZOOM);
            WebMercator.TilesCoveringBounds(
                MinLat, MaxLat, MinLon, MaxLon, Zoom,
                out int TxStart, out int TxEnd, out int TyStart, out int TyEnd);

            int TilesPerAxis = 1 << Zoom;
            List<(int TileX, int TileY)> Tiles = new List<(int TileX, int TileY)>();
            for (int Ty = TyStart; Ty <= TyEnd; Ty++)
            {
                if ((Ty < 0) || (Ty >= TilesPerAxis))
                    continue;

                for (int Tx = TxStart; Tx <= TxEnd; Tx++)
                {
                    int Tcx = ((Tx % TilesPerAxis) + TilesPerAxis) % TilesPerAxis;
                    Tiles.Add((Tcx, Ty));
                    if (Tiles.Count >= MaxTileCount)
                        return (Zoom, Tiles);
                }
            }

            return (Zoom, Tiles);
        }
    }
}
