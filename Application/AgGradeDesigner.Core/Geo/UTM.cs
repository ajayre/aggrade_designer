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



        /// <summary>WGS-84 semi-major axis in metres.</summary>
        private const double SEMI_MAJOR_AXIS_M = 6378137.0;

        /// <summary>WGS-84 inverse flattening (1/f).</summary>
        private const double WGS84_INVERSE_FLATTENING = 298.257223563;

        /// <summary>WGS-84 flattening (f = 1/f).</summary>
        private const double FLATTENING = 1.0 / WGS84_INVERSE_FLATTENING;

        /// <summary>Standard UTM scale factor k0.</summary>
        private const double UTM_SCALE_FACTOR = 0.9996;

        /// <summary>Minimum latitude supported by UTM in decimal degrees.</summary>
        private const double MIN_LATITUDE_DEG = -80.0;

        /// <summary>Maximum latitude supported by UTM in decimal degrees.</summary>
        private const double MAX_LATITUDE_DEG = 84.0;

        /// <summary>Minimum valid UTM zone number.</summary>
        private const int MIN_ZONE = 1;

        /// <summary>Maximum valid UTM zone number.</summary>
        private const int MAX_ZONE = 60;

        /// <summary>Longitude width of one UTM zone in decimal degrees.</summary>
        private const double ZONE_WIDTH_DEG = 6.0;

        /// <summary>False easting applied to all UTM coordinates in metres.</summary>
        private const double FALSE_EASTING_M = 500000.0;

        /// <summary>False northing added for southern-hemisphere coordinates in metres.</summary>
        private const double SOUTHERN_HEMISPHERE_FALSE_NORTHING_M = 10000000.0;

        /// <summary>Longitude offset from zone edge to central meridian in decimal degrees.</summary>
        private const double CENTRAL_MERIDIAN_OFFSET_DEG = 3.0;



        /// <summary>Meridional arc series coefficient for the e² term.</summary>
        private const double M_COEFF_E2 = 1.0 / 4.0;

        /// <summary>Meridional arc series coefficient for the e⁴ term.</summary>
        private const double M_COEFF_E4 = 3.0 / 64.0;

        /// <summary>Meridional arc series coefficient for the e⁶ term.</summary>
        private const double M_COEFF_E6 = 5.0 / 256.0;

        /// <summary>Meridional arc sin(2φ) coefficient for the e² term.</summary>
        private const double M_SIN2_COEFF_E2 = 3.0 / 8.0;

        /// <summary>Meridional arc sin(2φ) coefficient for the e⁴ term.</summary>
        private const double M_SIN2_COEFF_E4 = 3.0 / 32.0;

        /// <summary>Meridional arc sin(2φ) coefficient for the e⁶ term.</summary>
        private const double M_SIN2_COEFF_E6 = 45.0 / 1024.0;

        /// <summary>Meridional arc sin(4φ) coefficient for the e⁴ term.</summary>
        private const double M_SIN4_COEFF_E4 = 15.0 / 256.0;

        /// <summary>Meridional arc sin(4φ) coefficient for the e⁶ term.</summary>
        private const double M_SIN4_COEFF_E6 = 45.0 / 1024.0;

        /// <summary>Meridional arc sin(6φ) coefficient for the e⁶ term.</summary>
        private const double M_SIN6_COEFF_E6 = 35.0 / 3072.0;

        /// <summary>Angle multiplier for the meridional arc sin(2φ) term.</summary>
        private const double MERIDIONAL_SIN_DOUBLE_ANGLE = 2.0;

        /// <summary>Angle multiplier for the meridional arc sin(4φ) term.</summary>
        private const double MERIDIONAL_SIN_QUADRUPLE_ANGLE = 4.0;

        /// <summary>Angle multiplier for the meridional arc sin(6φ) term.</summary>
        private const double MERIDIONAL_SIN_SEXTUPLE_ANGLE = 6.0;



        /// <summary>Footpoint latitude series J1 coefficient for e¹.</summary>
        private const double FOOTPOINT_LATITUDE_J1_COEFF_E1 = 3.0 / 2.0;

        /// <summary>Footpoint latitude series J1 coefficient for e³.</summary>
        private const double FOOTPOINT_LATITUDE_J1_COEFF_E3 = 27.0 / 32.0;

        /// <summary>Footpoint latitude series J2 coefficient for e².</summary>
        private const double FOOTPOINT_LATITUDE_J2_COEFF_E2 = 21.0 / 16.0;

        /// <summary>Footpoint latitude series J2 coefficient for e⁴.</summary>
        private const double FOOTPOINT_LATITUDE_J2_COEFF_E4 = 55.0 / 32.0;

        /// <summary>Footpoint latitude series J3 coefficient for e³.</summary>
        private const double FOOTPOINT_LATITUDE_J3_COEFF_E3 = 151.0 / 96.0;

        /// <summary>Footpoint latitude series J4 coefficient for e⁴.</summary>
        private const double FOOTPOINT_LATITUDE_J4_COEFF_E4 = 1097.0 / 512.0;

        /// <summary>Angle multiplier for the footpoint latitude sin(2μ) term.</summary>
        private const double FOOTPOINT_LATITUDE_SIN_DOUBLE_ANGLE = 2.0;

        /// <summary>Angle multiplier for the footpoint latitude sin(4μ) term.</summary>
        private const double FOOTPOINT_LATITUDE_SIN_QUADRUPLE_ANGLE = 4.0;

        /// <summary>Angle multiplier for the footpoint latitude sin(6μ) term.</summary>
        private const double FOOTPOINT_LATITUDE_SIN_SEXTUPLE_ANGLE = 6.0;

        /// <summary>Angle multiplier for the footpoint latitude sin(8μ) term.</summary>
        private const double FOOTPOINT_LATITUDE_SIN_OCTUPLE_ANGLE = 8.0;



        /// <summary>Divisor for the easting A³ series term.</summary>
        private const double EASTING_A3_DIVISOR = 6.0;

        /// <summary>Divisor for the easting A⁵ series term.</summary>
        private const double EASTING_A5_DIVISOR = 120.0;

        /// <summary>Easting A⁵ series coefficient for the T term.</summary>
        private const double EASTING_A5_T_COEFF = 5.0;

        /// <summary>Easting A⁵ series coefficient for the T² term.</summary>
        private const double EASTING_A5_T2_COEFF = 18.0;

        /// <summary>Easting A⁵ series coefficient for the C term.</summary>
        private const double EASTING_A5_C_COEFF = 72.0;

        /// <summary>Easting A⁵ series coefficient for the e′² term.</summary>
        private const double EASTING_A5_EP2_COEFF = 58.0;

        /// <summary>Exponent used when raising A to the third power in easting series.</summary>
        private const double POWER_A_CUBED = 3.0;

        /// <summary>Exponent used when raising A to the fifth power in easting series.</summary>
        private const double POWER_A_FIFTH = 5.0;



        /// <summary>Divisor for the northing A² series term.</summary>
        private const double NORTHING_A2_DIVISOR = 2.0;

        /// <summary>Divisor for the northing A⁴ series term.</summary>
        private const double NORTHING_A4_DIVISOR = 24.0;

        /// <summary>Divisor for the northing A⁶ series term.</summary>
        private const double NORTHING_A6_DIVISOR = 720.0;

        /// <summary>Northing A⁴ series coefficient for the T term.</summary>
        private const double NORTHING_A4_T_COEFF = 5.0;

        /// <summary>Northing A⁴ series coefficient for the C term.</summary>
        private const double NORTHING_A4_C_COEFF = 9.0;

        /// <summary>Northing A⁴ series coefficient for the C² term.</summary>
        private const double NORTHING_A4_C2_COEFF = 4.0;

        /// <summary>Northing A⁶ series coefficient for the T term.</summary>
        private const double NORTHING_A6_T_COEFF = 61.0;

        /// <summary>Northing A⁶ series coefficient for the T² term.</summary>
        private const double NORTHING_A6_T2_COEFF = 58.0;

        /// <summary>Northing A⁶ series coefficient for the C term.</summary>
        private const double NORTHING_A6_C_COEFF = 600.0;

        /// <summary>Northing A⁶ series coefficient for the e′² term.</summary>
        private const double NORTHING_A6_EP2_COEFF = 330.0;

        /// <summary>Exponent used when raising A to the fourth power in northing series.</summary>
        private const double POWER_A_FOURTH = 4.0;

        /// <summary>Exponent used when raising A to the sixth power in northing series.</summary>
        private const double POWER_A_SIXTH = 6.0;



        /// <summary>Divisor for the inverse latitude D² series term.</summary>
        private const double INVERSE_LATITUDE_D2_DIVISOR = 2.0;

        /// <summary>Divisor for the inverse latitude D⁴ series term.</summary>
        private const double INVERSE_LATITUDE_D4_DIVISOR = 24.0;

        /// <summary>Divisor for the inverse latitude D⁶ series term.</summary>
        private const double INVERSE_LATITUDE_D6_DIVISOR = 720.0;

        /// <summary>Inverse latitude D⁴ series coefficient for the T term.</summary>
        private const double INVERSE_LATITUDE_D4_T_COEFF = 5.0;

        /// <summary>Inverse latitude D⁴ series coefficient for the T² term.</summary>
        private const double INVERSE_LATITUDE_D4_T2_COEFF = 3.0;

        /// <summary>Inverse latitude D⁴ series coefficient for the C term.</summary>
        private const double INVERSE_LATITUDE_D4_C_COEFF = 10.0;

        /// <summary>Inverse latitude D⁴ series coefficient for the C² term.</summary>
        private const double INVERSE_LATITUDE_D4_C2_COEFF = 4.0;

        /// <summary>Inverse latitude D⁴ series coefficient for the e′² term.</summary>
        private const double INVERSE_LATITUDE_D4_EP2_COEFF = 9.0;

        /// <summary>Inverse latitude D⁶ series coefficient for the T term.</summary>
        private const double INVERSE_LATITUDE_D6_T_COEFF = 61.0;

        /// <summary>Inverse latitude D⁶ series coefficient for the T² term.</summary>
        private const double INVERSE_LATITUDE_D6_T2_COEFF = 90.0;

        /// <summary>Inverse latitude D⁶ series coefficient for the C term.</summary>
        private const double INVERSE_LATITUDE_D6_C_COEFF = 298.0;

        /// <summary>Inverse latitude D⁶ series coefficient for the T⁴ term.</summary>
        private const double INVERSE_LATITUDE_D6_T4_COEFF = 45.0;

        /// <summary>Inverse latitude D⁶ series coefficient for the e′² term.</summary>
        private const double INVERSE_LATITUDE_D6_EP2_COEFF = 252.0;

        /// <summary>Inverse latitude D⁶ series coefficient for the C² term.</summary>
        private const double INVERSE_LATITUDE_D6_C2_COEFF = 3.0;

        /// <summary>Exponent used when raising D to the fourth power in inverse latitude series.</summary>
        private const double POWER_D_FOURTH = 4.0;

        /// <summary>Exponent used when raising D to the sixth power in inverse latitude series.</summary>
        private const double POWER_D_SIXTH = 6.0;



        /// <summary>Divisor for the inverse longitude D³ series term.</summary>
        private const double INVERSE_LONGITUDE_D3_DIVISOR = 6.0;

        /// <summary>Divisor for the inverse longitude D⁵ series term.</summary>
        private const double INVERSE_LONGITUDE_D5_DIVISOR = 120.0;

        /// <summary>Inverse longitude D³ series coefficient for the T² term.</summary>
        private const double INVERSE_LONGITUDE_D3_T2_COEFF = 2.0;

        /// <summary>Inverse longitude D⁵ series coefficient for the T term.</summary>
        private const double INVERSE_LONGITUDE_D5_T_COEFF = 28.0;

        /// <summary>Inverse longitude D⁵ series coefficient for the C² term.</summary>
        private const double INVERSE_LONGITUDE_D5_C2_COEFF = 3.0;

        /// <summary>Inverse longitude D⁵ series coefficient for the e′² term.</summary>
        private const double INVERSE_LONGITUDE_D5_EP2_COEFF = 8.0;

        /// <summary>Inverse longitude D⁵ series coefficient for the T⁴ term.</summary>
        private const double INVERSE_LONGITUDE_D5_T4_COEFF = 24.0;

        /// <summary>Exponent used when raising D to the third power in inverse longitude series.</summary>
        private const double POWER_D_CUBED = 3.0;

        /// <summary>Exponent used when raising D to the fifth power in inverse longitude series.</summary>
        private const double POWER_D_FIFTH = 5.0;



        /// <summary>WGS-84 semi-minor axis in metres.</summary>
        static private readonly double SemiMinorAxisM = SEMI_MAJOR_AXIS_M * (1.0 - FLATTENING);

        /// <summary>First eccentricity squared (e²) of the WGS-84 ellipsoid.</summary>
        static private readonly double EccentricitySq = 1.0 - (SemiMinorAxisM * SemiMinorAxisM) / (SEMI_MAJOR_AXIS_M * SEMI_MAJOR_AXIS_M);

        /// <summary>Second eccentricity squared (e′²) of the WGS-84 ellipsoid.</summary>
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

            if (LatitudeDeg < MIN_LATITUDE_DEG || LatitudeDeg > MAX_LATITUDE_DEG)

            {

                throw new ArgumentOutOfRangeException(nameof(LatitudeDeg), "Latitude out of UTM bounds (-80 to 84 degrees).");

            }



            // Normalize longitude to [-180, 180) before zone assignment.

            double LonNorm = ((LongitudeDeg + GeoConstants.DEGREES_IN_HALF_CIRCLE) % GeoConstants.DEGREES_IN_FULL_CIRCLE + GeoConstants.DEGREES_IN_FULL_CIRCLE) % GeoConstants.DEGREES_IN_FULL_CIRCLE - GeoConstants.DEGREES_IN_HALF_CIRCLE;



            int Zone = (int)Math.Floor((LonNorm + GeoConstants.DEGREES_IN_HALF_CIRCLE) / ZONE_WIDTH_DEG) + 1;

            bool IsNorthern = LatitudeDeg >= 0.0;



            double LatRad = DegreesToRadians(LatitudeDeg);

            double LonRad = DegreesToRadians(LonNorm);



            double LonOrigin = (Zone - 1) * ZONE_WIDTH_DEG - GeoConstants.DEGREES_IN_HALF_CIRCLE + CENTRAL_MERIDIAN_OFFSET_DEG;

            double LonOriginRad = DegreesToRadians(LonOrigin);



            double N = SEMI_MAJOR_AXIS_M / Math.Sqrt(1.0 - EccentricitySq * Math.Sin(LatRad) * Math.Sin(LatRad));

            double T = Math.Tan(LatRad) * Math.Tan(LatRad);

            double C = SecondEccentricitySq * Math.Cos(LatRad) * Math.Cos(LatRad);

            double A = Math.Cos(LatRad) * (LonRad - LonOriginRad);



            double EccentricitySqSquared = EccentricitySq * EccentricitySq;

            double EccentricitySqCubed = EccentricitySqSquared * EccentricitySq;

            double M = SEMI_MAJOR_AXIS_M * (

                (1.0 - M_COEFF_E2 * EccentricitySq - M_COEFF_E4 * EccentricitySqSquared - M_COEFF_E6 * EccentricitySqCubed) * LatRad

                - (M_SIN2_COEFF_E2 * EccentricitySq + M_SIN2_COEFF_E4 * EccentricitySqSquared + M_SIN2_COEFF_E6 * EccentricitySqCubed) * Math.Sin(MERIDIONAL_SIN_DOUBLE_ANGLE * LatRad)

                + (M_SIN4_COEFF_E4 * EccentricitySqSquared + M_SIN4_COEFF_E6 * EccentricitySqCubed) * Math.Sin(MERIDIONAL_SIN_QUADRUPLE_ANGLE * LatRad)

                - (M_SIN6_COEFF_E6 * EccentricitySqCubed) * Math.Sin(MERIDIONAL_SIN_SEXTUPLE_ANGLE * LatRad)

            );



            double X = UTM_SCALE_FACTOR * N *

                       (A +

                        (1.0 - T + C) * Math.Pow(A, POWER_A_CUBED) / EASTING_A3_DIVISOR +

                        (EASTING_A5_T_COEFF - EASTING_A5_T2_COEFF * T + T * T + EASTING_A5_C_COEFF * C - EASTING_A5_EP2_COEFF * SecondEccentricitySq) * Math.Pow(A, POWER_A_FIFTH) / EASTING_A5_DIVISOR);



            double Y = UTM_SCALE_FACTOR *

                       (M +

                        N * Math.Tan(LatRad) *

                        (A * A / NORTHING_A2_DIVISOR +

                         (NORTHING_A4_T_COEFF - T + NORTHING_A4_C_COEFF * C + NORTHING_A4_C2_COEFF * C * C) * Math.Pow(A, POWER_A_FOURTH) / NORTHING_A4_DIVISOR +

                         (NORTHING_A6_T_COEFF - NORTHING_A6_T2_COEFF * T + T * T + NORTHING_A6_C_COEFF * C - NORTHING_A6_EP2_COEFF * SecondEccentricitySq) * Math.Pow(A, POWER_A_SIXTH) / NORTHING_A6_DIVISOR));



            double Easting = X + FALSE_EASTING_M;

            double Northing = Y;

            if (!IsNorthern)

            {

                Northing += SOUTHERN_HEMISPHERE_FALSE_NORTHING_M;

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

            if (Zone < MIN_ZONE || Zone > MAX_ZONE)

            {

                throw new ArgumentOutOfRangeException(nameof(Zone), "UTM zone must be between 1 and 60.");

            }



            double X = Easting - FALSE_EASTING_M;

            double Y = Northing;



            if (!IsNorthernHemisphere)

            {

                Y -= SOUTHERN_HEMISPHERE_FALSE_NORTHING_M;

            }



            double LonOrigin = (Zone - 1) * ZONE_WIDTH_DEG - GeoConstants.DEGREES_IN_HALF_CIRCLE + CENTRAL_MERIDIAN_OFFSET_DEG;

            double LonOriginRad = DegreesToRadians(LonOrigin);



            // Iteratively recover latitude from the meridional arc length.

            double M = Y / UTM_SCALE_FACTOR;

            double EccentricitySqSquared = EccentricitySq * EccentricitySq;

            double EccentricitySqCubed = EccentricitySqSquared * EccentricitySq;

            double Mu = M / (SEMI_MAJOR_AXIS_M * (1.0 - M_COEFF_E2 * EccentricitySq - M_COEFF_E4 * EccentricitySqSquared - M_COEFF_E6 * EccentricitySqCubed));



            double E1 = (1.0 - Math.Sqrt(1.0 - EccentricitySq)) / (1.0 + Math.Sqrt(1.0 - EccentricitySq));



            double E1Squared = E1 * E1;

            double E1Cubed = E1Squared * E1;

            double E1Fourth = E1Cubed * E1;

            double J1 = (FOOTPOINT_LATITUDE_J1_COEFF_E1 * E1 - FOOTPOINT_LATITUDE_J1_COEFF_E3 * E1Cubed);

            double J2 = (FOOTPOINT_LATITUDE_J2_COEFF_E2 * E1Squared - FOOTPOINT_LATITUDE_J2_COEFF_E4 * E1Fourth);

            double J3 = (FOOTPOINT_LATITUDE_J3_COEFF_E3 * E1Cubed);

            double J4 = (FOOTPOINT_LATITUDE_J4_COEFF_E4 * E1Fourth);



            double Fp = Mu

                        + J1 * Math.Sin(FOOTPOINT_LATITUDE_SIN_DOUBLE_ANGLE * Mu)

                        + J2 * Math.Sin(FOOTPOINT_LATITUDE_SIN_QUADRUPLE_ANGLE * Mu)

                        + J3 * Math.Sin(FOOTPOINT_LATITUDE_SIN_SEXTUPLE_ANGLE * Mu)

                        + J4 * Math.Sin(FOOTPOINT_LATITUDE_SIN_OCTUPLE_ANGLE * Mu);



            double SinFp = Math.Sin(Fp);

            double CosFp = Math.Cos(Fp);



            double C1 = SecondEccentricitySq * CosFp * CosFp;

            double T1 = Math.Tan(Fp) * Math.Tan(Fp);

            double N1 = SEMI_MAJOR_AXIS_M / Math.Sqrt(1.0 - EccentricitySq * SinFp * SinFp);

            double R1 = N1 * (1.0 - EccentricitySq) / (1.0 - EccentricitySq * SinFp * SinFp);

            double D = X / (N1 * UTM_SCALE_FACTOR);



            double LatRad = Fp

                            - (N1 * Math.Tan(Fp) / R1) *

                              (D * D / INVERSE_LATITUDE_D2_DIVISOR

                               - (INVERSE_LATITUDE_D4_T_COEFF + INVERSE_LATITUDE_D4_T2_COEFF * T1 + INVERSE_LATITUDE_D4_C_COEFF * C1 - INVERSE_LATITUDE_D4_C2_COEFF * C1 * C1 - INVERSE_LATITUDE_D4_EP2_COEFF * SecondEccentricitySq) * Math.Pow(D, POWER_D_FOURTH) / INVERSE_LATITUDE_D4_DIVISOR

                               + (INVERSE_LATITUDE_D6_T_COEFF + INVERSE_LATITUDE_D6_T2_COEFF * T1 + INVERSE_LATITUDE_D6_C_COEFF * C1 + INVERSE_LATITUDE_D6_T4_COEFF * T1 * T1 - INVERSE_LATITUDE_D6_EP2_COEFF * SecondEccentricitySq - INVERSE_LATITUDE_D6_C2_COEFF * C1 * C1) * Math.Pow(D, POWER_D_SIXTH) / INVERSE_LATITUDE_D6_DIVISOR);



            double LonRad = LonOriginRad

                            + (D

                               - (1.0 + INVERSE_LONGITUDE_D3_T2_COEFF * T1 + C1) * Math.Pow(D, POWER_D_CUBED) / INVERSE_LONGITUDE_D3_DIVISOR

                               + (EASTING_A5_T_COEFF - INVERSE_LONGITUDE_D5_C2_COEFF * C1 + INVERSE_LONGITUDE_D5_T_COEFF * T1 - INVERSE_LONGITUDE_D5_C2_COEFF * C1 * C1 + INVERSE_LONGITUDE_D5_EP2_COEFF * SecondEccentricitySq + INVERSE_LONGITUDE_D5_T4_COEFF * T1 * T1) * Math.Pow(D, POWER_D_FIFTH) / INVERSE_LONGITUDE_D5_DIVISOR)

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



            OffsetLongitudeDeg = ((OffsetLongitudeDeg + GeoConstants.DEGREES_IN_HALF_CIRCLE) % GeoConstants.DEGREES_IN_FULL_CIRCLE + GeoConstants.DEGREES_IN_FULL_CIRCLE) % GeoConstants.DEGREES_IN_FULL_CIRCLE - GeoConstants.DEGREES_IN_HALF_CIRCLE;



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

            return Degrees * Math.PI / GeoConstants.DEGREES_IN_HALF_CIRCLE;

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

            return Radians * GeoConstants.DEGREES_IN_HALF_CIRCLE / Math.PI;

        }

    }

}


