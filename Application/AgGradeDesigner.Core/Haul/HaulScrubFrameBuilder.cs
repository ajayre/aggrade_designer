using SkiaSharp;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Builds one haul scrub frame bitmap: cut/fill base map plus route symbology (§7.4.5–§7.4.9).
    /// </summary>
    public static class HaulScrubFrameBuilder
    {
        /// <summary>Cut section colour on the haul bitmap.</summary>
        private static readonly SKColor CutColor = SKColors.Red;

        /// <summary>Fill section colour on the haul bitmap.</summary>
        private static readonly SKColor FillColor = SKColors.Blue;

        /// <summary>GPS track polyline colour.</summary>
        private static readonly SKColor TrackColor = SKColors.Black;

        /// <summary>GPS track outline colour.</summary>
        private static readonly SKColor TrackOutlineColor = SKColors.White;

        /// <summary>Narrow GPS track stroke width in pixels (§7.4.9).</summary>
        private const float TrackStrokeWidth = 3.0f;

        /// <summary>GPS vertex marker radius in pixels.</summary>
        private const float VertexRadius = 4.0f;

        /// <summary>
        /// Generates PNG bytes for one haul scrub frame at route index <paramref name="RouteScrubIndex"/>.
        /// </summary>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Field">Parsed AGD field used to rebuild the grid when no live result is present.</param>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <returns>PNG bytes for the complete haul scrub frame.</returns>
        public static byte[] BuildFramePng
            (
            HaulData Data,
            HaulBuilder.AgdField Field,
            int RouteScrubIndex
            )
        {
            HaulBuilder.Grid Grid = Data.ResolveGrid(Field);
            IReadOnlyList<HaulBuilder.Pass> OrderedPasses = Data.ResolveOrderedPasses();
            int PixelsPerBin = Data.Payload.PixelsPerBin;
            int Index = Math.Clamp(RouteScrubIndex, 0, Math.Max(0, OrderedPasses.Count - 1));

            byte[] BasePng = BuildBaseMapPng(Grid, OrderedPasses, Index, PixelsPerBin);
            IReadOnlyList<HaulBuilder.GuidancePoint> Guidance = Data.ResolveGuidancePoints();
            int CurrentPassNo = Index + 1;
            return OverlayRouteSymbology(BasePng, Grid, Guidance, CurrentPassNo, PixelsPerBin);
        }

        /// <summary>
        /// Builds the haul cut/fill base map PNG for the supplied route scrub index.
        /// </summary>
        /// <param name="Grid">Processing grid.</param>
        /// <param name="OrderedPasses">Route-ordered passes.</param>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <param name="PixelsPerBin">Render scale in pixels per grid cell.</param>
        /// <returns>Base map PNG bytes.</returns>
        private static byte[] BuildBaseMapPng
            (
            HaulBuilder.Grid Grid,
            IReadOnlyList<HaulBuilder.Pass> OrderedPasses,
            int RouteScrubIndex,
            int PixelsPerBin
            )
        {
            if (RouteScrubIndex <= 0 || OrderedPasses.Count == 0)
                return HaulBuilder.Render.RenderPng(Grid, null, PixelsPerBin, 0);

            // Apply only passes completed before the current route (§7.4.5).
            int PreviousPassCount = Math.Min(RouteScrubIndex, OrderedPasses.Count);
            IReadOnlyList<HaulBuilder.Pass> PreviousPasses = OrderedPasses.Take(PreviousPassCount).ToList();
            HaulBuilder.Grid LeveledGrid = HaulGridLeveling.BuildAfterPasses(Grid, PreviousPasses);
            return HaulBuilder.Render.RenderPng(LeveledGrid, null, PixelsPerBin, 0);
        }

        /// <summary>
        /// Composites symbology for the current haul route over the leveled base map.
        /// </summary>
        /// <param name="BasePng">Cut/fill base map PNG bytes.</param>
        /// <param name="Grid">Processing grid for coordinate projection.</param>
        /// <param name="Guidance">Full guidance track.</param>
        /// <param name="CurrentPassNo">1-based pass number for the route being displayed.</param>
        /// <param name="PixelsPerBin">Render scale in pixels per grid cell.</param>
        /// <returns>Composited PNG bytes.</returns>
        private static byte[] OverlayRouteSymbology
            (
            byte[] BasePng,
            HaulBuilder.Grid Grid,
            IReadOnlyList<HaulBuilder.GuidancePoint> Guidance,
            int CurrentPassNo,
            int PixelsPerBin
            )
        {
            using SKBitmap Base = SKBitmap.Decode(BasePng)
                ?? throw new InvalidOperationException("Failed to decode haul base map PNG.");

            using SKBitmap Composited = Base.Copy();
            using SKCanvas Canvas = new SKCanvas(Composited);

            List<HaulBuilder.GuidancePoint> Visible = Guidance
                .Where(Point => Point.PassNo == CurrentPassNo)
                .OrderBy(Point => Point.Seq)
                .ToList();

            if (Visible.Count >= 2)
            {
                float SwathWidthPx = SwathStrokeWidth(PixelsPerBin);

                for (int Index = 1; Index < Visible.Count; Index++)
                {
                    HaulBuilder.GuidancePoint Start = Visible[Index - 1];
                    HaulBuilder.GuidancePoint End = Visible[Index];
                    if (!IsCutOrFillAction(End.Action))
                        continue;

                    DrawSegment(
                        Canvas,
                        Grid,
                        Start,
                        End,
                        PixelsPerBin,
                        TrackOutlineColor,
                        SwathWidthPx + 3.0f);
                    DrawSegment(
                        Canvas,
                        Grid,
                        Start,
                        End,
                        PixelsPerBin,
                        ResolveSegmentColor(End.Action),
                        SwathWidthPx);
                }

                for (int Index = 1; Index < Visible.Count; Index++)
                {
                    HaulBuilder.GuidancePoint Start = Visible[Index - 1];
                    HaulBuilder.GuidancePoint End = Visible[Index];
                    DrawSegment(Canvas, Grid, Start, End, PixelsPerBin, TrackOutlineColor, TrackStrokeWidth + 2.0f);
                    DrawSegment(Canvas, Grid, Start, End, PixelsPerBin, TrackColor, TrackStrokeWidth);
                }

                DrawVertices(Canvas, Grid, Visible, PixelsPerBin);
            }

            return EncodePng(Composited);
        }

        /// <summary>
        /// Returns swath width in pixels for one haul grid bin edge (§7.4.6–§7.4.8).
        /// </summary>
        private static float SwathStrokeWidth
            (
            int PixelsPerBin
            )
        {
            return Math.Max(PixelsPerBin, 1);
        }

        /// <summary>
        /// Returns whether an inbound guidance action is a cut or fill swath.
        /// </summary>
        private static bool IsCutOrFillAction
            (
            string Action
            )
        {
            return Action.Equals("CUT", StringComparison.OrdinalIgnoreCase) ||
                Action.Equals("FILL", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Resolves symbology colour for one guidance leg from its inbound action code.
        /// </summary>
        private static SKColor ResolveSegmentColor
            (
            string Action
            )
        {
            if (Action.Equals("CUT", StringComparison.OrdinalIgnoreCase))
                return CutColor;

            if (Action.Equals("FILL", StringComparison.OrdinalIgnoreCase))
                return FillColor;

            return TrackColor;
        }

        /// <summary>
        /// Draws one route segment between consecutive guidance points.
        /// </summary>
        private static void DrawSegment
            (
            SKCanvas Canvas,
            HaulBuilder.Grid Grid,
            HaulBuilder.GuidancePoint Start,
            HaulBuilder.GuidancePoint End,
            int PixelsPerBin,
            SKColor Color,
            float StrokeWidth
            )
        {
            (float StartX, float StartY) = ProjectToPixel(Grid, Start.X, Start.Y, PixelsPerBin);
            (float EndX, float EndY) = ProjectToPixel(Grid, End.X, End.Y, PixelsPerBin);

            using SKPaint Paint = new SKPaint
            {
                Color = Color,
                StrokeWidth = StrokeWidth,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };

            Canvas.DrawLine(StartX, StartY, EndX, EndY, Paint);
        }

        /// <summary>
        /// Draws GPS vertex markers with white outlines.
        /// </summary>
        private static void DrawVertices
            (
            SKCanvas Canvas,
            HaulBuilder.Grid Grid,
            IReadOnlyList<HaulBuilder.GuidancePoint> Points,
            int PixelsPerBin
            )
        {
            using SKPaint FillPaint = new SKPaint
            {
                Color = TrackColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };
            using SKPaint OutlinePaint = new SKPaint
            {
                Color = TrackOutlineColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 2.0f
            };

            foreach (HaulBuilder.GuidancePoint Point in Points)
            {
                (float X, float Y) Pixel = ProjectToPixel(Grid, Point.X, Point.Y, PixelsPerBin);
                Canvas.DrawCircle(Pixel.X, Pixel.Y, VertexRadius + 1.0f, OutlinePaint);
                Canvas.DrawCircle(Pixel.X, Pixel.Y, VertexRadius, FillPaint);
            }
        }

        /// <summary>
        /// Projects a UTM coordinate to image pixel space using grid geometry.
        /// </summary>
        private static (float X, float Y) ProjectToPixel
            (
            HaulBuilder.Grid Grid,
            double Easting,
            double Northing,
            int PixelsPerBin
            )
        {
            double CellX = (Easting - Grid.MinX) / Grid.GridSizeM - 0.5;
            double CellY = (Northing - Grid.MinY) / Grid.GridSizeM - 0.5;
            float PixelX = (float)(CellX * PixelsPerBin + PixelsPerBin * 0.5);
            float PixelY = (float)((Grid.Height - 1 - CellY) * PixelsPerBin + PixelsPerBin * 0.5);
            return (PixelX, PixelY);
        }

        /// <summary>
        /// Encodes a bitmap to PNG bytes.
        /// </summary>
        private static byte[] EncodePng
            (
            SKBitmap Bitmap
            )
        {
            using SKImage Image = SKImage.FromBitmap(Bitmap);
            using SKData Data = Image.Encode(SKEncodedImageFormat.Png, 100);
            return Data.ToArray();
        }
    }
}
