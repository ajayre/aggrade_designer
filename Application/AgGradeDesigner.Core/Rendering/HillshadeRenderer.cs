using AgGradeDesigner.Core.Models;

using AgGradeDesigner.Core;

namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// GIS-style hillshade factor for plan-view overlays.
    /// </summary>

    static public class HillshadeRenderer
    {
        /// <summary>
        /// Fixed sun altitude in degrees for hillshade computation.
        /// </summary>

        private const double SUN_ALTITUDE_DEG = 45.0;
        /// <summary>
        /// Fixed sun azimuth in degrees clockwise from north.
        /// </summary>

        private const double SUN_AZIMUTH_DEG = 315.0;
        /// <summary>
        /// Lat/lon offset in degrees for central-difference hillshade gradients.
        /// </summary>

        private const double SAMPLE_DELTA_DEGREES = 0.000012;
        /// <summary>
        /// Right angle in degrees used when converting sun altitude.
        /// </summary>

        private const double RIGHT_ANGLE_DEG = 90.0;
        /// <summary>
        /// Azimuth offset in degrees applied before converting to radians.
        /// </summary>

        private const double SUN_AZIMUTH_OFFSET_DEG = 90.0;
        /// <summary>
        /// Divisor used for symmetric finite-difference gradients.
        /// </summary>

        private const double CENTRAL_DIFFERENCE_DIVISOR = 2.0;
        /// <summary>
        /// Multiplier converting a half circle to a full circle in radians.
        /// </summary>

        private const double FULL_CIRCLE_RADIANS_FACTOR = 2.0;
        /// <summary>
        /// Default box-blur radius in pixels for hillshade grids.
        /// </summary>

        private const int DEFAULT_BLUR_RADIUS = 2;

        /// <summary>
        /// Computes a hillshade modulation factor at a geographic location on the scalar surface.
        /// </summary>
        /// <param name="Surface">Sampled elevation field.</param>
        /// <param name="ContentView">Scalar channel to shade (existing, proposed, or cut/fill).</param>
        /// <param name="CentreLat">Map centre latitude for longitude scale correction.</param>
        /// <param name="Lat">Sample latitude in decimal degrees.</param>
        /// <param name="Lon">Sample longitude in decimal degrees.</param>
        /// <param name="ZScale">Vertical exaggeration applied to elevation gradients.</param>
        /// <returns>Hillshade factor in [0, 1], or NaN when samples are invalid.</returns>
        static public double ComputeFactorAtLocation
            (
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double CentreLat,
            double Lat,
            double Lon,
            double ZScale
            )
        {
            // Central-difference gradients in a small lat/lon neighbourhood.
            double E = Surface.SampleScalar(ContentView, Lat, Lon);
            double ExPlus = Surface.SampleScalar(ContentView, Lat, Lon + SAMPLE_DELTA_DEGREES);
            double ExMinus = Surface.SampleScalar(ContentView, Lat, Lon - SAMPLE_DELTA_DEGREES);
            double EyPlus = Surface.SampleScalar(ContentView, Lat + SAMPLE_DELTA_DEGREES, Lon);
            double EyMinus = Surface.SampleScalar(ContentView, Lat - SAMPLE_DELTA_DEGREES, Lon);

            if (double.IsNaN(E) || double.IsNaN(ExPlus) || double.IsNaN(ExMinus) || double.IsNaN(EyPlus) || double.IsNaN(EyMinus))
                return double.NaN;

            double MetresPerDegreeLat = FieldConstants.METRES_PER_DEGREE_LAT;
            double MetresPerDegreeLon = MetresPerDegreeLat * Math.Cos(CentreLat * Math.PI / FieldConstants.DEGREES_PER_RADIAN);
            double DxM = SAMPLE_DELTA_DEGREES * MetresPerDegreeLon;
            double DyM = SAMPLE_DELTA_DEGREES * MetresPerDegreeLat;

            double DzDx = (ExPlus - ExMinus) / (CENTRAL_DIFFERENCE_DIVISOR * DxM) * ZScale;
            double DzDy = (EyPlus - EyMinus) / (CENTRAL_DIFFERENCE_DIVISOR * DyM) * ZScale;

            return ComputeFactorFromGradients(DzDx, DzDy);
        }

        /// <summary>
        /// Applies a box blur to a hillshade factor grid to soften high-frequency noise.
        /// </summary>
        /// <param name="Shades">Flat hillshade factors indexed row-major; NaN marks invalid cells.</param>
        /// <param name="Width">Grid width in pixels.</param>
        /// <param name="Height">Grid height in pixels.</param>
        /// <param name="Radius">Blur kernel radius in pixels.</param>
        static public void BoxBlurShadeGrid
            (
            double[] Shades,
            int Width,
            int Height,
            int Radius = DEFAULT_BLUR_RADIUS
            )
        {
            if (Radius <= 0)
                return;

            double[] Copy = new double[Shades.Length];
            Array.Copy(Shades, Copy, Shades.Length);

            for (int Y = 0; Y < Height; Y++)
            {
                for (int X = 0; X < Width; X++)
                {
                    int Idx = Y * Width + X;
                    if (double.IsNaN(Copy[Idx]))
                        continue;

                    double Sum = 0;
                    int Count = 0;
                    for (int Dy = -Radius; Dy <= Radius; Dy++)
                    {
                        for (int Dx = -Radius; Dx <= Radius; Dx++)
                        {
                            int Nx = X + Dx;
                            int Ny = Y + Dy;
                            if (Nx < 0 || Ny < 0 || Nx >= Width || Ny >= Height)
                                continue;

                            double Neighbour = Copy[Ny * Width + Nx];
                            if (double.IsNaN(Neighbour))
                                continue;

                            Sum += Neighbour;
                            Count++;
                        }
                    }

                    if (Count > 0)
                        Shades[Idx] = Sum / Count;
                }
            }
        }

        /// <summary>
        /// Converts surface gradients to a hillshade factor using a fixed sun vector.
        /// </summary>
        /// <param name="DzDx">Elevation gradient in the east direction (metres per metre).</param>
        /// <param name="DzDy">Elevation gradient in the north direction (metres per metre).</param>
        /// <returns>Shade factor clamped to [0, 1].</returns>
        static private double ComputeFactorFromGradients
            (
            double DzDx,
            double DzDy
            )
        {
            double Slope = Math.Atan(Math.Sqrt(DzDx * DzDx + DzDy * DzDy));
            double Aspect = Math.Atan2(DzDy, -DzDx);
            if (Aspect < 0)
                Aspect += FULL_CIRCLE_RADIANS_FACTOR * Math.PI;

            // Fixed northwest sun position for consistent plan-view relief shading.
            double Zenith = (RIGHT_ANGLE_DEG - SUN_ALTITUDE_DEG) * Math.PI / FieldConstants.DEGREES_PER_RADIAN;
            double Azimuth = (FieldConstants.DEGREES_FULL_CIRCLE - SUN_AZIMUTH_DEG + SUN_AZIMUTH_OFFSET_DEG) * Math.PI / FieldConstants.DEGREES_PER_RADIAN;

            double Shade = Math.Sin(Zenith) * Math.Cos(Slope)
                + Math.Cos(Zenith) * Math.Sin(Slope) * Math.Cos(Azimuth - Aspect);

            return Math.Clamp(Shade, 0.0, 1.0);
        }
    }
}

