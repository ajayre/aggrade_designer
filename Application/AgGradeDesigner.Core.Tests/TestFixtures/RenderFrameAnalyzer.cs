using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Pixel-level metrics for headless render regression tests (black frames, content loss, satellite presence).
    /// </summary>
    static internal class RenderFrameAnalyzer
    {
        /// <summary>
        /// Maximum RGB channel value treated as near-black.
        /// </summary>
        internal const byte NEAR_BLACK_CHANNEL_MAX = 20;

        /// <summary>
        /// Minimum fraction of non-near-black pixels required for a frame to count as visible content.
        /// </summary>
        internal const double MIN_VISIBLE_CONTENT_FRACTION = 0.05;

        /// <summary>
        /// Maximum fraction of near-black pixels before a frame is classified as a black failure.
        /// </summary>
        internal const double MAX_BLACK_FRAME_FRACTION = 0.90;

        /// <summary>
        /// Minimum fraction of pixels that must differ between two frames to confirm camera motion.
        /// </summary>
        internal const double MIN_FRAME_DIFF_FRACTION = 0.02;

        /// <summary>
        /// Relative drop in visible content between consecutive frames that indicates a black flash.
        /// </summary>
        internal const double FLASH_VISIBLE_DROP_FRACTION = 0.50;

        /// <summary>
        /// Per-channel tolerance when matching an expected satellite tile colour.
        /// </summary>
        internal const int SATELLITE_COLOR_TOLERANCE = 25;

        /// <summary>
        /// Minimum fraction of pixels that must match the seeded satellite colour when satellite is on.
        /// </summary>
        internal const double MIN_SATELLITE_PIXEL_FRACTION = 0.0002;

        /// <summary>
        /// BGRA bytes per pixel.
        /// </summary>
        internal const int BYTES_PER_PIXEL = 4;

        /// <summary>
        /// Blue channel index in BGRA layout.
        /// </summary>
        internal const int CHANNEL_BLUE = 0;

        /// <summary>
        /// Green channel index in BGRA layout.
        /// </summary>
        internal const int CHANNEL_GREEN = 1;

        /// <summary>
        /// Red channel index in BGRA layout.
        /// </summary>
        internal const int CHANNEL_RED = 2;

        /// <summary>
        /// Summarized pixel metrics for one rendered frame.
        /// </summary>
        internal readonly struct FrameMetrics
        {
            /// <summary>
            /// Total pixels sampled.
            /// </summary>
            internal int TotalPixels { get; init; }

            /// <summary>
            /// Count of pixels that are not near-black.
            /// </summary>
            internal int VisiblePixels { get; init; }

            /// <summary>
            /// Fraction of pixels that are not near-black.
            /// </summary>
            internal double VisibleFraction { get; init; }

            /// <summary>
            /// Fraction of pixels classified as near-black.
            /// </summary>
            internal double BlackFraction { get; init; }

            /// <summary>
            /// Mean luminance across all pixels (0–255).
            /// </summary>
            internal double MeanLuminance { get; init; }
        }

        /// <summary>
        /// Computes frame metrics from BGRA8888 pixel bytes.
        /// </summary>
        /// <param name="Pixels">Row-major BGRA pixel buffer.</param>
        /// <param name="Width">Frame width in pixels.</param>
        /// <param name="Height">Frame height in pixels.</param>
        /// <returns>Aggregated metrics for the frame.</returns>
        static public FrameMetrics AnalyzeBgraBytes
            (
            byte[] Pixels,
            int Width,
            int Height
            )
        {
            int TotalPixels = Width * Height;
            if ((Pixels.Length < TotalPixels * BYTES_PER_PIXEL) || (TotalPixels <= 0))
            {
                return new FrameMetrics
                {
                    TotalPixels = TotalPixels,
                    VisiblePixels = 0,
                    VisibleFraction = 0,
                    BlackFraction = 1.0,
                    MeanLuminance = 0
                };
            }

            int VisiblePixels = 0;
            long LuminanceSum = 0;
            for (int Offset = 0; Offset < TotalPixels * BYTES_PER_PIXEL; Offset += BYTES_PER_PIXEL)
            {
                byte Blue = Pixels[Offset + CHANNEL_BLUE];
                byte Green = Pixels[Offset + CHANNEL_GREEN];
                byte Red = Pixels[Offset + CHANNEL_RED];
                int Luminance = Red + Green + Blue;
                LuminanceSum += Luminance;

                if (!IsNearBlack(Red, Green, Blue))
                    VisiblePixels++;
            }

            double VisibleFraction = VisiblePixels / (double)TotalPixels;
            return new FrameMetrics
            {
                TotalPixels = TotalPixels,
                VisiblePixels = VisiblePixels,
                VisibleFraction = VisibleFraction,
                BlackFraction = 1.0 - VisibleFraction,
                MeanLuminance = LuminanceSum / (3.0 * TotalPixels)
            };
        }

        /// <summary>
        /// Computes frame metrics from a Skia bitmap.
        /// </summary>
        /// <param name="Bitmap">Decoded or rendered bitmap.</param>
        /// <returns>Aggregated metrics for the frame.</returns>
        static public FrameMetrics AnalyzeBitmap
            (
            SKBitmap Bitmap
            )
        {
            return AnalyzeBgraBytes(CopyBitmapToBgraBytes(Bitmap), Bitmap.Width, Bitmap.Height);
        }

        /// <summary>
        /// Copies BGRA8888 pixels from a Skia bitmap into a managed buffer.
        /// </summary>
        /// <param name="Bitmap">Source bitmap in BGRA8888 layout.</param>
        /// <returns>Row-major BGRA pixel bytes.</returns>
        static public byte[] CopyBitmapToBgraBytes
            (
            SKBitmap Bitmap
            )
        {
            int Length = Bitmap.Width * Bitmap.Height * BYTES_PER_PIXEL;
            byte[] Pixels = new byte[Length];
            IntPtr Source = Bitmap.GetPixels();
            System.Runtime.InteropServices.Marshal.Copy(Source, Pixels, 0, Length);
            return Pixels;
        }

        /// <summary>
        /// Returns true when the frame has enough non-black content to represent a healthy render.
        /// </summary>
        /// <param name="Metrics">Precomputed frame metrics.</param>
        /// <returns>True when visible content exceeds the minimum threshold.</returns>
        static public bool HasMinimumVisibleContent
            (
            FrameMetrics Metrics
            )
        {
            return Metrics.VisibleFraction >= MIN_VISIBLE_CONTENT_FRACTION;
        }

        /// <summary>
        /// Returns true when the frame is dominated by near-black pixels (view turned black).
        /// </summary>
        /// <param name="Metrics">Precomputed frame metrics.</param>
        /// <returns>True when black pixel fraction exceeds the failure threshold.</returns>
        static public bool IsMostlyBlack
            (
            FrameMetrics Metrics
            )
        {
            return Metrics.BlackFraction >= MAX_BLACK_FRAME_FRACTION;
        }

        /// <summary>
        /// Returns the fraction of pixels that differ between two BGRA frames of equal size.
        /// </summary>
        /// <param name="Left">First frame pixels.</param>
        /// <param name="Right">Second frame pixels.</param>
        /// <param name="Width">Frame width in pixels.</param>
        /// <param name="Height">Frame height in pixels.</param>
        /// <returns>Fraction of pixels that differ by at least one channel.</returns>
        static public double ComputeDiffFraction
            (
            byte[] Left,
            byte[] Right,
            int Width,
            int Height
            )
        {
            int TotalPixels = Width * Height;
            if ((TotalPixels <= 0) ||
                (Left.Length < TotalPixels * BYTES_PER_PIXEL) ||
                (Right.Length < TotalPixels * BYTES_PER_PIXEL))
                return 0;

            int DiffPixels = 0;
            for (int Offset = 0; Offset < TotalPixels * BYTES_PER_PIXEL; Offset += BYTES_PER_PIXEL)
            {
                if (Left[Offset] != Right[Offset] ||
                    Left[Offset + 1] != Right[Offset + 1] ||
                    Left[Offset + 2] != Right[Offset + 2])
                {
                    DiffPixels++;
                }
            }

            return DiffPixels / (double)TotalPixels;
        }

        /// <summary>
        /// Returns true when visible content dropped sharply between two frames (black flash).
        /// </summary>
        /// <param name="Before">Metrics from the prior frame.</param>
        /// <param name="After">Metrics from the current frame.</param>
        /// <returns>True when the visible fraction fell by more than the flash threshold.</returns>
        static public bool DetectBlackFlash
            (
            FrameMetrics Before,
            FrameMetrics After
            )
        {
            if (Before.VisibleFraction <= MIN_VISIBLE_CONTENT_FRACTION)
                return false;

            double Drop = (Before.VisibleFraction - After.VisibleFraction) / Before.VisibleFraction;
            return Drop >= FLASH_VISIBLE_DROP_FRACTION;
        }

        /// <summary>
        /// Counts pixels matching a seeded satellite tile colour within tolerance.
        /// </summary>
        /// <param name="Pixels">Row-major BGRA pixel buffer.</param>
        /// <param name="Width">Frame width in pixels.</param>
        /// <param name="Height">Frame height in pixels.</param>
        /// <param name="Expected">Expected satellite tile colour.</param>
        /// <returns>Fraction of pixels matching the expected colour.</returns>
        static public double ComputeSatelliteColorFraction
            (
            byte[] Pixels,
            int Width,
            int Height,
            SKColor Expected
            )
        {
            int TotalPixels = Width * Height;
            if ((TotalPixels <= 0) || (Pixels.Length < TotalPixels * BYTES_PER_PIXEL))
                return 0;

            int MatchPixels = 0;
            for (int Offset = 0; Offset < TotalPixels * BYTES_PER_PIXEL; Offset += BYTES_PER_PIXEL)
            {
                byte Blue = Pixels[Offset + CHANNEL_BLUE];
                byte Green = Pixels[Offset + CHANNEL_GREEN];
                byte Red = Pixels[Offset + CHANNEL_RED];
                if (Math.Abs(Red - Expected.Red) <= SATELLITE_COLOR_TOLERANCE &&
                    Math.Abs(Green - Expected.Green) <= SATELLITE_COLOR_TOLERANCE &&
                    Math.Abs(Blue - Expected.Blue) <= SATELLITE_COLOR_TOLERANCE)
                {
                    MatchPixels++;
                }
            }

            return MatchPixels / (double)TotalPixels;
        }

        /// <summary>
        /// Assert-friendly description of frame metrics.
        /// </summary>
        /// <param name="Metrics">Metrics to describe.</param>
        /// <param name="Label">Optional scenario label.</param>
        /// <returns>Human-readable summary for test failure messages.</returns>
        static public string Describe
            (
            FrameMetrics Metrics,
            string Label = ""
            )
        {
            string Prefix = string.IsNullOrWhiteSpace(Label) ? "" : $"{Label}: ";
            return $"{Prefix}visible={Metrics.VisibleFraction:P1}, black={Metrics.BlackFraction:P1}, meanLuma={Metrics.MeanLuminance:F1}";
        }

        /// <summary>
        /// Returns true when all three RGB channels are at or below the near-black threshold.
        /// </summary>
        static private bool IsNearBlack
            (
            byte Red,
            byte Green,
            byte Blue
            )
        {
            return Red <= NEAR_BLACK_CHANNEL_MAX &&
                   Green <= NEAR_BLACK_CHANNEL_MAX &&
                   Blue <= NEAR_BLACK_CHANNEL_MAX;
        }
    }
}
