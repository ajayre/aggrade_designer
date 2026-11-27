using SkiaSharp;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Outlines road and pad cells that finished off the pan target on the haul cut/fill map.
    /// The cut/fill colour inside each cell stays visible.
    /// </summary>
    public static class HaulPanTargetHighlight
    {
        /// <summary>Red channel of the outline. Magenta is outside the cut/fill palette.</summary>
        private const byte HIGHLIGHT_RED = 255;

        /// <summary>Green channel of the outline.</summary>
        private const byte HIGHLIGHT_GREEN = 0;

        /// <summary>Blue channel of the outline.</summary>
        private const byte HIGHLIGHT_BLUE = 255;

        /// <summary>Alpha channel of the outline.</summary>
        private const byte HIGHLIGHT_ALPHA = 255;

        /// <summary>Thinnest outline, in pixels, that still reads on a cell.</summary>
        private const float MIN_STROKE_PX = 2.0f;

        /// <summary>Thickest outline, so a large cell keeps its cut/fill colour.</summary>
        private const float MAX_STROKE_PX = 4.0f;

        /// <summary>Outline width as a fraction of one cell when that falls between the stroke limits.</summary>
        private const float STROKE_CELL_FRACTION = 0.12f;

        /// <summary>
        /// Paints a magenta outline on each failing cell.
        /// </summary>
        /// <param name="CutFillPng">Leveled cut/fill map. Returned unchanged when there is nothing to mark.</param>
        /// <param name="GridWidth">Grid width in cells.</param>
        /// <param name="GridHeight">Grid height in cells.</param>
        /// <param name="PixelsPerBin">Pixels drawn for one grid cell.</param>
        /// <param name="Miss">Failing cells. Null or empty leaves the map unchanged.</param>
        /// <returns>PNG bytes of the marked map, or <paramref name="CutFillPng"/> when nothing was painted.</returns>
        public static byte[] Paint
            (
            byte[] CutFillPng,
            int GridWidth,
            int GridHeight,
            int PixelsPerBin,
            HaulPanTargetMissDto? Miss
            )
        {
            if (Miss == null || Miss.Cells.Count == 0 || GridWidth <= 0 || GridHeight <= 0)
                return CutFillPng;

            int Pixels = Math.Max(1, PixelsPerBin);
            int ExpectedWidth = GridWidth * Pixels;
            int ExpectedHeight = GridHeight * Pixels;
            using SKBitmap? Bitmap = SKBitmap.Decode(CutFillPng);
            // A route sprite is not the full cut/fill map. Stamping grid cells onto it would mark the wrong place.
            if (Bitmap == null || Bitmap.Width != ExpectedWidth || Bitmap.Height != ExpectedHeight)
                return CutFillPng;

            using SKCanvas Canvas = new SKCanvas(Bitmap);
            float Stroke = Math.Clamp(Pixels * STROKE_CELL_FRACTION, MIN_STROKE_PX, MAX_STROKE_PX);
            bool FillCell = Stroke * 2.0f >= Pixels;
            using SKPaint Paint = new SKPaint
            {
                Color = new SKColor(HIGHLIGHT_RED, HIGHLIGHT_GREEN, HIGHLIGHT_BLUE, HIGHLIGHT_ALPHA),
                Style = FillCell ? SKPaintStyle.Fill : SKPaintStyle.Stroke,
                StrokeWidth = Stroke,
                IsAntialias = false
            };

            foreach (HaulPanTargetMissDto.Cell Cell in Miss.Cells)
            {
                if (Cell.X < 0 || Cell.Y < 0 || Cell.X >= GridWidth || Cell.Y >= GridHeight)
                    continue;

                // Image north is the top row. Grid row 0 is the south edge, matching the cut/fill renderer.
                float Left = Cell.X * Pixels;
                float Right = Left + Pixels;
                float Top = (GridHeight - 1 - Cell.Y) * Pixels;
                float Bottom = Top + Pixels;
                SKRect Rect = FillCell
                    ? new SKRect(Left, Top, Right, Bottom)
                    : new SKRect(
                        Left + (Stroke / 2.0f),
                        Top + (Stroke / 2.0f),
                        Right - (Stroke / 2.0f),
                        Bottom - (Stroke / 2.0f));
                Canvas.DrawRect(Rect, Paint);
            }

            Canvas.Flush();
            using SKImage Image = SKImage.FromBitmap(Bitmap);
            using SKData Data = Image.Encode(SKEncodedImageFormat.Png, 100);
            return Data.ToArray();
        }
    }
}
