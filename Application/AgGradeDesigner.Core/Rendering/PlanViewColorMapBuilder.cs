using AgGradeDesigner.Core.Color;

using AgGradeDesigner.Core.Models;



using SkiaSharp;



namespace AgGradeDesigner.Core.Rendering

{

    /// <summary>

    /// Builds a raster colour map for smooth 2D plan-view rendering (no quad seams).

    /// </summary>

    static public class PlanViewColorMapBuilder

    {

        private const int SUPERSAMPLE_FACTOR = 4;



        /// <summary>

        /// Builds a supersampled colour map bitmap for the scalar field, optionally with hillshade.

        /// </summary>

        /// <param name="Surface">Sampled elevation field.</param>

        /// <param name="ContentView">Scalar channel to colour (existing, proposed, or cut/fill).</param>

        /// <param name="VMin">Minimum value of the display range.</param>

        /// <param name="VMax">Maximum value of the display range.</param>

        /// <param name="ApplyHillshade">When true, modulates colours with a hillshade overlay.</param>

        /// <returns>RGBA bitmap sized to supersample the surface grid.</returns>

        static public SKBitmap Build

            (

            FieldSurfaceSampler Surface,

            FieldContentView ContentView,

            double VMin,

            double VMax,

            bool ApplyHillshade,

            IProgress<int>? Progress = null

            )

        {

            int Width = Math.Max(2, (Surface.SampleWidth - 1) * SUPERSAMPLE_FACTOR + 1);

            int Height = Math.Max(2, (Surface.SampleHeight - 1) * SUPERSAMPLE_FACTOR + 1);

            SKBitmap Bitmap = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);

            double[]? Shades = ApplyHillshade ? new double[Width * Height] : null;



            double CentreLat = (Surface.MinLat + Surface.MaxLat) * 0.5;

            double ZScale = ApplyHillshade ? Surface.ComputeHillshadeZScale() : 1.0;



            // First pass: classify scalars into band colours and optionally record hillshade factors.

            for (int Y = 0; Y < Height; Y++)

            {

                for (int X = 0; X < Width; X++)

                {

                    double Lon = Surface.MinLon + (Surface.MaxLon - Surface.MinLon) * X / (Width - 1);

                    double Lat = Surface.MaxLat - (Surface.MaxLat - Surface.MinLat) * Y / (Height - 1);

                    double Value = Surface.SampleScalar(ContentView, Lat, Lon);

                    if (double.IsNaN(Value))

                    {

                        Bitmap.SetPixel(X, Y, SKColors.Transparent);

                        if (Shades != null)

                            Shades[Y * Width + X] = double.NaN;

                        continue;

                    }



                    uint Argb = NineBandColorScale.GetColor(Value, VMin, VMax);

                    SKColor Color = ArgbToSkia(Argb);



                    if (Shades != null)

                    {

                        double Shade = HillshadeRenderer.ComputeFactorAtLocation(

                            Surface, ContentView, CentreLat, Lat, Lon, ZScale);

                        Shades[Y * Width + X] = Shade;

                        Bitmap.SetPixel(X, Y, Color);

                    }

                    else

                    {

                        Bitmap.SetPixel(X, Y, Color);

                    }

                }

                int FirstPassEnd = ApplyHillshade ? 70 : 100;
                ProgressMapper.ReportSegment(Progress, 0, FirstPassEnd, (Y + 1) * 100 / Height);

            }



            if (Shades != null)

            {

                // Second pass: blur hillshade factors then multiply into base band colours.

                HillshadeRenderer.BoxBlurShadeGrid(Shades, Width, Height, Radius: 2);

                ProgressMapper.ReportSegment(Progress, 70, 85, 100);

                for (int Y = 0; Y < Height; Y++)

                {

                    for (int X = 0; X < Width; X++)

                    {

                        int Idx = Y * Width + X;

                        SKColor Base = Bitmap.GetPixel(X, Y);

                        if (Base.Alpha == 0)

                            continue;



                        Bitmap.SetPixel(X, Y, ModulateHillshade(Base, Shades[Idx]));

                    }

                    ProgressMapper.ReportSegment(Progress, 85, 100, (Y + 1) * 100 / Height);

                }

            }



            Progress?.Report(100);

            return Bitmap;

        }



        /// <summary>

        /// Darkens or lightens a base colour by a hillshade factor.

        /// </summary>

        /// <param name="Base">Band colour before shading.</param>

        /// <param name="Shade">Hillshade factor in [0, 1], or NaN to leave unchanged.</param>

        /// <returns>Modulated colour.</returns>

        static private SKColor ModulateHillshade

            (

            SKColor Base,

            double Shade

            )

        {

            if (double.IsNaN(Shade))

                return Base;



            double Mix = 0.32 + 0.68 * Shade;

            return new SKColor(

                (byte)Math.Clamp(Base.Red * Mix, 0, 255),

                (byte)Math.Clamp(Base.Green * Mix, 0, 255),

                (byte)Math.Clamp(Base.Blue * Mix, 0, 255),

                Base.Alpha);

        }



        /// <summary>

        /// Converts a 32-bit ARGB value to an Skia colour.

        /// </summary>

        /// <param name="Argb">Colour in ARGB byte order.</param>

        /// <returns>Skia RGBA colour.</returns>

        static private SKColor ArgbToSkia

            (

            uint Argb

            )

        {

            byte A = (byte)((Argb >> 24) & 0xFF);

            byte R = (byte)((Argb >> 16) & 0xFF);

            byte G = (byte)((Argb >> 8) & 0xFF);

            byte B = (byte)(Argb & 0xFF);

            return new SKColor(R, G, B, A);

        }



        /// <summary>

        /// Builds a supersampled hillshade factor grid for the scalar field (no colour bands).

        /// </summary>

        /// <param name="Surface">Sampled elevation field.</param>

        /// <param name="ContentView">Scalar channel used for slope calculation.</param>

        /// <returns>Flat row-major shade factors and grid dimensions.</returns>

        static public (double[] Shades, int Width, int Height) BuildShadeGrid

            (

            FieldSurfaceSampler Surface,

            FieldContentView ContentView

            )

        {

            int Width = Math.Max(2, (Surface.SampleWidth - 1) * SUPERSAMPLE_FACTOR + 1);

            int Height = Math.Max(2, (Surface.SampleHeight - 1) * SUPERSAMPLE_FACTOR + 1);

            double[] Shades = new double[Width * Height];

            double CentreLat = (Surface.MinLat + Surface.MaxLat) * 0.5;

            double ZScale = Surface.ComputeHillshadeZScale();



            for (int Y = 0; Y < Height; Y++)

            {

                for (int X = 0; X < Width; X++)

                {

                    double Lon = Surface.MinLon + (Surface.MaxLon - Surface.MinLon) * X / (Width - 1);

                    double Lat = Surface.MaxLat - (Surface.MaxLat - Surface.MinLat) * Y / (Height - 1);

                    int Idx = Y * Width + X;

                    double Value = Surface.SampleScalar(ContentView, Lat, Lon);

                    if (double.IsNaN(Value))

                    {

                        Shades[Idx] = double.NaN;

                        continue;

                    }



                    Shades[Idx] = HillshadeRenderer.ComputeFactorAtLocation(

                        Surface, ContentView, CentreLat, Lat, Lon, ZScale);

                }

            }



            HillshadeRenderer.BoxBlurShadeGrid(Shades, Width, Height, Radius: 2);

            return (Shades, Width, Height);

        }



        /// <summary>

        /// Applies a precomputed hillshade grid to a base colour map bitmap.

        /// </summary>

        /// <param name="Base">Band-coloured bitmap without hillshade.</param>

        /// <param name="Shades">Flat row-major shade factors matching Base dimensions.</param>

        /// <returns>New bitmap with hillshade modulation applied.</returns>

        static public SKBitmap ApplyHillshadeGrid

            (

            SKBitmap Base,

            double[] Shades

            )

        {

            int Width = Base.Width;

            int Height = Base.Height;

            SKBitmap Result = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);



            for (int Y = 0; Y < Height; Y++)

            {

                for (int X = 0; X < Width; X++)

                {

                    int Idx = Y * Width + X;

                    SKColor Pixel = Base.GetPixel(X, Y);

                    if (Pixel.Alpha == 0)

                    {

                        Result.SetPixel(X, Y, Pixel);

                        continue;

                    }



                    Result.SetPixel(X, Y, ModulateHillshade(Pixel, Shades[Idx]));

                }

            }



            return Result;

        }

    }

}


