namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Places the haul engine's finished road and well-pad section on the displayed surface.
    /// Pan trips are not aligned to the designer sample grid, so cell depths are not
    /// re-simulated into that grid. The section is the same crown, side slope, and pad
    /// cone the haul built, evaluated at the sample point.
    /// </summary>
    internal sealed class HaulRoadSurface
    {
        /// <summary>Segments within this angle of a grid axis are snapped onto the pan, matching the haul engine.</summary>
        private const double AXIS_SNAP_RADIANS = Math.PI / 180.0;

        /// <summary>Segment length below this is not a road section.</summary>
        private const double DEGENERATE_LENGTH_M = 1e-9;

        /// <summary>Cell size below this is not snapped.</summary>
        private const double DEGENERATE_CELL_M = 1e-9;

        /// <summary>Odd lane counts sit on a cell centre, which is half a cell from the corner index.</summary>
        private const double CELL_CENTER_INDEX_OFFSET = 0.5;

        /// <summary>Midpoint of a segment, as a fraction of the way from the start to the end.</summary>
        private const double SEGMENT_MIDPOINT_FRACTION = 0.5;

        /// <summary>Embankment below this is field, not road or pad.</summary>
        private const double EMBANKMENT_EPSILON_M = 1e-5;

        /// <summary>Snapped two-point roads. Each segment is snapped the way the haul pan is snapped.</summary>
        private readonly List<HaulBuilder.RoadCentreline> _Segments;

        /// <summary>Well pads supplied to the haul.</summary>
        private readonly List<HaulBuilder.WellPad> _Pads;

        /// <summary>Haul options that own the road and pad defaults.</summary>
        private readonly HaulBuilder.HaulBuilderOptions _Options;

        /// <summary>Fitted field design under the embankment.</summary>
        private readonly HaulBuilder.RoadPadSurface _Surface;

        /// <summary>Grid origin and cell size the field design was fitted on.</summary>
        private readonly HaulBuilder.Grid _Grid;

        /// <summary>
        /// Builds the finished section for one haul grid.
        /// </summary>
        /// <param name="Grid">Haul grid the field design was fitted on.</param>
        /// <param name="Surface">Field design from the haul run.</param>
        /// <param name="Options">Roads, pads, and the defaults they inherit.</param>
        private HaulRoadSurface
            (
            HaulBuilder.Grid Grid,
            HaulBuilder.RoadPadSurface Surface,
            HaulBuilder.HaulBuilderOptions Options
            )
        {
            _Grid = Grid;
            _Surface = Surface;
            _Options = Options;
            _Pads = new List<HaulBuilder.WellPad>(Options.WellPads);
            _Segments = new List<HaulBuilder.RoadCentreline>();
            foreach (HaulBuilder.RoadCentreline Road in Options.Roads)
                AddSnappedSegments(Road);
        }

        /// <summary>
        /// Returns the finished section when this scrub frame should show it.
        /// The last scrub step is the final surface, including when drive order
        /// did not list the road trips. Earlier steps show it once a road trip
        /// has already been driven.
        /// </summary>
        /// <param name="Data">Haul data for the open project.</param>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <param name="Grid">Haul grid the section is evaluated on.</param>
        /// <returns>The section, or null when this frame is still field work.</returns>
        public static HaulRoadSurface? ForFrame
            (
            HaulData Data,
            int RouteScrubIndex,
            HaulBuilder.Grid Grid
            )
        {
            HaulBuilder.RoadPadSurface? Surface = Data.Result?.RoadPads;
            HaulBuilder.HaulBuilderOptions? Options = Data.Result?.Options;
            if (Surface == null || Options == null || Grid == null)
                return null;

            if (!ShowsFinishedSection(Data, RouteScrubIndex))
                return null;

            return new HaulRoadSurface(Grid, Surface, Options);
        }

        /// <summary>
        /// Builds the finished section from a haul surface that is already in hand.
        /// </summary>
        /// <param name="Grid">Haul grid the field design was fitted on.</param>
        /// <param name="Surface">Field design from the haul.</param>
        /// <param name="Options">Roads, pads, and the defaults they inherit.</param>
        /// <returns>The section.</returns>
        public static HaulRoadSurface FromEngine
            (
            HaulBuilder.Grid Grid,
            HaulBuilder.RoadPadSurface Surface,
            HaulBuilder.HaulBuilderOptions Options
            )
        {
            return new HaulRoadSurface(Grid, Surface, Options);
        }

        /// <summary>
        /// Writes the finished section onto cell centres. Cells off the embankment are left alone.
        /// </summary>
        /// <param name="Grid">Leveled grid that shares origin and cell size with the fitted design.</param>
        public void StampCellCentres
            (
            HaulBuilder.Grid Grid
            )
        {
            int Width = Math.Min(Grid.Width, _Surface.FieldDesignM.GetLength(1));
            int Height = Math.Min(Grid.Height, _Surface.FieldDesignM.GetLength(0));
            for (int Row = 0; Row < Height; Row++)
            {
                for (int Col = 0; Col < Width; Col++)
                {
                    if (!Grid.Valid[Row, Col])
                        continue;

                    // intent: the centre is where this cell is displayed; the section is not the pan depth
                    if (TryElevation(Grid.CenterX[Row, Col], Grid.CenterY[Row, Col], out double Elevation))
                        Grid.Existing[Row, Col] = Elevation;
                }
            }
        }

        /// <summary>
        /// Returns the finished elevation at a project coordinate when it lies on the road or pad.
        /// </summary>
        /// <param name="Easting">Easting in metres.</param>
        /// <param name="Northing">Northing in metres.</param>
        /// <param name="Elevation">Field design plus the haul section, in metres.</param>
        /// <returns>True when the point is on the embankment.</returns>
        public bool TryElevation
            (
            double Easting,
            double Northing,
            out double Elevation
            )
        {
            Elevation = 0;
            double EmbankmentM = EmbankmentAt(Easting, Northing);
            if (EmbankmentM <= EMBANKMENT_EPSILON_M)
                return false;

            double FieldM = SampleFieldDesign(Easting, Northing);
            if (!double.IsFinite(FieldM))
                return false;

            Elevation = FieldM + EmbankmentM;
            return true;
        }

        /// <summary>
        /// True when this scrub frame should draw the finished road and pad.
        /// </summary>
        /// <param name="Data">Haul data for the open project.</param>
        /// <param name="RouteScrubIndex">Zero-based route scrub index.</param>
        /// <returns>True on the final step, and once a road trip is already driven.</returns>
        private static bool ShowsFinishedSection
            (
            HaulData Data,
            int RouteScrubIndex
            )
        {
            if (Data.RouteStepCount > 0 && RouteScrubIndex >= Data.RouteStepCount - 1)
                return true;

            foreach (HaulBuilder.Pass Pass in Data.PassesBeforeDriveStep(RouteScrubIndex))
            {
                if (Pass.Stage == HaulBuilder.HaulStage.Road)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Adds one snapped two-point road per centreline segment.
        /// </summary>
        /// <param name="Road">Centreline from the haul options.</param>
        private void AddSnappedSegments
            (
            HaulBuilder.RoadCentreline Road
            )
        {
            if (Road.Vertices.Count < 2)
                return;

            // intent: odd lane counts centre on a cell centre, even counts on a boundary, same as the pan
            int Lanes = LaneCount(Road);
            bool OnBoundary = Lanes % 2 == 0;
            for (int Index = 1; Index < Road.Vertices.Count; Index++)
            {
                (double X, double Y) Start = Road.Vertices[Index - 1];
                (double X, double Y) End = Road.Vertices[Index];
                SnapSegment(Start.X, Start.Y, End.X, End.Y, OnBoundary, out double Ax, out double Ay, out double Bx, out double By);
                double Dx = Bx - Ax;
                double Dy = By - Ay;
                if ((Dx * Dx) + (Dy * Dy) <= DEGENERATE_LENGTH_M * DEGENERATE_LENGTH_M)
                    continue;

                _Segments.Add(SegmentRoad(Road, Ax, Ay, Bx, By));
            }
        }

        /// <summary>
        /// Copies one centreline segment and the overrides that size its section.
        /// </summary>
        /// <param name="Source">Road the segment belongs to.</param>
        /// <param name="Ax">Snapped start easting.</param>
        /// <param name="Ay">Snapped start northing.</param>
        /// <param name="Bx">Snapped end easting.</param>
        /// <param name="By">Snapped end northing.</param>
        /// <returns>A two-point road the haul height query can evaluate.</returns>
        private static HaulBuilder.RoadCentreline SegmentRoad
            (
            HaulBuilder.RoadCentreline Source,
            double Ax,
            double Ay,
            double Bx,
            double By
            )
        {
            return new HaulBuilder.RoadCentreline(new (double X, double Y)[]
            {
                (Ax, Ay),
                (Bx, By)
            })
            {
                HeightFt = Source.HeightFt,
                SideSlope = Source.SideSlope,
                CrossSlopePct = Source.CrossSlopePct,
                MinRoadbedFt = Source.MinRoadbedFt,
                MinLanes = Source.MinLanes,
                MaxGradePct = Source.MaxGradePct
            };
        }

        /// <summary>
        /// Lane count the haul uses for this road, including its overrides.
        /// </summary>
        /// <param name="Road">Centreline.</param>
        /// <returns>Pan lane count.</returns>
        private int LaneCount
            (
            HaulBuilder.RoadCentreline Road
            )
        {
            double LaneFt = HaulBuilder.RoadPadBuild.LaneWidthFt(_Options);
            double RoadbedFt = Road.MinRoadbedFt ?? _Options.RoadNrcsMinRoadbedFt;
            int MinLanes = Road.MinLanes ?? _Options.RoadMinLanes;
            double HeightFt = Road.HeightFt ?? _Options.RoadHeightFt;
            double SideSlope = Road.SideSlope ?? _Options.RoadSideSlope;
            return HaulBuilder.RoadPadBuild.PanLaneCount(LaneFt, RoadbedFt, MinLanes, SideSlope, HeightFt);
        }

        /// <summary>
        /// Moves an axis-aligned segment onto the pan line the haul builds.
        /// </summary>
        /// <param name="Ax">Start easting.</param>
        /// <param name="Ay">Start northing.</param>
        /// <param name="Bx">End easting.</param>
        /// <param name="By">End northing.</param>
        /// <param name="OnBoundary">True when an even lane count centres on a cell boundary.</param>
        /// <param name="SnappedAx">Snapped start easting.</param>
        /// <param name="SnappedAy">Snapped start northing.</param>
        /// <param name="SnappedBx">Snapped end easting.</param>
        /// <param name="SnappedBy">Snapped end northing.</param>
        private void SnapSegment
            (
            double Ax,
            double Ay,
            double Bx,
            double By,
            bool OnBoundary,
            out double SnappedAx,
            out double SnappedAy,
            out double SnappedBx,
            out double SnappedBy
            )
        {
            SnappedAx = Ax;
            SnappedAy = Ay;
            SnappedBx = Bx;
            SnappedBy = By;
            double Dx = Bx - Ax;
            double Dy = By - Ay;
            if (Math.Sqrt((Dx * Dx) + (Dy * Dy)) <= DEGENERATE_LENGTH_M)
                return;

            // intent: fold the heading into 0–90° so east-west and north-south share one test
            double Angle = Math.Abs(Math.Atan2(Dy, Dx));
            if (Angle > Math.PI / 2.0)
                Angle = Math.PI - Angle;

            if (Angle <= AXIS_SNAP_RADIANS)
            {
                double SnappedNorthing = SnapCoordinate((Ay + By) * SEGMENT_MIDPOINT_FRACTION, _Grid.MinY, _Grid.GridSizeM, OnBoundary);
                SnappedAy = SnappedNorthing;
                SnappedBy = SnappedNorthing;
            }
            else if ((Math.PI / 2.0) - Angle <= AXIS_SNAP_RADIANS)
            {
                double SnappedEasting = SnapCoordinate((Ax + Bx) * SEGMENT_MIDPOINT_FRACTION, _Grid.MinX, _Grid.GridSizeM, OnBoundary);
                SnappedAx = SnappedEasting;
                SnappedBx = SnappedEasting;
            }
        }

        /// <summary>
        /// Snaps one coordinate onto a cell centre or boundary, and never moves it more than half a cell.
        /// </summary>
        /// <param name="Coord">Coordinate in metres.</param>
        /// <param name="Origin">Grid minimum on that axis.</param>
        /// <param name="Cell">Cell size in metres.</param>
        /// <param name="OnBoundary">True to snap to a boundary, false to snap to a centre.</param>
        /// <returns>Snapped coordinate in metres.</returns>
        private static double SnapCoordinate
            (
            double Coord,
            double Origin,
            double Cell,
            bool OnBoundary
            )
        {
            if (Cell <= DEGENERATE_CELL_M)
                return Coord;

            double Index = (Coord - Origin) / Cell;
            double SnappedIndex = OnBoundary
                ? Math.Round(Index, MidpointRounding.AwayFromZero)
                : Math.Round(Index - CELL_CENTER_INDEX_OFFSET, MidpointRounding.AwayFromZero) + CELL_CENTER_INDEX_OFFSET;
            double Snapped = Origin + (SnappedIndex * Cell);
            double HalfCell = Cell * CELL_CENTER_INDEX_OFFSET;
            double Shift = Snapped - Coord;
            if (Shift > HalfCell)
                return Coord + HalfCell;

            if (Shift < -HalfCell)
                return Coord - HalfCell;

            return Snapped;
        }

        /// <summary>
        /// Highest road or pad section at a point, in metres above the field design.
        /// </summary>
        /// <param name="Easting">Easting in metres.</param>
        /// <param name="Northing">Northing in metres.</param>
        /// <returns>Embankment height in metres. Zero off the works.</returns>
        private double EmbankmentAt
            (
            double Easting,
            double Northing
            )
        {
            double BestM = 0;
            foreach (HaulBuilder.RoadCentreline Segment in _Segments)
                BestM = Math.Max(BestM, HaulBuilder.RoadPadBuild.RoadHeightM(Easting, Northing, Segment, _Options));

            foreach (HaulBuilder.WellPad Pad in _Pads)
                BestM = Math.Max(BestM, HaulBuilder.RoadPadBuild.PadHeightM(Easting, Northing, Pad, _Options));

            return BestM;
        }

        /// <summary>
        /// Bilinear field-design elevation at a project coordinate.
        /// </summary>
        /// <param name="Easting">Easting in metres.</param>
        /// <param name="Northing">Northing in metres.</param>
        /// <returns>Elevation in metres, or NaN outside the field.</returns>
        private double SampleFieldDesign
            (
            double Easting,
            double Northing
            )
        {
            // intent: field-design samples live at cell centres, same convention as the haul
            double ColFrac = ((Easting - _Grid.MinX) / _Grid.GridSizeM) - CELL_CENTER_INDEX_OFFSET;
            double RowFrac = ((Northing - _Grid.MinY) / _Grid.GridSizeM) - CELL_CENTER_INDEX_OFFSET;
            int Col0 = (int)Math.Floor(ColFrac);
            int Row0 = (int)Math.Floor(RowFrac);
            double Tx = ColFrac - Col0;
            double Ty = RowFrac - Row0;
            double E00 = DesignAt(Col0, Row0);
            double E10 = DesignAt(Col0 + 1, Row0);
            double E01 = DesignAt(Col0, Row0 + 1);
            double E11 = DesignAt(Col0 + 1, Row0 + 1);
            if (double.IsFinite(E00) && double.IsFinite(E10) && double.IsFinite(E01) && double.IsFinite(E11))
            {
                double Top = (E00 * (1.0 - Tx)) + (E10 * Tx);
                double Bottom = (E01 * (1.0 - Tx)) + (E11 * Tx);
                return (Top * (1.0 - Ty)) + (Bottom * Ty);
            }

            int Col = Math.Clamp((int)Math.Round(ColFrac), 0, _Grid.Width - 1);
            int Row = Math.Clamp((int)Math.Round(RowFrac), 0, _Grid.Height - 1);
            return DesignAt(Col, Row);
        }

        /// <summary>
        /// Reads one fitted field-design cell.
        /// </summary>
        /// <param name="Col">Column.</param>
        /// <param name="Row">Row.</param>
        /// <returns>Elevation in metres, or NaN when the cell is outside the field.</returns>
        private double DesignAt
            (
            int Col,
            int Row
            )
        {
            if (Col < 0 || Row < 0 || Col >= _Grid.Width || Row >= _Grid.Height || !_Grid.Valid[Row, Col])
                return double.NaN;

            if (Row >= _Surface.FieldDesignM.GetLength(0) || Col >= _Surface.FieldDesignM.GetLength(1))
                return double.NaN;

            return _Surface.FieldDesignM[Row, Col];
        }
    }
}
