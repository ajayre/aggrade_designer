using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

using SkiaSharp;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Builds haul scrub imagery: fast cropped route sprites for scrubbing and full frames when needed.
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

        /// <summary>Extra padding around route bounds in grid cells.</summary>
        private const int RouteBoundsPaddingCells = 2;

        /// <summary>Maximum pixels-per-bin used for fast scrub route sprites.</summary>
        private const int RouteSpriteMaxPixelsPerBin = 16;

        /// <summary>Field perimeter stroke width in haul boundary bitmap pixels.</summary>
        private const float FieldBoundaryStrokeWidth = 2.0f;

        /// <summary>Extra white halo width behind the black boundary stroke.</summary>
        private const float BoundaryHaloExtraWidth = 4.0f;

        /// <summary>Reference viewport width used to size the boundary for field-fit display.</summary>
        private const float ReferenceViewportWidthPx = 1280.0f;

        /// <summary>Target boundary stroke width on screen after the field bitmap is fitted.</summary>
        private const float TargetScreenStrokePx = 2.5f;

        /// <summary>
        /// Computes geographic bounds for a haul processing grid bitmap.
        /// </summary>
        public static FieldBounds ComputeGridBounds
            (
            HaulBuilder.Grid Grid
            )
        {
            double MaxE = Grid.MinX + Grid.Width * Grid.GridSizeM;
            double MaxN = Grid.MinY + Grid.Height * Grid.GridSizeM;
            return UtmBoundsToFieldBounds(Grid, Grid.MinX, Grid.MinY, MaxE, MaxN);
        }

        /// <summary>
        /// Builds a transparent PNG with a black perimeter tracing the outer edge of haul grid data cells.
        /// </summary>
        /// <param name="Project">Project containing survey boundary points.</param>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Field">Parsed AGD field used to resolve the haul grid.</param>
        /// <returns>PNG bytes aligned to the haul grid, or null when a boundary cannot be derived.</returns>
        public static byte[]? BuildFieldBoundaryPng
            (
            ProjectDocument Project,
            HaulData Data,
            HaulBuilder.AgdField Field
            )
        {
            if (Field == null)
                return null;

            HaulBuilder.Grid Grid = Data.ResolveGrid(Field);
            int PixelsPerBin = Data.Payload.PixelsPerBin;
            int WidthPx = Grid.Width * PixelsPerBin;
            int HeightPx = Grid.Height * PixelsPerBin;

            using SKBitmap Bitmap = new SKBitmap(WidthPx, HeightPx, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (SKCanvas Canvas = new SKCanvas(Bitmap))
            {
                Canvas.Clear(SKColors.Transparent);
                float StrokeWidth = ComputeBoundaryStrokeWidth(WidthPx, Grid.Width, PixelsPerBin);

                if (TryDrawExteriorValidMaskBoundary(Canvas, Grid, PixelsPerBin, StrokeWidth))
                    return EncodePng(Bitmap);

                if (Project != null && TryDrawSurveyBoundaryRing(Canvas, Grid, Project, PixelsPerBin, StrokeWidth))
                    return EncodePng(Bitmap);

                if (TryDrawAgdBoundaryRing(Canvas, Grid, Field, PixelsPerBin, StrokeWidth))
                    return EncodePng(Bitmap);

                return null;
            }
        }

        /// <summary>
        /// Chooses a stroke width that stays visible after the bitmap is scaled to a field-fit viewport.
        /// </summary>
        private static float ComputeBoundaryStrokeWidth
            (
            int BitmapWidthPx,
            int GridWidthCells,
            int PixelsPerBin
            )
        {
            float BitmapFitScale = ReferenceViewportWidthPx / Math.Max(1, BitmapWidthPx);
            float MinBitmapStroke = TargetScreenStrokePx / Math.Max(BitmapFitScale, 0.05f);
            float CellRelativeStroke = PixelsPerBin * 0.6f;
            float Stroke = Math.Max(FieldBoundaryStrokeWidth, Math.Max(CellRelativeStroke, MinBitmapStroke));
            return Math.Clamp(Stroke, 2f, PixelsPerBin * 2f);
        }

        private static SKPaint CreateBoundaryPaint
            (
            SKColor Color,
            float StrokeWidth
            )
        {
            return new SKPaint
            {
                Color = Color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = StrokeWidth,
                IsAntialias = true,
                StrokeJoin = SKStrokeJoin.Miter,
                StrokeCap = SKStrokeCap.Square
            };
        }

        private static void StrokeBoundaryPath
            (
            SKCanvas Canvas,
            SKPath Path,
            float StrokeWidth
            )
        {
            using SKPaint HaloPaint = CreateBoundaryPaint(SKColors.White, StrokeWidth + BoundaryHaloExtraWidth);
            using SKPaint StrokePaint = CreateBoundaryPaint(SKColors.Black, StrokeWidth);
            Canvas.DrawPath(Path, HaloPaint);
            Canvas.DrawPath(Path, StrokePaint);
        }

        /// <summary>
        /// Traces the outer edge of valid haul grid cells, ignoring interior holes.
        /// </summary>
        private static bool TryDrawExteriorValidMaskBoundary
            (
            SKCanvas Canvas,
            HaulBuilder.Grid Grid,
            int PixelsPerBin,
            float StrokeWidth
            )
        {
            bool[,] ExteriorInvalid = MarkExteriorInvalid(Grid.Valid, Grid.Width, Grid.Height);
            return TryDrawExteriorCellMaskBoundary(
                Canvas,
                Grid.Valid,
                ExteriorInvalid,
                Grid.Width,
                Grid.Height,
                PixelsPerBin,
                StrokeWidth);
        }

        /// <summary>
        /// Flood-fills invalid cells reachable from the grid border (excludes interior holes).
        /// </summary>
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

        /// <summary>
        /// Draws edges where valid cells meet exterior empty space only.
        /// </summary>
        private static bool TryDrawExteriorCellMaskBoundary
            (
            SKCanvas Canvas,
            bool[,] Valid,
            bool[,] ExteriorInvalid,
            int GridWidth,
            int GridHeight,
            int PixelsPerBin,
            float StrokeWidth
            )
        {
            if (Valid == null || Valid.GetLength(0) == 0 || Valid.GetLength(1) == 0)
                return false;

            using SKPath Path = new SKPath();
            bool HasSegments = false;

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
                    {
                        Path.MoveTo(Left, Top);
                        Path.LineTo(Left, Bottom);
                        HasSegments = true;
                    }

                    if (BordersExteriorInvalid(ExteriorInvalid, CellX + 1, CellY, GridWidth, GridHeight))
                    {
                        Path.MoveTo(Right, Top);
                        Path.LineTo(Right, Bottom);
                        HasSegments = true;
                    }

                    if (BordersExteriorInvalid(ExteriorInvalid, CellX, CellY + 1, GridWidth, GridHeight))
                    {
                        Path.MoveTo(Left, Top);
                        Path.LineTo(Right, Top);
                        HasSegments = true;
                    }

                    if (BordersExteriorInvalid(ExteriorInvalid, CellX, CellY - 1, GridWidth, GridHeight))
                    {
                        Path.MoveTo(Left, Bottom);
                        Path.LineTo(Right, Bottom);
                        HasSegments = true;
                    }
                }
            }

            if (!HasSegments)
                return false;

            StrokeBoundaryPath(Canvas, Path, StrokeWidth);
            return true;
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

        /// <summary>
        /// Draws the survey 2PER boundary ring projected into haul-grid pixel space.
        /// </summary>
        private static bool TryDrawSurveyBoundaryRing
            (
            SKCanvas Canvas,
            HaulBuilder.Grid Grid,
            ProjectDocument Project,
            int PixelsPerBin,
            float StrokeWidth
            )
        {
            SurveyModel Survey = Project.Survey!;
            if (Survey.BoundaryPoints.Count < GeoConstants.MIN_POLYGON_VERTICES)
                return false;

            List<(double Lat, double Lon)> Unordered = new List<(double Lat, double Lon)>(Survey.BoundaryPoints.Count);
            foreach (TopologyPoint Point in Survey.BoundaryPoints)
                Unordered.Add((Point.Latitude, Point.Longitude));

            List<(double Lat, double Lon)> Ring = BoundaryMask.OrderBoundaryLatLon(Unordered);
            if (Ring.Count < GeoConstants.MIN_POLYGON_VERTICES)
                return false;

            List<(double Lat, double Lon)> LatLonInput = new List<(double Lat, double Lon)>(Ring);
            List<(double X, double Y)> Projected = HaulBuilder.Agd.ProjectLatlonRing(LatLonInput);
            if (Projected.Count < GeoConstants.MIN_POLYGON_VERTICES)
                return false;

            using SKPath Path = new SKPath();
            for (int Index = 0; Index < Projected.Count; Index++)
            {
                (double Easting, double Northing) = Projected[Index];
                (float PixelX, float PixelY) = EastingNorthingToPixel(
                    Grid,
                    Easting,
                    Northing,
                    PixelsPerBin);
                if (Index == 0)
                    Path.MoveTo(PixelX, PixelY);
                else
                    Path.LineTo(PixelX, PixelY);
            }

            Path.Close();
            StrokeBoundaryPath(Canvas, Path, StrokeWidth);
            return true;
        }

        /// <summary>
        /// Draws the AGD 2PER boundary ring using projected easting/northing in grid space.
        /// </summary>
        private static bool TryDrawAgdBoundaryRing
            (
            SKCanvas Canvas,
            HaulBuilder.Grid Grid,
            HaulBuilder.AgdField Field,
            int PixelsPerBin,
            float StrokeWidth
            )
        {
            if (Field.Boundary == null || Field.Boundary.Count < GeoConstants.MIN_POLYGON_VERTICES)
                return false;

            List<(double X, double Y)> Ordered = HaulBuilder.GridMath.OrderBoundaryRing(
                new List<HaulBuilder.AgdPoint>(Field.Boundary));
            if (Ordered.Count < GeoConstants.MIN_POLYGON_VERTICES)
                return false;

            using SKPath Path = new SKPath();
            for (int Index = 0; Index < Ordered.Count; Index++)
            {
                (double Easting, double Northing) = Ordered[Index];
                (float PixelX, float PixelY) = EastingNorthingToPixel(
                    Grid,
                    Easting,
                    Northing,
                    PixelsPerBin);
                if (Index == 0)
                    Path.MoveTo(PixelX, PixelY);
                else
                    Path.LineTo(PixelX, PixelY);
            }

            Path.Close();
            StrokeBoundaryPath(Canvas, Path, StrokeWidth);
            return true;
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
        /// Builds a cropped transparent route sprite positioned in map space for fast scrub preview.
        /// </summary>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Field">Parsed AGD field used to rebuild the grid when no live result is present.</param>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <returns>Cropped PNG bytes and geographic bounds for on-screen placement.</returns>
        public static HaulScrubFrame BuildRouteSprite
            (
            HaulData Data,
            HaulBuilder.AgdField Field,
            int RouteScrubIndex
            )
        {
            HaulBuilder.Grid Grid = Data.ResolveGrid(Field);
            int PixelsPerBin = Data.Payload.PixelsPerBin;
            int SpritePixelsPerBin = Math.Max(1, Math.Min(PixelsPerBin, RouteSpriteMaxPixelsPerBin));
            int Index = Math.Clamp(RouteScrubIndex, 0, Math.Max(0, Data.RouteStepCount - 1));
            int CurrentPassNo = Index + 1;
            IReadOnlyList<HaulBuilder.GuidancePoint> Guidance = Data.ResolveGuidancePoints();
            List<HaulBuilder.GuidancePoint> Visible = Guidance
                .Where(Point => Point.PassNo == CurrentPassNo)
                .OrderBy(Point => Point.Seq)
                .ToList();

            if (Visible.Count < 2)
            {
                return new HaulScrubFrame
                {
                    RouteScrubIndex = Index,
                    Kind = HaulScrubFrameKind.Partial
                };
            }

            ComputeRouteUtmBounds(Grid, Visible, SpritePixelsPerBin, out double MinE, out double MinN, out double MaxE, out double MaxN);
            FieldBounds Bounds = UtmBoundsToFieldBounds(Grid, MinE, MinN, MaxE, MaxN);

            double OriginCellX = (MinE - Grid.MinX) / Grid.GridSizeM;
            double OriginCellYTop = (MaxN - Grid.MinY) / Grid.GridSizeM;
            int WidthPx = Math.Max(1, (int)Math.Ceiling((MaxE - MinE) / Grid.GridSizeM * SpritePixelsPerBin));
            int HeightPx = Math.Max(1, (int)Math.Ceiling((MaxN - MinN) / Grid.GridSizeM * SpritePixelsPerBin));
            float PixelOffsetX = (float)(OriginCellX * SpritePixelsPerBin);
            float PixelOffsetY = (float)((Grid.Height - 1 - OriginCellYTop) * SpritePixelsPerBin);

            using SKBitmap Bitmap = new SKBitmap(WidthPx, HeightPx, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (SKCanvas Canvas = new SKCanvas(Bitmap))
            {
                Canvas.Clear(SKColors.Transparent);
                DrawRouteSymbology(Canvas, Grid, Visible, SpritePixelsPerBin, PixelOffsetX, PixelOffsetY);
            }

            return new HaulScrubFrame
            {
                RouteScrubIndex = Index,
                Kind = HaulScrubFrameKind.Partial,
                PngBytes = EncodePng(Bitmap),
                MinLat = Bounds.MinLat,
                MaxLat = Bounds.MaxLat,
                MinLon = Bounds.MinLon,
                MaxLon = Bounds.MaxLon
            };
        }

        /// <summary>
        /// Builds a complete haul scrub frame: leveled cut/fill map plus the current route.
        /// </summary>
        /// <param name="Data">Active haul data.</param>
        /// <param name="Field">Parsed AGD field used to rebuild the grid when no live result is present.</param>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <returns>Full-frame PNG bytes for display and cache storage.</returns>
        public static HaulScrubFrame BuildCompleteFrame
            (
            HaulData Data,
            HaulBuilder.AgdField Field,
            int RouteScrubIndex
            )
        {
            HaulBuilder.Grid Grid = Data.ResolveGrid(Field);
            IReadOnlyList<HaulBuilder.Pass> OrderedPasses = Data.ResolveOrderedPasses();
            int PixelsPerBin = Data.Payload.PixelsPerBin;
            int Index = Math.Clamp(RouteScrubIndex, 0, Math.Max(0, Data.RouteStepCount - 1));

            return new HaulScrubFrame
            {
                RouteScrubIndex = Index,
                Kind = HaulScrubFrameKind.Complete,
                PngBytes = BuildBaseMapPng(Grid, OrderedPasses, Index, PixelsPerBin),
                RouteOverlayPngBytes = BuildRouteOverlayPng(Data, Field, Index)
            };
        }

        /// <summary>
        /// Builds a transparent PNG with route symbology for one complete-frame route index.
        /// </summary>
        private static byte[] BuildRouteOverlayPng
            (
            HaulData Data,
            HaulBuilder.AgdField Field,
            int RouteScrubIndex
            )
        {
            HaulBuilder.Grid Grid = Data.ResolveGrid(Field);
            int PixelsPerBin = Data.Payload.PixelsPerBin;
            int Index = Math.Clamp(RouteScrubIndex, 0, Math.Max(0, Data.RouteStepCount - 1));
            int CurrentPassNo = Index + 1;
            IReadOnlyList<HaulBuilder.GuidancePoint> Guidance = Data.ResolveGuidancePoints();
            List<HaulBuilder.GuidancePoint> Visible = Guidance
                .Where(Point => Point.PassNo == CurrentPassNo)
                .OrderBy(Point => Point.Seq)
                .ToList();

            int WidthPx = Grid.Width * PixelsPerBin;
            int HeightPx = Grid.Height * PixelsPerBin;
            using SKBitmap Bitmap = new SKBitmap(WidthPx, HeightPx, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (SKCanvas Canvas = new SKCanvas(Bitmap))
            {
                Canvas.Clear(SKColors.Transparent);
                DrawRouteSymbology(Canvas, Grid, Visible, PixelsPerBin, 0.0f, 0.0f);
            }

            return EncodePng(Bitmap);
        }

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

            int PreviousPassCount = Math.Min(RouteScrubIndex, OrderedPasses.Count);
            IReadOnlyList<HaulBuilder.Pass> PreviousPasses = OrderedPasses.Take(PreviousPassCount).ToList();
            HaulBuilder.Grid LeveledGrid = HaulGridLeveling.BuildAfterPasses(Grid, PreviousPasses);
            return HaulBuilder.Render.RenderPng(LeveledGrid, null, PixelsPerBin, 0);
        }

        /// <summary>
        /// Composites symbology for the current haul route over the leveled base map.
        /// </summary>
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

            List<HaulBuilder.GuidancePoint> Visible = Guidance
                .Where(Point => Point.PassNo == CurrentPassNo)
                .OrderBy(Point => Point.Seq)
                .ToList();

            using SKBitmap Composited = Base.Copy();
            using SKCanvas Canvas = new SKCanvas(Composited);
            DrawRouteSymbology(Canvas, Grid, Visible, PixelsPerBin, 0.0f, 0.0f);
            return EncodePng(Composited);
        }

        /// <summary>
        /// Draws cut/fill swaths, travel track, and GPS vertices for one pass onto a canvas.
        /// </summary>
        private static void DrawRouteSymbology
            (
            SKCanvas Canvas,
            HaulBuilder.Grid Grid,
            IReadOnlyList<HaulBuilder.GuidancePoint> Visible,
            int PixelsPerBin,
            float PixelOffsetX,
            float PixelOffsetY
            )
        {
            if (Visible.Count < 2)
                return;

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
                    SwathWidthPx + 3.0f,
                    PixelOffsetX,
                    PixelOffsetY);
                DrawSegment(
                    Canvas,
                    Grid,
                    Start,
                    End,
                    PixelsPerBin,
                    ResolveSegmentColor(End.Action),
                    SwathWidthPx,
                    PixelOffsetX,
                    PixelOffsetY);
            }

            for (int Index = 1; Index < Visible.Count; Index++)
            {
                HaulBuilder.GuidancePoint Start = Visible[Index - 1];
                HaulBuilder.GuidancePoint End = Visible[Index];
                DrawSegment(
                    Canvas,
                    Grid,
                    Start,
                    End,
                    PixelsPerBin,
                    TrackOutlineColor,
                    TrackStrokeWidth + 2.0f,
                    PixelOffsetX,
                    PixelOffsetY);
                DrawSegment(
                    Canvas,
                    Grid,
                    Start,
                    End,
                    PixelsPerBin,
                    TrackColor,
                    TrackStrokeWidth,
                    PixelOffsetX,
                    PixelOffsetY);
            }

            DrawVertices(Canvas, Grid, Visible, PixelsPerBin, PixelOffsetX, PixelOffsetY);
        }

        /// <summary>
        /// Computes a padded UTM bounding box around one route's guidance points.
        /// </summary>
        private static void ComputeRouteUtmBounds
            (
            HaulBuilder.Grid Grid,
            IReadOnlyList<HaulBuilder.GuidancePoint> Visible,
            int PixelsPerBin,
            out double MinE,
            out double MinN,
            out double MaxE,
            out double MaxN
            )
        {
            MinE = Visible.Min(Point => Point.X);
            MaxE = Visible.Max(Point => Point.X);
            MinN = Visible.Min(Point => Point.Y);
            MaxN = Visible.Max(Point => Point.Y);

            double PaddingM = (RouteBoundsPaddingCells + SwathStrokeWidth(PixelsPerBin) / PixelsPerBin) * Grid.GridSizeM;
            MinE -= PaddingM;
            MaxE += PaddingM;
            MinN -= PaddingM;
            MaxN += PaddingM;
        }

        /// <summary>
        /// Converts a UTM bounding box to geographic bounds for map projection.
        /// </summary>
        private static FieldBounds UtmBoundsToFieldBounds
            (
            HaulBuilder.Grid Grid,
            double MinE,
            double MinN,
            double MaxE,
            double MaxN
            )
        {
            UTM.ToLatLon(Grid.Zone, Grid.IsNorthern, MinE, MinN, out double SouthLat, out double WestLon);
            UTM.ToLatLon(Grid.Zone, Grid.IsNorthern, MaxE, MaxN, out double NorthLat, out double EastLon);

            return new FieldBounds
            {
                MinLat = Math.Min(SouthLat, NorthLat),
                MaxLat = Math.Max(SouthLat, NorthLat),
                MinLon = Math.Min(WestLon, EastLon),
                MaxLon = Math.Max(WestLon, EastLon)
            };
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
            float StrokeWidth,
            float PixelOffsetX,
            float PixelOffsetY
            )
        {
            (float StartX, float StartY) = ProjectToPixel(
                Grid,
                Start.X,
                Start.Y,
                PixelsPerBin,
                PixelOffsetX,
                PixelOffsetY);
            (float EndX, float EndY) = ProjectToPixel(
                Grid,
                End.X,
                End.Y,
                PixelsPerBin,
                PixelOffsetX,
                PixelOffsetY);

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
            int PixelsPerBin,
            float PixelOffsetX,
            float PixelOffsetY
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
                (float X, float Y) Pixel = ProjectToPixel(
                    Grid,
                    Point.X,
                    Point.Y,
                    PixelsPerBin,
                    PixelOffsetX,
                    PixelOffsetY);
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
            int PixelsPerBin,
            float PixelOffsetX,
            float PixelOffsetY
            )
        {
            double CellX = (Easting - Grid.MinX) / Grid.GridSizeM - 0.5;
            double CellY = (Northing - Grid.MinY) / Grid.GridSizeM - 0.5;
            float PixelX = (float)(CellX * PixelsPerBin + PixelsPerBin * 0.5) - PixelOffsetX;
            float PixelY = (float)((Grid.Height - 1 - CellY) * PixelsPerBin + PixelsPerBin * 0.5) - PixelOffsetY;
            return (PixelX, PixelY);
        }

        /// <summary>
        /// Builds an ordered lat/lon ring from the survey boundary or field bounds fallback.
        /// </summary>
        private static List<(double Lat, double Lon)> BuildBoundaryLatLonRing
            (
            ProjectDocument Project
            )
        {
            SurveyModel Survey = Project.Survey!;
            if (Survey.BoundaryPoints.Count >= GeoConstants.MIN_POLYGON_VERTICES)
            {
                List<(double Lat, double Lon)> Unordered = new List<(double Lat, double Lon)>(Survey.BoundaryPoints.Count);
                foreach (TopologyPoint Point in Survey.BoundaryPoints)
                    Unordered.Add((Point.Latitude, Point.Longitude));

                return BoundaryMask.OrderBoundaryLatLon(Unordered);
            }

            if (!FieldBounds.TryFromProject(Project, out FieldBounds Bounds))
                return new List<(double Lat, double Lon)>();

            return new List<(double Lat, double Lon)>
            {
                (Bounds.MinLat, Bounds.MinLon),
                (Bounds.MinLat, Bounds.MaxLon),
                (Bounds.MaxLat, Bounds.MaxLon),
                (Bounds.MaxLat, Bounds.MinLon)
            };
        }

        /// <summary>
        /// Maps a UTM point to haul-grid bitmap pixel coordinates.
        /// </summary>
        private static (float PixelX, float PixelY) EastingNorthingToPixel
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
