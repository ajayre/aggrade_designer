namespace AgGradeDesigner.Core.Water
{
    /// <summary>
    /// Softens overlay alpha channels with a separable box blur at build time.
    /// </summary>
    public static class WaterOverlayAlphaBlur
    {
        /// <summary>RGBA bytes per pixel.</summary>
        private const int RGBA_BYTES_PER_PIXEL = 4;

        /// <summary>
        /// Applies a separable box blur to the alpha channel of an RGBA8888 buffer.
        /// </summary>
        /// <param name="Rgba">Row-major RGBA bytes modified in place.</param>
        /// <param name="Width">Raster width in pixels.</param>
        /// <param name="Height">Raster height in pixels.</param>
        /// <param name="Radius">Blur radius in pixels.</param>
        public static void ApplyToAlphaChannel
            (
            byte[] Rgba,
            int Width,
            int Height,
            int Radius
            )
        {
            if ((Radius <= 0) || (Width <= 0) || (Height <= 0) || (Rgba.Length != Width * Height * RGBA_BYTES_PER_PIXEL))
                return;

            float[] Alpha = new float[Width * Height];
            float[] Scratch = new float[Width * Height];
            for (Int32 Index = 0; Index < Alpha.Length; Index++)
                Alpha[Index] = Rgba[(Index * RGBA_BYTES_PER_PIXEL) + 3];

            BoxBlurPass(Alpha, Scratch, Width, Height, Horizontal: true, Radius);
            BoxBlurPass(Scratch, Alpha, Width, Height, Horizontal: false, Radius);

            for (Int32 Index = 0; Index < Alpha.Length; Index++)
                Rgba[(Index * RGBA_BYTES_PER_PIXEL) + 3] = (byte)Math.Clamp(Alpha[Index], 0, 255);
        }

        /// <summary>
        /// Runs one horizontal or vertical box-blur pass over an alpha grid.
        /// </summary>
        /// <param name="Source">Source alpha values.</param>
        /// <param name="Destination">Destination alpha values.</param>
        /// <param name="Width">Grid width.</param>
        /// <param name="Height">Grid height.</param>
        /// <param name="Horizontal">True for a horizontal pass; false for vertical.</param>
        /// <param name="Radius">Blur radius in pixels.</param>
        private static void BoxBlurPass
            (
            float[] Source,
            float[] Destination,
            int Width,
            int Height,
            bool Horizontal,
            int Radius
            )
        {
            if (Horizontal)
            {
                for (Int32 Y = 0; Y < Height; Y++)
                {
                    int RowOffset = Y * Width;
                    for (int X = 0; X < Width; X++)
                    {
                        int Start = Math.Max(0, X - Radius);
                        int End = Math.Min(Width - 1, X + Radius);
                        double Sum = 0.0;
                        for (int SampleX = Start; SampleX <= End; SampleX++)
                            Sum += Source[RowOffset + SampleX];

                        Destination[RowOffset + X] = (float)(Sum / ((End - Start) + 1));
                    }
                }

                return;
            }

            for (int X = 0; X < Width; X++)
            {
                for (Int32 Y = 0; Y < Height; Y++)
                {
                    int Start = Math.Max(0, Y - Radius);
                    int End = Math.Min(Height - 1, Y + Radius);
                    double Sum = 0.0;
                    for (int SampleY = Start; SampleY <= End; SampleY++)
                        Sum += Source[(SampleY * Width) + X];

                    Destination[(Y * Width) + X] = (float)(Sum / ((End - Start) + 1));
                }
            }
        }
    }
}
