using System.Runtime.InteropServices;

using AgGradeDesigner.Core;
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
        /// <summary>Full-quality supersample factor used for final display caches.</summary>
        public const int FullSupersampleFactor = 4;

        /// <summary>Fast preview supersample factor for progressive import display.</summary>
        public const int PreviewSupersampleFactor = 2;

        /// <summary>
        /// Minimum width or height in pixels for colour-map bitmaps.
        /// </summary>

        private const int MIN_BITMAP_DIMENSION = 2;

        /// <summary>
        /// Progress reporter value indicating completion.
        /// </summary>

        private const int PROGRESS_COMPLETE_PERCENT = 100;

        /// <summary>
        /// Progress percent after the initial hillshade sampling pass.
        /// </summary>

        private const int HILLSHADE_FIRST_PASS_END_PERCENT = 70;

        /// <summary>
        /// Progress percent after hillshade blur completes.
        /// </summary>

        private const int HILLSHADE_BLUR_END_PERCENT = 85;

        /// <summary>
        /// Divisor used to throttle row-based progress reporting.
        /// </summary>

        private const int PROGRESS_ROW_STRIDE_DIVISOR = 32;

        /// <summary>
        /// Box-blur radius applied to hillshade factor grids.
        /// </summary>

        private const int HILLSHADE_BLUR_RADIUS = 2;

        /// <summary>
        /// Base mix weight before applying hillshade modulation.
        /// </summary>

        private const double HILLSHADE_MIX_BASE = 0.32;

        /// <summary>
        /// Scale applied to hillshade factor when modulating colour.
        /// </summary>

        private const double HILLSHADE_MIX_SCALE = 0.68;

        /// <summary>
        /// Bitmask isolating one 8-bit ARGB channel.
        /// </summary>

        private const byte CHANNEL_MASK = 0xFF;

        /// <summary>
        /// Bit shift for the alpha channel in packed ARGB values.
        /// </summary>

        private const int ARGB_ALPHA_SHIFT = 24;

        /// <summary>
        /// Bit shift for the red channel in packed ARGB values.
        /// </summary>

        private const int ARGB_RED_SHIFT = 16;

        /// <summary>
        /// Bit shift for the green channel in packed ARGB values.
        /// </summary>

        private const int ARGB_GREEN_SHIFT = 8;

        /// <summary>
        /// Builds a supersampled colour map bitmap for the scalar field, optionally with hillshade.
        /// </summary>
        /// <param name="Surface">Sampled elevation field.</param>
        /// <param name="ContentView">Scalar channel to colour (existing, proposed, or cut/fill).</param>
        /// <param name="VMin">Minimum value of the display range.</param>
        /// <param name="VMax">Maximum value of the display range.</param>
        /// <param name="ApplyHillshade">When true, modulates colours with a hillshade overlay.</param>
        /// <param name="Progress">Optional 0–100 progress reporter.</param>
        /// <param name="SupersampleFactor">Pixels per surface cell edge (2 = preview, 4 = full quality).</param>
        /// <returns>RGBA bitmap sized to supersample the surface grid.</returns>
        static unsafe public SKBitmap Build
            (
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double VMin,
            double VMax,
            bool ApplyHillshade,
            IProgress<int>? Progress = null,
            int SupersampleFactor = FullSupersampleFactor
            )
        {
            int Factor = Math.Clamp(SupersampleFactor, 1, FullSupersampleFactor);
            int Width = Math.Max(MIN_BITMAP_DIMENSION, (Surface.SampleWidth - 1) * Factor + 1);
            int Height = Math.Max(MIN_BITMAP_DIMENSION, (Surface.SampleHeight - 1) * Factor + 1);
            SKBitmap Bitmap = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);
            double[]? Shades = ApplyHillshade ? new double[Width * Height] : null;

            double CentreLat = (Surface.MinLat + Surface.MaxLat) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
            double ZScale = ApplyHillshade ? Surface.ComputeHillshadeZScale() : 1.0;
            double LonScale = 1.0 / Math.Max(1, Width - 1);
            double LatScale = 1.0 / Math.Max(1, Height - 1);
            double LonSpan = Surface.MaxLon - Surface.MinLon;
            double LatSpan = Surface.MaxLat - Surface.MinLat;

            nint PixelPtr = Bitmap.GetPixels();
            if (PixelPtr == nint.Zero)
                throw new InvalidOperationException("Failed to allocate colour map pixel buffer.");

            int RowPixels = Bitmap.RowBytes / sizeof(uint);
            int FirstPassEnd = ApplyHillshade ? HILLSHADE_FIRST_PASS_END_PERCENT : PROGRESS_COMPLETE_PERCENT;
            int ProgressStride = Math.Max(1, Height / PROGRESS_ROW_STRIDE_DIVISOR);

            Parallel.For(0, Height, Y =>
            {
                uint* Row = (uint*)PixelPtr + ((nuint)Y * (nuint)RowPixels);
                double Lat = Surface.MaxLat - LatSpan * Y * LatScale;

                for (int X = 0; X < Width; X++)
                {
                    double Lon = Surface.MinLon + LonSpan * X * LonScale;
                    int Idx = Y * Width + X;

                    if (!Surface.IsInsideValidRegion(Lat, Lon))
                    {
                        Row[X] = 0;
                        if (Shades != null)
                            Shades[Idx] = double.NaN;
                        continue;
                    }

                    double Value = Surface.SampleScalar(ContentView, Lat, Lon);

                    if (double.IsNaN(Value))
                    {
                        Row[X] = 0;
                        if (Shades != null)
                            Shades[Idx] = double.NaN;
                        continue;
                    }

                    Row[X] = ArgbToRgba8888(NineBandColorScale.GetColor(Value, VMin, VMax));

                    if (Shades != null)
                    {
                        Shades[Idx] = HillshadeRenderer.ComputeFactorAtLocation(
                            Surface, ContentView, CentreLat, Lat, Lon, ZScale);
                    }
                }

                for (int X = Width; X < RowPixels; X++)
                    Row[X] = 0;

                if ((Y % ProgressStride) == 0 || Y == Height - 1)
                    ProgressMapper.ReportSegment(Progress, 0, FirstPassEnd, (Y + 1) * PROGRESS_COMPLETE_PERCENT / Height);
            });

            if (Shades != null)
            {
                HillshadeRenderer.BoxBlurShadeGrid(Shades, Width, Height, Radius: HILLSHADE_BLUR_RADIUS);
                ProgressMapper.ReportSegment(Progress, HILLSHADE_FIRST_PASS_END_PERCENT, HILLSHADE_BLUR_END_PERCENT, PROGRESS_COMPLETE_PERCENT);

                Parallel.For(0, Height, Y =>
                {
                    uint* Row = (uint*)PixelPtr + ((nuint)Y * (nuint)RowPixels);
                    for (int X = 0; X < Width; X++)
                    {
                        int Idx = Y * Width + X;
                        uint Pixel = Row[X];
                        if (GetRgba8888Alpha(Pixel) == 0)
                            continue;

                        Row[X] = ArgbToRgba8888(ModulateHillshadeArgb(Rgba8888ToArgb(Pixel), Shades[Idx]));
                    }

                    if ((Y % ProgressStride) == 0 || Y == Height - 1)
                        ProgressMapper.ReportSegment(Progress, HILLSHADE_BLUR_END_PERCENT, PROGRESS_COMPLETE_PERCENT, (Y + 1) * PROGRESS_COMPLETE_PERCENT / Height);
                });
            }

            Progress?.Report(PROGRESS_COMPLETE_PERCENT);
            return Bitmap;
        }

        /// <summary>
        /// Builds a supersampled hillshade factor grid for the scalar field (no colour bands).
        /// </summary>
        static public (double[] Shades, int Width, int Height) BuildShadeGrid
            (
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            int SupersampleFactor = FullSupersampleFactor
            )
        {
            int Factor = Math.Clamp(SupersampleFactor, 1, FullSupersampleFactor);
            int Width = Math.Max(MIN_BITMAP_DIMENSION, (Surface.SampleWidth - 1) * Factor + 1);
            int Height = Math.Max(MIN_BITMAP_DIMENSION, (Surface.SampleHeight - 1) * Factor + 1);
            double[] Shades = new double[Width * Height];
            double CentreLat = (Surface.MinLat + Surface.MaxLat) * FieldConstants.EXTENT_MIDPOINT_FRACTION;
            double ZScale = Surface.ComputeHillshadeZScale();
            double LonScale = 1.0 / Math.Max(1, Width - 1);
            double LatScale = 1.0 / Math.Max(1, Height - 1);
            double LonSpan = Surface.MaxLon - Surface.MinLon;
            double LatSpan = Surface.MaxLat - Surface.MinLat;

            Parallel.For(0, Height, Y =>
            {
                double Lat = Surface.MaxLat - LatSpan * Y * LatScale;
                for (int X = 0; X < Width; X++)
                {
                    double Lon = Surface.MinLon + LonSpan * X * LonScale;
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
            });

            HillshadeRenderer.BoxBlurShadeGrid(Shades, Width, Height, Radius: HILLSHADE_BLUR_RADIUS);
            return (Shades, Width, Height);
        }

        /// <summary>
        /// Applies a precomputed hillshade grid to a base colour map bitmap.
        /// </summary>
        static unsafe public SKBitmap ApplyHillshadeGrid
            (
            SKBitmap Base,
            double[] Shades
            )
        {
            int Width = Base.Width;
            int Height = Base.Height;
            SKBitmap Result = new SKBitmap(Width, Height, SKColorType.Rgba8888, SKAlphaType.Premul);

            nint SourcePtr = Base.GetPixels();
            nint DestPtr = Result.GetPixels();
            if ((SourcePtr == nint.Zero) || (DestPtr == nint.Zero))
                throw new InvalidOperationException("Failed to access hillshade pixel buffers.");

            int RowPixels = Result.RowBytes / sizeof(uint);

            Parallel.For(0, Height, Y =>
            {
                uint* SourceRow = (uint*)SourcePtr + ((nuint)Y * (nuint)RowPixels);
                uint* DestRow = (uint*)DestPtr + ((nuint)Y * (nuint)RowPixels);
                int RowBase = Y * Width;

                for (int X = 0; X < Width; X++)
                {
                    uint Pixel = SourceRow[X];
                    if (GetRgba8888Alpha(Pixel) == 0)
                    {
                        DestRow[X] = Pixel;
                        continue;
                    }

                    DestRow[X] = ArgbToRgba8888(ModulateHillshadeArgb(Rgba8888ToArgb(Pixel), Shades[RowBase + X]));
                }

                for (int X = Width; X < RowPixels; X++)
                    DestRow[X] = 0;
            });

            return Result;
        }

        /// <summary>
        /// Darkens or lightens a packed ARGB pixel by a hillshade factor.
        /// </summary>
        static private uint ModulateHillshadeArgb
            (
            uint Argb,
            double Shade
            )
        {
            if (double.IsNaN(Shade))
                return Argb;

            double Mix = HILLSHADE_MIX_BASE + HILLSHADE_MIX_SCALE * Shade;
            byte A = (byte)((Argb >> ARGB_ALPHA_SHIFT) & CHANNEL_MASK);
            byte R = (byte)Math.Clamp(((Argb >> ARGB_RED_SHIFT) & CHANNEL_MASK) * Mix, 0, CHANNEL_MASK);
            byte G = (byte)Math.Clamp(((Argb >> ARGB_GREEN_SHIFT) & CHANNEL_MASK) * Mix, 0, CHANNEL_MASK);
            byte B = (byte)Math.Clamp((Argb & CHANNEL_MASK) * Mix, 0, CHANNEL_MASK);
            return ((uint)A << ARGB_ALPHA_SHIFT) | ((uint)R << ARGB_RED_SHIFT) | ((uint)G << ARGB_GREEN_SHIFT) | B;
        }

        /// <summary>
        /// Packs ARGB channel values into Skia RGBA8888 little-endian pixel layout.
        /// </summary>
        static private uint ArgbToRgba8888
            (
            uint Argb
            )
        {
            byte A = (byte)((Argb >> ARGB_ALPHA_SHIFT) & CHANNEL_MASK);
            byte R = (byte)((Argb >> ARGB_RED_SHIFT) & CHANNEL_MASK);
            byte G = (byte)((Argb >> ARGB_GREEN_SHIFT) & CHANNEL_MASK);
            byte B = (byte)(Argb & CHANNEL_MASK);
            return (uint)R | ((uint)G << ARGB_GREEN_SHIFT) | ((uint)B << ARGB_RED_SHIFT) | ((uint)A << ARGB_ALPHA_SHIFT);
        }

        /// <summary>
        /// Unpacks a Skia RGBA8888 little-endian pixel into ARGB channel order.
        /// </summary>
        static private uint Rgba8888ToArgb
            (
            uint Pixel
            )
        {
            byte R = (byte)(Pixel & CHANNEL_MASK);
            byte G = (byte)((Pixel >> ARGB_GREEN_SHIFT) & CHANNEL_MASK);
            byte B = (byte)((Pixel >> ARGB_RED_SHIFT) & CHANNEL_MASK);
            byte A = (byte)((Pixel >> ARGB_ALPHA_SHIFT) & CHANNEL_MASK);
            return ((uint)A << ARGB_ALPHA_SHIFT) | ((uint)R << ARGB_RED_SHIFT) | ((uint)G << ARGB_GREEN_SHIFT) | B;
        }

        /// <summary>
        /// Returns the alpha channel from a Skia RGBA8888 pixel.
        /// </summary>
        static private byte GetRgba8888Alpha
            (
            uint Pixel
            )
        {
            return (byte)((Pixel >> ARGB_ALPHA_SHIFT) & CHANNEL_MASK);
        }
    }
}
