using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Lightweight checks on downloaded or rendered image byte payloads in tests.
    /// </summary>
    static internal class ImageBytes
    {
        /// <summary>
        /// Detects common JPEG or PNG magic-byte prefixes in a byte buffer.
        /// </summary>
        /// <param name="Data">Raw file or tile bytes.</param>
        /// <returns>True when the buffer begins with a recognised image signature.</returns>
        static public bool LooksLikeImage
            (
            byte[] Data
            )
        {
            if (Data.Length < 4)
                return false;

            // Recognise JPEG SOI and PNG signature bytes without decoding the full image.
            bool Jpeg = (Data[0] == 0xFF) && (Data[1] == 0xD8);
            bool Png = (Data[0] == 0x89) && (Data[1] == 0x50) && (Data[2] == 0x4E) && (Data[3] == 0x47);
            return Jpeg || Png;
        }

        /// <summary>
        /// Samples decoded PNG pixels to confirm the image is not a flat placeholder colour.
        /// </summary>
        /// <param name="PngBytes">PNG-encoded image bytes.</param>
        /// <returns>True when at least one sampled pixel differs from the top-left pixel.</returns>
        static public bool HasVariedPixels
            (
            byte[] PngBytes
            )
        {
            using SKBitmap Bitmap = SKBitmap.Decode(PngBytes);
            if ((Bitmap == null) || (Bitmap.Width < 2) || (Bitmap.Height < 2))
                return false;

            SKColor First = Bitmap.GetPixel(0, 0);
            // Sample a coarse grid so large tiles are checked without scanning every pixel.
            for (Int32 Y = 0; Y < Bitmap.Height; Y += Math.Max(1, Bitmap.Height / 16))
            {
                for (Int32 X = 0; X < Bitmap.Width; X += Math.Max(1, Bitmap.Width / 16))
                {
                    if (Bitmap.GetPixel(X, Y) != First)
                        return true;
                }
            }

            return false;
        }
    }
}
