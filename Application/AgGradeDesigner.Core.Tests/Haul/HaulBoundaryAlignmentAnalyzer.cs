using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.Haul
{
    /// <summary>
    /// Compares haul field-boundary PNG strokes to expected exterior data-cell edges.
    /// </summary>
    internal static class HaulBoundaryAlignmentAnalyzer
    {
        /// <summary>RGB channel maximum treated as near-white background.</summary>
        internal const byte NEAR_WHITE_CHANNEL_MIN = 240;

        /// <summary>RGB channel maximum treated as boundary stroke.</summary>
        internal const byte NEAR_BLACK_CHANNEL_MAX = 80;

        /// <summary>Search radius in pixels when matching edge samples to stroke pixels.</summary>
        internal const int DEFAULT_SEARCH_RADIUS_PX = 20;

        /// <summary>Minimum fraction of cut/fill edge pixels that must lie near a boundary stroke.</summary>
        internal const double MIN_EDGE_ALIGNMENT_FRACTION = 0.95;

        /// <summary>Minimum fraction of boundary stroke pixels that must lie near a cut/fill edge.</summary>
        internal const double MIN_STROKE_ON_EDGE_FRACTION = 0.35;

        /// <summary>Minimum stroke pixels required for a visible boundary.</summary>
        internal const int MIN_STROKE_PIXEL_COUNT = 200;

        /// <summary>Maximum fraction of stroke pixels allowed on the bitmap outer border.</summary>
        internal const double MAX_OUTER_BORDER_STROKE_FRACTION = 0.55;

        /// <summary>Minimum stroke pixels that must lie off the bitmap outer border for an irregular field.</summary>
        internal const int MIN_INTERIOR_STROKE_PIXEL_COUNT = 1000;

        /// <summary>Minimum fraction of the bitmap span the stroke must NOT fill for both axes.</summary>
        internal const double MIN_AXIS_INSET_FRACTION = 0.02;

        /// <summary>Minimum fraction of exterior edge pixels that must lie near a boundary stroke.</summary>
        internal const double MIN_EXTERIOR_EDGE_ALIGNMENT_FRACTION = 0.95;

        /// <summary>Minimum fraction of boundary stroke pixels that must lie near an exterior edge.</summary>
        internal const double MIN_STROKE_ON_EXTERIOR_EDGE_FRACTION = 0.95;

        /// <summary>
        /// Measures how well boundary stroke pixels align with the outer haul-grid data edge.
        /// </summary>
        internal static AlignmentMetrics AnalyzeExteriorValidBoundary
            (
            HaulBuilder.Grid Grid,
            byte[] BoundaryPngBytes,
            int PixelsPerBin,
            int SearchRadiusPx = DEFAULT_SEARCH_RADIUS_PX
            )
        {
            using SKBitmap Boundary = SKBitmap.Decode(BoundaryPngBytes)
                ?? throw new InvalidOperationException("Failed to decode boundary PNG.");

            int WidthPx = Grid.Width * PixelsPerBin;
            int HeightPx = Grid.Height * PixelsPerBin;
            if (Boundary.Width != WidthPx || Boundary.Height != HeightPx)
            {
                throw new InvalidOperationException(
                    $"Bitmap size mismatch: expected={WidthPx}x{HeightPx}, boundary={Boundary.Width}x{Boundary.Height}.");
            }

            bool[,] Edge = BuildExteriorValidEdgeMask(Grid, PixelsPerBin);
            bool[,] Stroke = new bool[WidthPx, HeightPx];
            for (int Y = 0; Y < HeightPx; Y++)
            {
                for (int X = 0; X < WidthPx; X++)
                    Stroke[X, Y] = IsNearBlack(Boundary.GetPixel(X, Y));
            }

            int EdgeCount = 0;
            int EdgeMatched = 0;
            for (int Y = 0; Y < HeightPx; Y++)
            {
                for (int X = 0; X < WidthPx; X++)
                {
                    if (!Edge[X, Y])
                        continue;

                    EdgeCount++;
                    if (HasStrokeWithin(Stroke, X, Y, WidthPx, HeightPx, SearchRadiusPx))
                        EdgeMatched++;
                }
            }

            int StrokeCount = 0;
            int StrokeOnEdge = 0;
            for (int Y = 0; Y < HeightPx; Y++)
            {
                for (int X = 0; X < WidthPx; X++)
                {
                    if (!Stroke[X, Y])
                        continue;

                    StrokeCount++;
                    if (HasStrokeWithin(Edge, X, Y, WidthPx, HeightPx, SearchRadiusPx))
                        StrokeOnEdge++;
                }
            }

            return new AlignmentMetrics
            {
                Width = WidthPx,
                Height = HeightPx,
                EdgePixelCount = EdgeCount,
                StrokePixelCount = StrokeCount,
                EdgeAlignmentFraction = EdgeCount == 0 ? 0 : EdgeMatched / (double)EdgeCount,
                StrokeOnEdgeFraction = StrokeCount == 0 ? 0 : StrokeOnEdge / (double)StrokeCount
            };
        }

        private static bool[,] BuildExteriorValidEdgeMask
            (
            HaulBuilder.Grid Grid,
            int PixelsPerBin
            )
        {
            bool[,] Valid = Grid.Valid;
            int GridWidth = Grid.Width;
            int GridHeight = Grid.Height;
            bool[,] ExteriorInvalid = MarkExteriorInvalid(Valid, GridWidth, GridHeight);
            int WidthPx = GridWidth * PixelsPerBin;
            int HeightPx = GridHeight * PixelsPerBin;
            bool[,] Edge = new bool[WidthPx, HeightPx];

            for (int CellY = 0; CellY < GridHeight; CellY++)
            {
                for (int CellX = 0; CellX < GridWidth; CellX++)
                {
                    if (!IsGridCellValid(Valid, CellX, CellY, GridWidth, GridHeight))
                        continue;

                    float Left = CellX * PixelsPerBin;
                    float Right = (CellX + 1) * PixelsPerBin;
                    float Top = (GridHeight - 1 - CellY) * PixelsPerBin;
                    float Bottom = (GridHeight - CellY) * PixelsPerBin;

                    if (BordersExteriorInvalid(ExteriorInvalid, CellX - 1, CellY, GridWidth, GridHeight))
                        MarkVerticalEdge(Edge, (int)Left, (int)Top, (int)Bottom, WidthPx, HeightPx);

                    if (BordersExteriorInvalid(ExteriorInvalid, CellX + 1, CellY, GridWidth, GridHeight))
                        MarkVerticalEdge(Edge, (int)Right, (int)Top, (int)Bottom, WidthPx, HeightPx);

                    if (BordersExteriorInvalid(ExteriorInvalid, CellX, CellY + 1, GridWidth, GridHeight))
                        MarkHorizontalEdge(Edge, (int)Left, (int)Right, (int)Top, WidthPx, HeightPx);

                    if (BordersExteriorInvalid(ExteriorInvalid, CellX, CellY - 1, GridWidth, GridHeight))
                        MarkHorizontalEdge(Edge, (int)Left, (int)Right, (int)Bottom, WidthPx, HeightPx);
                }
            }

            return Edge;
        }

        private static void MarkVerticalEdge
            (
            bool[,] Edge,
            int X,
            int Top,
            int Bottom,
            int WidthPx,
            int HeightPx
            )
        {
            int ClampedX = Math.Clamp(X, 0, WidthPx - 1);
            for (int Y = Math.Max(0, Top); Y < Math.Min(HeightPx, Bottom); Y++)
                Edge[ClampedX, Y] = true;
        }

        private static void MarkHorizontalEdge
            (
            bool[,] Edge,
            int Left,
            int Right,
            int Y,
            int WidthPx,
            int HeightPx
            )
        {
            int ClampedY = Math.Clamp(Y, 0, HeightPx - 1);
            for (int X = Math.Max(0, Left); X < Math.Min(WidthPx, Right); X++)
                Edge[X, ClampedY] = true;
        }

        private static bool[,] MarkExteriorInvalid
            (
            bool[,] Valid,
            int GridWidth,
            int GridHeight
            )
        {
            bool[,] Exterior = new bool[GridHeight, GridWidth];
            Stack<(int X, int Y)> Stack = new Stack<(int X, int Y)>();

            void Seed
                (
                int X,
                int Y
                )
            {
                if (X < 0 || Y < 0 || X >= GridWidth || Y >= GridHeight)
                    return;

                if (Valid[Y, X] || Exterior[Y, X])
                    return;

                Exterior[Y, X] = true;
                Stack.Push((X, Y));
            }

            for (int X = 0; X < GridWidth; X++)
            {
                Seed(X, 0);
                Seed(X, GridHeight - 1);
            }

            for (int Y = 0; Y < GridHeight; Y++)
            {
                Seed(0, Y);
                Seed(GridWidth - 1, Y);
            }

            while (Stack.Count > 0)
            {
                (int X, int Y) = Stack.Pop();
                Seed(X - 1, Y);
                Seed(X + 1, Y);
                Seed(X, Y - 1);
                Seed(X, Y + 1);
            }

            return Exterior;
        }

        private static bool BordersExteriorInvalid
            (
            bool[,] ExteriorInvalid,
            int CellX,
            int CellY,
            int GridWidth,
            int GridHeight
            )
        {
            if (CellX < 0 || CellY < 0 || CellX >= GridWidth || CellY >= GridHeight)
                return true;

            return ExteriorInvalid[CellY, CellX];
        }

        private static bool IsGridCellValid
            (
            bool[,] Valid,
            int CellX,
            int CellY,
            int GridWidth,
            int GridHeight
            )
        {
            if (CellX < 0 || CellY < 0 || CellX >= GridWidth || CellY >= GridHeight)
                return false;

            return Valid[CellY, CellX];
        }

        /// <summary>
        /// Measures how well boundary stroke pixels align with the cut/fill map field edge.
        /// </summary>
        internal static AlignmentMetrics Analyze
            (
            byte[] BaseMapPngBytes,
            byte[] BoundaryPngBytes,
            int SearchRadiusPx = DEFAULT_SEARCH_RADIUS_PX
            )
        {
            using SKBitmap BaseMap = SKBitmap.Decode(BaseMapPngBytes)
                ?? throw new InvalidOperationException("Failed to decode base map PNG.");
            using SKBitmap Boundary = SKBitmap.Decode(BoundaryPngBytes)
                ?? throw new InvalidOperationException("Failed to decode boundary PNG.");

            if (BaseMap.Width != Boundary.Width || BaseMap.Height != Boundary.Height)
            {
                throw new InvalidOperationException(
                    $"Bitmap size mismatch: base={BaseMap.Width}x{BaseMap.Height}, boundary={Boundary.Width}x{Boundary.Height}.");
            }

            int Width = BaseMap.Width;
            int Height = BaseMap.Height;
            bool[,] Inside = new bool[Width, Height];
            bool[,] Stroke = new bool[Width, Height];
            bool[,] Edge = new bool[Width, Height];

            for (int Y = 0; Y < Height; Y++)
            {
                for (int X = 0; X < Width; X++)
                {
                    SKColor BaseColor = BaseMap.GetPixel(X, Y);
                    SKColor BoundaryColor = Boundary.GetPixel(X, Y);
                    Inside[X, Y] = !IsNearWhite(BaseColor);
                    Stroke[X, Y] = IsNearBlack(BoundaryColor);
                }
            }

            int EdgeCount = 0;
            int EdgeMatched = 0;
            for (int Y = 0; Y < Height; Y++)
            {
                for (int X = 0; X < Width; X++)
                {
                    if (!Inside[X, Y] || !IsEdgePixel(Inside, X, Y, Width, Height))
                        continue;

                    EdgeCount++;
                    Edge[X, Y] = true;
                    if (HasStrokeWithin(Stroke, X, Y, Width, Height, SearchRadiusPx))
                        EdgeMatched++;
                }
            }

            int StrokeCount = 0;
            int StrokeOnEdge = 0;
            for (int Y = 0; Y < Height; Y++)
            {
                for (int X = 0; X < Width; X++)
                {
                    if (!Stroke[X, Y])
                        continue;

                    StrokeCount++;
                    if (HasEdgeWithin(Edge, X, Y, Width, Height, SearchRadiusPx))
                        StrokeOnEdge++;
                }
            }

            return new AlignmentMetrics
            {
                Width = Width,
                Height = Height,
                EdgePixelCount = EdgeCount,
                StrokePixelCount = StrokeCount,
                EdgeAlignmentFraction = EdgeCount == 0 ? 0 : EdgeMatched / (double)EdgeCount,
                StrokeOnEdgeFraction = StrokeCount == 0 ? 0 : StrokeOnEdge / (double)StrokeCount
            };
        }

        /// <summary>
        /// Detects whether a boundary stroke is mostly the bitmap/grid outer rectangle.
        /// </summary>
        internal static ShapeMetrics AnalyzeShape
            (
            byte[] BoundaryPngBytes
            )
        {
            using SKBitmap Boundary = SKBitmap.Decode(BoundaryPngBytes)
                ?? throw new InvalidOperationException("Failed to decode boundary PNG.");

            int Width = Boundary.Width;
            int Height = Boundary.Height;
            int StrokeCount = 0;
            int OuterBorderStrokeCount = 0;
            int MinStrokeX = Width;
            int MaxStrokeX = -1;
            int MinStrokeY = Height;
            int MaxStrokeY = -1;

            for (int Y = 0; Y < Height; Y++)
            {
                for (int X = 0; X < Width; X++)
                {
                    if (!IsNearBlack(Boundary.GetPixel(X, Y)))
                        continue;

                    StrokeCount++;
                    MinStrokeX = Math.Min(MinStrokeX, X);
                    MaxStrokeX = Math.Max(MaxStrokeX, X);
                    MinStrokeY = Math.Min(MinStrokeY, Y);
                    MaxStrokeY = Math.Max(MaxStrokeY, Y);

                    if (X <= 1 || Y <= 1 || X >= Width - 2 || Y >= Height - 2)
                        OuterBorderStrokeCount++;
                }
            }

            int InteriorStrokeCount = StrokeCount - OuterBorderStrokeCount;
            double OuterBorderStrokeFraction = StrokeCount == 0 ? 0 : OuterBorderStrokeCount / (double)StrokeCount;
            double HorizontalSpanFraction = Width <= 1 ? 0 : (MaxStrokeX - MinStrokeX) / (double)(Width - 1);
            double VerticalSpanFraction = Height <= 1 ? 0 : (MaxStrokeY - MinStrokeY) / (double)(Height - 1);
            bool SpansFullBitmap =
                MinStrokeX <= 1 &&
                MaxStrokeX >= Width - 2 &&
                MinStrokeY <= 1 &&
                MaxStrokeY >= Height - 2;
            bool IsGridRectangle =
                StrokeCount > 0 &&
                SpansFullBitmap &&
                HorizontalSpanFraction >= 1.0 - MIN_AXIS_INSET_FRACTION &&
                VerticalSpanFraction >= 1.0 - MIN_AXIS_INSET_FRACTION &&
                OuterBorderStrokeFraction >= MAX_OUTER_BORDER_STROKE_FRACTION &&
                InteriorStrokeCount < MIN_INTERIOR_STROKE_PIXEL_COUNT;

            return new ShapeMetrics
            {
                StrokePixelCount = StrokeCount,
                OuterBorderStrokeCount = OuterBorderStrokeCount,
                InteriorStrokeCount = InteriorStrokeCount,
                OuterBorderStrokeFraction = OuterBorderStrokeFraction,
                HorizontalSpanFraction = HorizontalSpanFraction,
                VerticalSpanFraction = VerticalSpanFraction,
                IsGridRectangle = IsGridRectangle
            };
        }

        /// <summary>
        /// Assert-friendly check that the boundary is not just the haul bitmap outer rectangle.
        /// </summary>
        internal static void AssertNotGridRectangle
            (
            byte[] BoundaryPngBytes
            )
        {
            ShapeMetrics Shape = AnalyzeShape(BoundaryPngBytes);
            if (Shape.IsGridRectangle)
            {
                throw new InvalidOperationException(
                    $"Boundary stroke is a grid rectangle, not the field shape: {Shape.Describe()}");
            }
        }

        private static bool IsEdgePixel
            (
            bool[,] Inside,
            int X,
            int Y,
            int Width,
            int Height
            )
        {
            if (X == 0 || Y == 0 || X == Width - 1 || Y == Height - 1)
                return true;

            return !Inside[X - 1, Y] ||
                !Inside[X + 1, Y] ||
                !Inside[X, Y - 1] ||
                !Inside[X, Y + 1];
        }

        private static bool HasStrokeWithin
            (
            bool[,] Stroke,
            int X,
            int Y,
            int Width,
            int Height,
            int Radius
            )
        {
            for (int Dy = -Radius; Dy <= Radius; Dy++)
            {
                for (int Dx = -Radius; Dx <= Radius; Dx++)
                {
                    int SampleX = X + Dx;
                    int SampleY = Y + Dy;
                    if (SampleX < 0 || SampleY < 0 || SampleX >= Width || SampleY >= Height)
                        continue;

                    if (Stroke[SampleX, SampleY])
                        return true;
                }
            }

            return false;
        }

        private static bool HasEdgeWithin
            (
            bool[,] Edge,
            int X,
            int Y,
            int Width,
            int Height,
            int Radius
            )
        {
            return HasStrokeWithin(Edge, X, Y, Width, Height, Radius);
        }

        private static bool IsNearWhite
            (
            SKColor Color
            )
        {
            return Color.Alpha >= 200 &&
                Color.Red >= NEAR_WHITE_CHANNEL_MIN &&
                Color.Green >= NEAR_WHITE_CHANNEL_MIN &&
                Color.Blue >= NEAR_WHITE_CHANNEL_MIN;
        }

        private static bool IsNearBlack
            (
            SKColor Color
            )
        {
            return Color.Alpha >= 200 &&
                Color.Red <= NEAR_BLACK_CHANNEL_MAX &&
                Color.Green <= NEAR_BLACK_CHANNEL_MAX &&
                Color.Blue <= NEAR_BLACK_CHANNEL_MAX;
        }

        internal readonly struct AlignmentMetrics
        {
            internal int Width { get; init; }
            internal int Height { get; init; }
            internal int EdgePixelCount { get; init; }
            internal int StrokePixelCount { get; init; }
            internal double EdgeAlignmentFraction { get; init; }
            internal double StrokeOnEdgeFraction { get; init; }

            internal string Describe
                (
                )
            {
                return
                    $"edge={EdgePixelCount}, stroke={StrokePixelCount}, " +
                    $"edgeAligned={EdgeAlignmentFraction:P1}, strokeOnEdge={StrokeOnEdgeFraction:P1}";
            }
        }

        internal readonly struct ShapeMetrics
        {
            internal int StrokePixelCount { get; init; }
            internal int OuterBorderStrokeCount { get; init; }
            internal int InteriorStrokeCount { get; init; }
            internal double OuterBorderStrokeFraction { get; init; }
            internal double HorizontalSpanFraction { get; init; }
            internal double VerticalSpanFraction { get; init; }
            internal bool IsGridRectangle { get; init; }

            internal string Describe
                (
                )
            {
                return
                    $"stroke={StrokePixelCount}, outerBorder={OuterBorderStrokeCount}, " +
                    $"interior={InteriorStrokeCount}, outerFraction={OuterBorderStrokeFraction:P1}, " +
                    $"hSpan={HorizontalSpanFraction:P1}, vSpan={VerticalSpanFraction:P1}, " +
                    $"isGridRectangle={IsGridRectangle}";
            }
        }
    }
}
