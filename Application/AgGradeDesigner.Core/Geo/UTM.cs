using System;

namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Utility methods for converting between WGS‑84 latitude/longitude and
    /// UTM (Universal Transverse Mercator) easting/northing coordinates.
    ///
    /// Assumptions / simplifications:
    /// - WGS‑84 ellipsoid is assumed.
    /// - Standard UTM scale factor k0 = 0.9996 is used.
    /// - Special-case longitude zone tweaks for Norway/Svalbard are ignored.
    /// - Intended use is over relatively small working areas (e.g. a few km),
    ///   which is much larger than the 2,640 ft × 2,640 ft requirement.
    /// </summary>
    static public class UTM
    {
        /// <summary>
        /// Represents a UTM coordinate (zone, hemisphere, easting, northing).
        /// </summary>
        public struct UTMCoordinate
        {
            /// <summary>UTM zone number (1–60).</summary>
            public int Zone;

            /// <summary>True when the point lies in the northern hemisphere.</summary>
            public bool IsNorthernHemisphere;

            /// <summary>Easting in metres from the zone central meridian false easting.</summary>
            public double Easting;

            /// <summary>Northing in metres from the equator, including southern false northing when applicable.</summary>
            public double Northing;

            /// <summary>
            /// Creates a UTM coordinate with zone, hemisphere, easting, and northing.
            /// </summary>
            /// <param name="Zone">UTM zone number.</param>
            /// <param name="IsNorthernHemisphere">True for northern hemisphere.</param>
            /// <param name="Easting">Easting in metres.</param>
            /// <param name="Northing">Northing in metres.</param>
            public UTMCoordinate
                (
                int Zone,
                bool IsNorthernHemisphere,
                double Easting,
                double Northing
                )
            {
                this.Zone = Zone;
                this.IsNorthernHemisphere = IsNorthernHemisphere;
                this.Easting = Easting;
                this.Northing = Northing;
            }
        }

        private const double SEMI_MAJOR_AXIS_M = 6378137.0;
        private const double FLATTENING = 1.0 / 298.257223563;
        private const double UTM_SCALE_FACTOR = 0.9996;

        static private readonly double SemiMinorAxisM = SEMI_MAJOR_AXIS_M * (1.0 - FLATTENING);
        static private readonly double EccentricitySq = 1.0 - (SemiMinorAxisM * SemiMinorAxisM) / (SEMI_MAJOR_AXIS_M * SEMI_MAJOR_AXIS_M);
        static private readonly double SecondEccentricitySq = EccentricitySq / (1.0 - EccentricitySq);

        /// <summary>
        /// Converts WGS‑84 latitude/longitude to a UTM coordinate.
        /// </summary>
        /// <param name="LatitudeDeg">Latitude in decimal degrees.</param>
        /// <param name="LongitudeDeg">Longitude in decimal degrees.</param>
        /// <returns>UTM coordinate (zone, hemisphere, easting, northing).</returns>
        static public UTMCoordinate FromLatLon
            (
            double LatitudeDeg,
            double LongitudeDeg
            )
        {
            if (LatitudeDeg < -80.0 || LatitudeDeg > 84.0)
            {
                throw new ArgumentOutOfRangeException(nameof(LatitudeDeg), "Latitude out of UTM bounds (-80 to 84 degrees).");
            }

            // Normalize longitude to [-180, 180) before zone assignment.
            double LonNorm = ((LongitudeDeg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;

            int Zone = (int)Math.Floor((LonNorm + 180.0) / 6.0) + 1;
            bool IsNorthern = LatitudeDeg >= 0.0;

            double LatRad = DegreesToRadians(LatitudeDeg);
            double LonRad = DegreesToRadians(LonNorm);

            double LonOrigin = (Zone - 1) * 6 - 180 + 3;
            double LonOriginRad = DegreesToRadians(LonOrigin);

            double N = SEMI_MAJOR_AXIS_M / Math.Sqrt(1.0 - EccentricitySq * Math.Sin(LatRad) * Math.Sin(LatRad));
            double T = Math.Tan(LatRad) * Math.Tan(LatRad);
            double C = SecondEccentricitySq * Math.Cos(LatRad) * Math.Cos(LatRad);
            double A = Math.Cos(LatRad) * (LonRad - LonOriginRad);

            double M = SEMI_MAJOR_AXIS_M * (
                (1.0 - EccentricitySq / 4.0 - 3.0 * EccentricitySq * EccentricitySq / 64.0 - 5.0 * EccentricitySq * EccentricitySq * EccentricitySq / 256.0) * LatRad
                - (3.0 * EccentricitySq / 8.0 + 3.0 * EccentricitySq * EccentricitySq / 32.0 + 45.0 * EccentricitySq * EccentricitySq * EccentricitySq / 1024.0) * Math.Sin(2.0 * LatRad)
                + (15.0 * EccentricitySq * EccentricitySq / 256.0 + 45.0 * EccentricitySq * EccentricitySq * EccentricitySq / 1024.0) * Math.Sin(4.0 * LatRad)
                - (35.0 * EccentricitySq * EccentricitySq * EccentricitySq / 3072.0) * Math.Sin(6.0 * LatRad)
            );

            double X = UTM_SCALE_FACTOR * N *
                       (A +
                        (1.0 - T + C) * Math.Pow(A, 3) / 6.0 +
                        (5.0 - 18.0 * T + T * T + 72.0 * C - 58.0 * SecondEccentricitySq) * Math.Pow(A, 5) / 120.0);

            double Y = UTM_SCALE_FACTOR *
                       (M +
                        N * Math.Tan(LatRad) *
                        (A * A / 2.0 +
                         (5.0 - T + 9.0 * C + 4.0 * C * C) * Math.Pow(A, 4) / 24.0 +
                         (61.0 - 58.0 * T + T * T + 600.0 * C - 330.0 * SecondEccentricitySq) * Math.Pow(A, 6) / 720.0));

            double Easting = X + 500000.0;
            double Northing = Y;
            if (!IsNorthern)
            {
                Northing += 10000000.0;
            }

            return new UTMCoordinate(Zone, IsNorthern, Easting, Northing);
        }

        /// <summary>
        /// Converts a UTM coordinate back to WGS‑84 latitude/longitude.
        /// </summary>
        /// <param name="Zone">UTM zone.</param>
        /// <param name="IsNorthernHemisphere">True if in northern hemisphere, false if southern.</param>
        /// <param name="Easting">Easting in meters.</param>
        /// <param name="Northing">Northing in meters (includes false northing if southern hemisphere).</param>
        /// <param name="LatitudeDeg">Output latitude in decimal degrees.</param>
        /// <param name="LongitudeDeg">Output longitude in decimal degrees.</param>
        static public void ToLatLon
            (
            int Zone,
            bool IsNorthernHemisphere,
            double Easting,
            double Northing,
            out double LatitudeDeg,
            out double LongitudeDeg
            )
        {
            if (Zone < 1 || Zone > 60)
            {
                throw new ArgumentOutOfRangeException(nameof(Zone), "UTM zone must be between 1 and 60.");
            }

            double X = Easting - 500000.0;
            double Y = Northing;

            if (!IsNorthernHemisphere)
            {
                Y -= 10000000.0;
            }

            double LonOrigin = (Zone - 1) * 6 - 180 + 3;
            double LonOriginRad = DegreesToRadians(LonOrigin);

            // Iteratively recover latitude from the meridional arc length.
            double M = Y / UTM_SCALE_FACTOR;
            double Mu = M / (SEMI_MAJOR_AXIS_M * (1.0 - EccentricitySq / 4.0 - 3.0 * EccentricitySq * EccentricitySq / 64.0 - 5.0 * EccentricitySq * EccentricitySq * EccentricitySq / 256.0));

            double E1 = (1.0 - Math.Sqrt(1.0 - EccentricitySq)) / (1.0 + Math.Sqrt(1.0 - EccentricitySq));

            double J1 = (3.0 * E1 / 2.0 - 27.0 * E1 * E1 * E1 / 32.0);
            double J2 = (21.0 * E1 * E1 / 16.0 - 55.0 * E1 * E1 * E1 * E1 / 32.0);
            double J3 = (151.0 * E1 * E1 * E1 / 96.0);
            double J4 = (1097.0 * E1 * E1 * E1 * E1 / 512.0);

            double Fp = Mu
                        + J1 * Math.Sin(2.0 * Mu)
                        + J2 * Math.Sin(4.0 * Mu)
                        + J3 * Math.Sin(6.0 * Mu)
                        + J4 * Math.Sin(8.0 * Mu);

            double SinFp = Math.Sin(Fp);
            double CosFp = Math.Cos(Fp);

            double C1 = SecondEccentricitySq * CosFp * CosFp;
            double T1 = Math.Tan(Fp) * Math.Tan(Fp);
            double N1 = SEMI_MAJOR_AXIS_M / Math.Sqrt(1.0 - EccentricitySq * SinFp * SinFp);
            double R1 = N1 * (1.0 - EccentricitySq) / (1.0 - EccentricitySq * SinFp * SinFp);
            double D = X / (N1 * UTM_SCALE_FACTOR);

            double LatRad = Fp
                            - (N1 * Math.Tan(Fp) / R1) *
                              (D * D / 2.0
                               - (5.0 + 3.0 * T1 + 10.0 * C1 - 4.0 * C1 * C1 - 9.0 * SecondEccentricitySq) * Math.Pow(D, 4) / 24.0
                               + (61.0 + 90.0 * T1 + 298.0 * C1 + 45.0 * T1 * T1 - 252.0 * SecondEccentricitySq - 3.0 * C1 * C1) * Math.Pow(D, 6) / 720.0);

            double LonRad = LonOriginRad
                            + (D
                               - (1.0 + 2.0 * T1 + C1) * Math.Pow(D, 3) / 6.0
                               + (5.0 - 2.0 * C1 + 28.0 * T1 - 3.0 * C1 * C1 + 8.0 * SecondEccentricitySq + 24.0 * T1 * T1) * Math.Pow(D, 5) / 120.0)
                              / CosFp;

            LatitudeDeg = RadiansToDegrees(LatRad);
            LongitudeDeg = RadiansToDegrees(LonRad);
        }

        /// <summary>
        /// Offsets a WGS-84 latitude/longitude location by UTM easting/northing metres
        /// and returns the resulting latitude/longitude location.
        /// </summary>
        /// <param name="Location">Location to offset.</param>
        /// <param name="EastingM">Offset in meters in +UTM easting direction (west is negative).</param>
        /// <param name="NorthingM">Offset in meters in +UTM northing direction (south is negative).</param>
        /// <returns>Offset location as latitude/longitude.</returns>
        static public Coordinate OffsetLocation
            (
            Coordinate Location,
            double EastingM,
            double NorthingM
            )
        {
            if ((EastingM == 0) && (NorthingM == 0)) return Location;

            return OffsetLocation(Location.Latitude, Location.Longitude, EastingM, NorthingM);
        }

        /// <summary>
        /// Offsets a WGS-84 latitude/longitude location by UTM easting/northing metres
        /// and returns the resulting latitude/longitude location.
        /// </summary>
        /// <param name="LatitudeDeg">Start latitude in decimal degrees.</param>
        /// <param name="LongitudeDeg">Start longitude in decimal degrees.</param>
        /// <param name="EastingM">Offset in meters in +UTM easting direction (west is negative).</param>
        /// <param name="NorthingM">Offset in meters in +UTM northing direction (south is negative).</param>
        /// <returns>Offset location as latitude/longitude.</returns>
        static public Coordinate OffsetLocation
            (
            double LatitudeDeg,
            double LongitudeDeg,
            double EastingM,
            double NorthingM
            )
        {
            if ((EastingM == 0) && (NorthingM == 0)) return new Coordinate(LatitudeDeg, LongitudeDeg);

            UTMCoordinate Start = FromLatLon(LatitudeDeg, LongitudeDeg);

            double OffsetEasting = Start.Easting + EastingM;
            double OffsetNorthing = Start.Northing + NorthingM;

            ToLatLon(
                Start.Zone,
                Start.IsNorthernHemisphere,
                OffsetEasting,
                OffsetNorthing,
                out double OffsetLatitudeDeg,
                out double OffsetLongitudeDeg);

            OffsetLongitudeDeg = ((OffsetLongitudeDeg + 180.0) % 360.0 + 360.0) % 360.0 - 180.0;

            return new Coordinate(OffsetLatitudeDeg, OffsetLongitudeDeg);
        }

        /// <summary>
        /// Converts decimal degrees to radians.
        /// </summary>
        /// <param name="Degrees">Angle in decimal degrees.</param>
        /// <returns>Angle in radians.</returns>
        static private double DegreesToRadians
            (
            double Degrees
            )
        {
            return Degrees * Math.PI / 180.0;
        }

        /// <summary>
        /// Converts radians to decimal degrees.
        /// </summary>
        /// <param name="Radians">Angle in radians.</param>
        /// <returns>Angle in decimal degrees.</returns>
        static private double RadiansToDegrees
            (
            double Radians
            )
        {
            return Radians * 180.0 / Math.PI;
        }
    }
}
