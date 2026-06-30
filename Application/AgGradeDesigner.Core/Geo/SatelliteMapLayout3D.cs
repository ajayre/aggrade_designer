namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Selects Web Mercator tiles for 3D draped satellite imagery based on orbit camera pitch.
    /// </summary>
    static public class SatelliteMapLayout3D
    {
        /// <summary>Pitch above this angle fills the entire viewport with tiles.</summary>
        public const double FullViewportPitchThresholdDeg = 30.0;

        private const int FullViewportSampleCols = 14;
        private const int FullViewportSampleRows = 14;
        private const int ForegroundSampleCols = 12;
        private const int ForegroundMaxRows = 10;
        private const double GeoPadViewportFraction = 0.6;
        private const double ForegroundPadViewportFraction = 0.25;
        private const double ForegroundCameraReachFraction = 0.35;
        private const int MaxTileCount = 256;

        /// <summary>
        /// Computes tile indices visible on the flat ground plane for a 3D orbit view.
        /// </summary>
        /// <param name="CentreLat">Orbit pivot latitude in degrees.</param>
        /// <param name="CentreLon">Orbit pivot longitude in degrees.</param>
        /// <param name="HeadingDeg">Orbit heading in degrees.</param>
        /// <param name="PitchDeg">Orbit pitch in degrees above the horizon.</param>
        /// <param name="DistanceM">Camera distance from the pivot in metres.</param>
        /// <param name="ViewportWidth">Viewport width in pixels.</param>
        /// <param name="ViewportHeight">Viewport height in pixels.</param>
        /// <param name="MetresPerPixel">Ground metres per screen pixel at the pivot.</param>
        /// <param name="FieldMinLat">Southern field extent in degrees.</param>
        /// <param name="FieldMaxLat">Northern field extent in degrees.</param>
        /// <param name="FieldMinLon">Western field extent in degrees.</param>
        /// <param name="FieldMaxLon">Eastern field extent in degrees.</param>
        /// <returns>Zoom level and distinct tile column/row indices to fetch.</returns>
        static public (int Zoom, IReadOnlyList<(int TileX, int TileY)> Tiles) ComputeVisibleTiles
            (
            double CentreLat,
            double CentreLon,
            double HeadingDeg,
            double PitchDeg,
            double DistanceM,
            float ViewportWidth,
            float ViewportHeight,
            double MetresPerPixel,
            double FieldMinLat,
            double FieldMaxLat,
            double FieldMinLon,
            double FieldMaxLon
            )
        {
            if ((ViewportWidth <= 0) || (ViewportHeight <= 0) || (DistanceM <= 0))
                return (14, Array.Empty<(int, int)>());

            double HeadingRad = HeadingDeg * Math.PI / 180.0;
            double PitchRad = PitchDeg * Math.PI / 180.0;
            int Zoom = WebMercator.PickZoomLevel(MetresPerPixel, CentreLat);

            double MinLat = FieldMinLat;
            double MaxLat = FieldMaxLat;
            double MinLon = FieldMinLon;
            double MaxLon = FieldMaxLon;
            bool HaveBounds = false;

            if (PitchDeg > FullViewportPitchThresholdDeg)
                HaveBounds = TryExpandFromViewportSamples(
                    CentreLat, CentreLon, HeadingRad, PitchRad, DistanceM,
                    ViewportWidth, ViewportHeight,
                    FullViewportSampleCols, FullViewportSampleRows,
                    0.0f, ViewportHeight,
                    ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);
            else
            {
                HaveBounds = TryExpandFromViewportSamples(
                    CentreLat, CentreLon, HeadingRad, PitchRad, DistanceM,
                    ViewportWidth, ViewportHeight,
                    ForegroundSampleCols, ForegroundMaxRows,
                    ViewportHeight * 0.35f, ViewportHeight,
                    ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);

                ExpandTowardCamera(
                    CentreLat, CentreLon, HeadingRad, DistanceM,
                    FieldMinLat, FieldMaxLat, FieldMinLon, FieldMaxLon,
                    ref MinLat, ref MaxLat, ref MinLon, ref MaxLon,
                    ref HaveBounds);
            }

            IncludeFieldBounds(FieldMinLat, FieldMaxLat, FieldMinLon, FieldMaxLon, ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);
            HaveBounds = true;

            double PadFraction = PitchDeg > FullViewportPitchThresholdDeg
                ? GeoPadViewportFraction
                : ForegroundPadViewportFraction;
            PadGeoBoundsMetres(
                CentreLat,
                Math.Max(ViewportWidth, ViewportHeight) * MetresPerPixel * PadFraction,
                ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);

            if (!HaveBounds)
                return (Zoom, Array.Empty<(int, int)>());

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

        /// <summary>
        /// Geographic bounds of the ground plane visible in the viewport (for solid fill behind tiles).
        /// </summary>
        static public (double MinLat, double MaxLat, double MinLon, double MaxLon) ComputeVisibleGroundPlaneBounds
            (
            double CentreLat,
            double CentreLon,
            double HeadingDeg,
            double PitchDeg,
            double DistanceM,
            float ViewportWidth,
            float ViewportHeight,
            double MetresPerPixel,
            double FieldMinLat,
            double FieldMaxLat,
            double FieldMinLon,
            double FieldMaxLon
            )
        {
            double MinLat = FieldMinLat;
            double MaxLat = FieldMaxLat;
            double MinLon = FieldMinLon;
            double MaxLon = FieldMaxLon;

            double HeadingRad = HeadingDeg * Math.PI / 180.0;
            double PitchRad = PitchDeg * Math.PI / 180.0;

            TryExpandFromViewportSamples(
                CentreLat, CentreLon, HeadingRad, PitchRad, DistanceM,
                ViewportWidth, ViewportHeight,
                FullViewportSampleCols, FullViewportSampleRows,
                0.0f, ViewportHeight,
                ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);

            IncludeFieldBounds(FieldMinLat, FieldMaxLat, FieldMinLon, FieldMaxLon, ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);

            if (PitchDeg <= FullViewportPitchThresholdDeg)
            {
                ExpandTowardHorizon(
                    CentreLat, CentreLon, HeadingRad, PitchRad, DistanceM,
                    ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);
            }

            PadGeoBoundsMetres(
                CentreLat,
                Math.Max(ViewportWidth, ViewportHeight) * MetresPerPixel * 0.15,
                ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);

            return (MinLat, MaxLat, MinLon, MaxLon);
        }

        /// <summary>
        /// Extends bounds toward the horizon so the fill plane reaches the distant visible ground.
        /// </summary>
        static private void ExpandTowardHorizon
            (
            double CentreLat,
            double CentreLon,
            double HeadingRad,
            double PitchRad,
            double DistanceM,
            ref double MinLat,
            ref double MaxLat,
            ref double MinLon,
            ref double MaxLon
            )
        {
            UTM.UTMCoordinate Pivot = UTM.FromLatLon(CentreLat, CentreLon);
            double SinP = Math.Max(0.05, Math.Sin(PitchRad));
            double ReachM = DistanceM * (1.5 / SinP);
            double AwayEx = Math.Sin(HeadingRad) * ReachM;
            double AwayEy = Math.Cos(HeadingRad) * ReachM;

            double MinE = double.MaxValue;
            double MaxE = double.MinValue;
            double MinN = double.MaxValue;
            double MaxN = double.MinValue;

            UTM.UTMCoordinate Sw = UTM.FromLatLon(MinLat, MinLon);
            UTM.UTMCoordinate Ne = UTM.FromLatLon(MaxLat, MaxLon);
            (double E, double N)[] Corners =
            {
                (Sw.Easting, Sw.Northing),
                (Ne.Easting, Sw.Northing),
                (Sw.Easting, Ne.Northing),
                (Ne.Easting, Ne.Northing),
                (Sw.Easting + AwayEx, Sw.Northing + AwayEy),
                (Ne.Easting + AwayEx, Sw.Northing + AwayEy),
                (Sw.Easting + AwayEx, Ne.Northing + AwayEy),
                (Ne.Easting + AwayEx, Ne.Northing + AwayEy)
            };

            foreach ((double E, double N) Corner in Corners)
            {
                MinE = Math.Min(MinE, Corner.E);
                MaxE = Math.Max(MaxE, Corner.E);
                MinN = Math.Min(MinN, Corner.N);
                MaxN = Math.Max(MaxN, Corner.N);
            }

            UTM.ToLatLon(Pivot.Zone, Pivot.IsNorthernHemisphere, MinE, MinN, out MinLat, out MinLon);
            UTM.ToLatLon(Pivot.Zone, Pivot.IsNorthernHemisphere, MaxE, MaxN, out MaxLat, out MaxLon);
        }

        /// <summary>
        /// Unprojects a screen point onto the flat ground plane (Ez = 0) for tile footprint selection.
        /// </summary>
        static public bool TryUnprojectGround
            (
            double CentreLat,
            double CentreLon,
            double HeadingRad,
            double PitchRad,
            double DistanceM,
            float ViewportWidth,
            float ViewportHeight,
            float ScreenX,
            float ScreenY,
            out double Lat,
            out double Lon
            )
        {
            Lat = 0;
            Lon = 0;

            double SinP = Math.Sin(PitchRad);
            if (Math.Abs(SinP) < 1e-6)
                return false;

            double Scale = ViewportHeight / DistanceM;
            if (Scale <= 0)
                return false;

            double MidX = ViewportWidth * 0.5;
            double MidY = ViewportHeight * 0.5;
            double Rx = (ScreenX - MidX) / Scale;
            double Up = (MidY - ScreenY) / Scale;
            double Fy = -Up / SinP;

            if ((Fy * Math.Cos(PitchRad)) <= 0)
                return false;

            double CosH = Math.Cos(HeadingRad);
            double SinH = Math.Sin(HeadingRad);
            double Ex = (Rx * CosH) + (Fy * SinH);
            double Ey = (-Rx * SinH) + (Fy * CosH);

            UTM.UTMCoordinate CentreUtm = UTM.FromLatLon(CentreLat, CentreLon);
            UTM.ToLatLon(
                CentreUtm.Zone,
                CentreUtm.IsNorthernHemisphere,
                CentreUtm.Easting + Ex,
                CentreUtm.Northing + Ey,
                out Lat,
                out Lon);
            return true;
        }

        static private bool TryExpandFromViewportSamples
            (
            double CentreLat,
            double CentreLon,
            double HeadingRad,
            double PitchRad,
            double DistanceM,
            float ViewportWidth,
            float ViewportHeight,
            int SampleCols,
            int SampleRows,
            float SampleMinY,
            float SampleMaxY,
            ref double MinLat,
            ref double MaxLat,
            ref double MinLon,
            ref double MaxLon
            )
        {
            bool Found = false;
            for (int Row = 0; Row <= SampleRows; Row++)
            {
                float ScreenY = SampleMinY + ((SampleMaxY - SampleMinY) * Row / Math.Max(1, SampleRows));
                for (int Col = 0; Col <= SampleCols; Col++)
                {
                    float ScreenX = ViewportWidth * Col / Math.Max(1, SampleCols);
                    if (!TryUnprojectGround(
                            CentreLat, CentreLon, HeadingRad, PitchRad, DistanceM,
                            ViewportWidth, ViewportHeight, ScreenX, ScreenY,
                            out double Lat, out double Lon))
                        continue;

                    IncludePoint(Lat, Lon, ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);
                    Found = true;
                }
            }

            return Found;
        }

        static private void ExpandTowardCamera
            (
            double CentreLat,
            double CentreLon,
            double HeadingRad,
            double DistanceM,
            double FieldMinLat,
            double FieldMaxLat,
            double FieldMinLon,
            double FieldMaxLon,
            ref double MinLat,
            ref double MaxLat,
            ref double MinLon,
            ref double MaxLon,
            ref bool HaveBounds
            )
        {
            UTM.UTMCoordinate Pivot = UTM.FromLatLon(CentreLat, CentreLon);
            double ReachM = DistanceM * ForegroundCameraReachFraction;
            double TowardEx = -Math.Sin(HeadingRad) * ReachM;
            double TowardEy = -Math.Cos(HeadingRad) * ReachM;

            UTM.UTMCoordinate FieldSw = UTM.FromLatLon(FieldMinLat, FieldMinLon);
            UTM.UTMCoordinate FieldNe = UTM.FromLatLon(FieldMaxLat, FieldMaxLon);
            double MinE = Math.Min(FieldSw.Easting, FieldNe.Easting);
            double MaxE = Math.Max(FieldSw.Easting, FieldNe.Easting);
            double MinN = Math.Min(FieldSw.Northing, FieldNe.Northing);
            double MaxN = Math.Max(FieldSw.Northing, FieldNe.Northing);

            (double E, double N)[] Corners =
            {
                (MinE, MinN),
                (MaxE, MinN),
                (MinE, MaxN),
                (MaxE, MaxN),
                (MinE + TowardEx, MinN + TowardEy),
                (MaxE + TowardEx, MinN + TowardEy),
                (MinE + TowardEx, MaxN + TowardEy),
                (MaxE + TowardEx, MaxN + TowardEy)
            };

            foreach ((double E, double N) Corner in Corners)
            {
                UTM.ToLatLon(Pivot.Zone, Pivot.IsNorthernHemisphere, Corner.E, Corner.N, out double Lat, out double Lon);
                IncludePoint(Lat, Lon, ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);
            }

            HaveBounds = true;
        }

        static private void IncludeFieldBounds
            (
            double FieldMinLat,
            double FieldMaxLat,
            double FieldMinLon,
            double FieldMaxLon,
            ref double MinLat,
            ref double MaxLat,
            ref double MinLon,
            ref double MaxLon
            )
        {
            IncludePoint(FieldMinLat, FieldMinLon, ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);
            IncludePoint(FieldMaxLat, FieldMaxLon, ref MinLat, ref MaxLat, ref MinLon, ref MaxLon);
        }

        static private void IncludePoint
            (
            double Lat,
            double Lon,
            ref double MinLat,
            ref double MaxLat,
            ref double MinLon,
            ref double MaxLon
            )
        {
            MinLat = Math.Min(MinLat, Lat);
            MaxLat = Math.Max(MaxLat, Lat);
            MinLon = Math.Min(MinLon, Lon);
            MaxLon = Math.Max(MaxLon, Lon);
        }

        static private void PadGeoBoundsMetres
            (
            double CentreLat,
            double PadM,
            ref double MinLat,
            ref double MaxLat,
            ref double MinLon,
            ref double MaxLon
            )
        {
            if (PadM <= 0)
                return;

            double MetresPerDegreeLat = 111_320.0;
            double MetresPerDegreeLon = MetresPerDegreeLat * Math.Max(0.01, Math.Cos(CentreLat * Math.PI / 180.0));
            MinLat -= PadM / MetresPerDegreeLat;
            MaxLat += PadM / MetresPerDegreeLat;
            MinLon -= PadM / MetresPerDegreeLon;
            MaxLon += PadM / MetresPerDegreeLon;
        }
    }
}
