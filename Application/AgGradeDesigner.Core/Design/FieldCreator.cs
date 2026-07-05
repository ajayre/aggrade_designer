using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Design
{
    /// <summary>
    /// Builds a field SQLite database from the survey in <see cref="FieldDesign.SurveyFileName"/> (.txt multiplane or .ags)
    /// and a <see cref="FieldDesign"/>, matching <c>Field Creator/fieldcreator.py</c> schema and data keys.
    /// Existing elevations are generated over the full grid using the same MLS + warp smoothing pipeline as
    /// <c>AI Field Design/fielddesign.py</c> <c>build_all_bins</c>, so sparse surveys and dense surveys use one consistent
    /// field-surface construction path before design-plane solve / DB write.
    /// Does not write an AGD file; HaulPaths and HaulArrows tables are left empty.
    /// Pass <see cref="Progress"/> (constructor) to receive user-facing status strings during long steps.
    /// </summary>

    public class FieldCreator
    {
        /// <summary>Optional callback invoked with a short English message as the pipeline advances (UI thread safe is the caller's responsibility).</summary>
        public Action<string>? Progress { get; }

        /// <summary>
        /// Creates a field builder with an optional progress reporter for long-running grid and surface steps.
        /// </summary>
        /// <param name="ProgressCallback">Optional handler receiving status strings during pipeline execution.</param>
        public FieldCreator
            (
            Action<string>? ProgressCallback = null
            )
        {
            Progress = ProgressCallback;
        }

        /// <summary>
        /// Forwards a status string to <see cref="Progress"/> when a handler is attached.
        /// </summary>
        /// <param name="Message">User-facing pipeline status text.</param>
        private void ReportProgress
            (
            string Message
            )
        {
            Progress?.Invoke(Message);
        }

        /// <summary>
        /// Emits the cut/fill and composite slope summary lines after grid construction completes.
        /// </summary>
        /// <param name="S">Aggregated design statistics from the solved plane.</param>
        private void ReportStatisticsSummary
            (
            Statistics S
            )
        {
            // Emit human-readable acreage and slope summary lines after the grid solve completes.
            IFormatProvider Fp = CultureInfo.InvariantCulture;
            ReportProgress("Total area: " + S.TotalAreaAcres.ToString($"F{STATISTICS_AREA_DECIMALS}", Fp) + " acres");
            ReportProgress("Total cut: " + S.TotalCutCY.ToString($"F{STATISTICS_VOLUME_DECIMALS}", Fp) + " cu yd");
            ReportProgress("Total fill: " + S.TotalFillCY.ToString($"F{STATISTICS_VOLUME_DECIMALS}", Fp) + " cu yd");
            ReportProgress("Composite slope heading: " + S.CompositeSlopeHeadingDeg.ToString($"F{STATISTICS_HEADING_DECIMALS}", Fp) + "°");
            ReportProgress("Composite slope: " + S.CompositeSlopePercent.ToString($"F{STATISTICS_SLOPE_DECIMALS}", Fp) + " %");
        }

        /// <summary>Spatial hash cell size in meters for k-nearest neighbor queries during MLS interpolation.</summary>
        private const double SPATIAL_HASH_CELL_M = 25.0;
        /// <summary>Maximum neighbor count for moving-least-squares elevation fit at each bin.</summary>
        private const int EMPTY_BIN_MLS_K = 64;
        /// <summary>Squared epsilon added to MLS weights to avoid division by zero at sample locations.</summary>
        private const double EMPTY_BIN_INTERP_EPS_SQ = FieldConstants.EPSILON_FINE;
        /// <summary>Regularization scale applied to the MLS normal equations trace.</summary>
        private const double EMPTY_BIN_MLS_REG_SCALE = 5e-6;
        /// <summary>Fraction of max neighbor distance used as MLS smoothing length scale.</summary>
        private const double EMPTY_BIN_MLS_SMOOTH_FRAC = 0.75;
        /// <summary>Box blur radius in bins for the baseline surface before residual warp.</summary>
        private const int WARP_BASE_RADIUS_BINS = 3;
        /// <summary>Box blur pass count for the baseline surface before residual warp.</summary>
        private const int WARP_BASE_PASSES = 2;
        /// <summary>Normalized residual warp radius in bins for the primary correction pass.</summary>
        private const int WARP_RADIUS_BINS = 12;
        /// <summary>Normalized residual warp pass count for the primary correction pass.</summary>
        private const int WARP_PASSES = 4;
        /// <summary>Blend factor applied to the primary normalized residual warp correction.</summary>
        private const double WARP_ALPHA = 0.85;
        /// <summary>Target maximum absolute elevation error at observed bins after warping, in meters.</summary>
        private const double WARP_TOL_M = 0.009;
        /// <summary>Iteration cap for tolerance-driven residual warp refinement.</summary>
        private const int WARP_TOL_ITERS = 8;
        /// <summary>Normalized residual warp radius in bins during tolerance refinement.</summary>
        private const int WARP_TOL_RADIUS_BINS = 8;
        /// <summary>Normalized residual warp pass count during tolerance refinement.</summary>
        private const int WARP_TOL_PASSES = 3;
        /// <summary>Blend factor for tolerance refinement warp corrections (full strength).</summary>
        private const double WARP_TOL_ALPHA = 1.0;
        /// <summary>Post-warp box blur radius in bins before final tolerance pass.</summary>
        private const int WARP_POST_RADIUS_BINS = 4;
        /// <summary>Post-warp box blur pass count before final tolerance pass.</summary>
        private const int WARP_POST_PASSES = 3;
        /// <summary>Iteration cap for tolerance refinement after post-warp smoothing.</summary>
        private const int WARP_POST_TOL_ITERS = 6;
        /// <summary>Accept cut/fill ratio within this fraction of target when bisecting plane offset <c>c</c>.</summary>
        private const double RATIO_TOLERANCE = 0.02;
        /// <summary>Minimum margin (m) added when bracketing plane offset <c>c</c> from survey elevation range.</summary>
        private const double C_BRACKET_MARGIN_M = 1.0;
        /// <summary>Fraction of per-bin <c>c</c> span used as bracket margin when bracketing plane offset.</summary>
        private const double C_BRACKET_MARGIN_FRAC = 0.02;
        /// <summary>Maximum expansion steps when widening the <c>c</c> bracket for cut/fill ratio solve.</summary>
        private const int C_BRACKET_EXPAND_MAX = 24;
        /// <summary>Multiplier converting gradient magnitude to slope percent.</summary>
        private const double SLOPE_PERCENT_SCALE = 100.0;
        /// <summary>Minimum non-zero gradient magnitude for heading calculation.</summary>
        private const double GRADIENT_MAGNITUDE_EPSILON = 1e-18;
        /// <summary>Minimum polygon vertex count for inside tests.</summary>
        private const int MIN_POLYGON_VERTICES = 3;
        /// <summary>Minimum original neighbours required before filling a missing bin.</summary>
        private const int MIN_ORIG_NEIGHBORS_FOR_FILL = 2;
        /// <summary>Maximum bisection iterations when solving plane offset <c>c</c>.</summary>
        private const int SOLVE_C_MAX_ITERATIONS = 100;
        /// <summary>Minimum span (m) when bracketing plane offset from per-bin levels.</summary>
        private const double MIN_C_SPAN_M = 1e-6;
        /// <summary>Multiplier applied when widening the <c>c</c> bracket.</summary>
        private const double C_BRACKET_EXPAND_FACTOR = 1.5;
        /// <summary>Residual tolerance (m³) for early exit during plane offset bisection.</summary>
        private const double SOLVE_C_RESIDUAL_TOLERANCE = 1e-3;
        /// <summary>Interval width tolerance (m) for plane offset bisection.</summary>
        private const double SOLVE_C_INTERVAL_TOLERANCE = 1e-10;
        /// <summary>Extra spatial-hash ring padding beyond computed max ring.</summary>
        private const int SPATIAL_HASH_RING_PADDING = 3;
        /// <summary>Distance slack (m) when deciding spatial-hash ring search can stop.</summary>
        private const double SPATIAL_RING_SEARCH_EPSILON_M = 1e-6;
        /// <summary>Singular-matrix pivot threshold for 3×3 Gaussian elimination.</summary>
        private const double MATRIX_SINGULAR_EPSILON = 1e-20;
        /// <summary>Dimension of the MLS normal-equation system.</summary>
        private const int MLS_NORMAL_EQUATION_DIM = 3;
        /// <summary>Row interval for MLS interpolation progress reporting.</summary>
        private const int MLS_PROGRESS_ROW_DIVISOR = 10;
        /// <summary>Morphological close/open pass count for observed-point valid mask.</summary>
        private const int OBSERVED_MASK_MORPHOLOGY_PASSES = 2;
        /// <summary>Multiplier converting blur radius to inclusive window width.</summary>
        private const int BOX_BLUR_DIAMETER_FACTOR = 2;
        /// <summary>Minimum vertex count for a degenerate boundary ring comparison.</summary>
        private const int MIN_DEGENERATE_RING_VERTICES = 2;
        /// <summary>Eight-connected neighbour count for component labelling.</summary>
        private const int EIGHT_CONNECTED_NEIGHBOR_COUNT = 8;
        /// <summary>Minimum offset for morphological kernel neighbourhood scans.</summary>
        private const int MORPHOLOGY_KERNEL_MIN_OFFSET = -1;
        /// <summary>Maximum offset for morphological kernel neighbourhood scans.</summary>
        private const int MORPHOLOGY_KERNEL_MAX_OFFSET = 1;
        /// <summary>Minimum grid extent in metres when computing spatial-hash ring count.</summary>
        private const double MIN_GRID_EXTENT_METRES = 1.0;
        /// <summary>Numerator for inverse-distance MLS weights.</summary>
        private const double MLS_WEIGHT_NUMERATOR = 1.0;
        /// <summary>Mask value representing an observed bin in normalized residual warp.</summary>
        private const double OBSERVED_MASK_TRUE = 1.0;
        /// <summary>Default downhill heading when the design plane gradient is zero.</summary>
        private const double DEFAULT_DOWNHILL_HEADING_DEG = 0.0;
        /// <summary>Fallback elevation when no valid bins exist for warp fill.</summary>
        private const double DEFAULT_FILL_ELEVATION = 0.0;
        /// <summary>Decimal places for area statistics reporting.</summary>
        private const int STATISTICS_AREA_DECIMALS = 3;
        /// <summary>Decimal places for volume statistics reporting.</summary>
        private const int STATISTICS_VOLUME_DECIMALS = 2;
        /// <summary>Decimal places for slope heading statistics reporting.</summary>
        private const int STATISTICS_HEADING_DECIMALS = 2;
        /// <summary>Decimal places for slope percent statistics reporting.</summary>
        private const int STATISTICS_SLOPE_DECIMALS = 3;

        /// <summary>
        /// Summary values from <see cref="CreateFromSurveyAndDesign"/>.
        /// Composite slope/heading follow <c>AI Field Design/fielddesign.py</c> <c>composite_slope_and_heading</c>
        /// (steepest downhill grade from dz/dE=a, dz/dN=b).
        /// </summary>
        public readonly struct Statistics
        {
            /// <summary>Full bin grid footprint: <c>gridWidth × gridHeight × bin area</c>, in acres.</summary>
            /// <summary>Full bin grid footprint in acres.</summary>
            public double TotalAreaAcres { get; init; }
            /// <summary>Total cut volume in cubic yards across valid bins.</summary>
            public double TotalCutCY { get; init; }
            /// <summary>Total fill volume in cubic yards across valid bins.</summary>
            public double TotalFillCY { get; init; }
            /// <summary>Downhill direction on the design plane, degrees clockwise from north.</summary>
            public double CompositeSlopeHeadingDeg { get; init; }
            /// <summary>Steepest downhill grade of the design plane, percent (m/m × 100).</summary>
            public double CompositeSlopePercent { get; init; }
        }

        /// <summary>
        /// Builds a design grid from embedded survey data and design parameters.
        /// </summary>
        /// <param name="Survey">Survey topology and elevations to interpolate.</param>
        /// <param name="Parameters">Main/cross slope, cut-fill ratio, and import/export volumes.</param>
        /// <returns>Solved design grid and aggregated cut/fill statistics.</returns>
        public (DesignGrid Grid, Statistics Stats) CreateDesign
            (
            SurveyModel Survey,
            DesignParameters Parameters
            )
        {
            if (Survey == null)
                throw new ArgumentNullException(nameof(Survey));
            if (Parameters == null)
                throw new ArgumentNullException(nameof(Parameters));

            if (Survey.InteriorPoints.Count == 0 && Survey.BoundaryPoints.Count == 0)
                throw new InvalidDataException("Survey has no interior or boundary topology points.");

            ReportProgress("Building bin grid and existing surface…");
            // Convert import/export from cubic yards and apply default cut-fill ratio when unset.
            double Ratio = Parameters.CutFillRatio > 0 ? Parameters.CutFillRatio : FieldConstants.CUT_FILL_RATIO;
            double ImportM3 = CubicYardsToM3(Parameters.ImportToFieldCy);
            double ExportM3 = CubicYardsToM3(Parameters.ExportFromFieldCy);

            BuildGrid(
                Survey,
                Parameters.UseExistingTopology,
                (int)Math.Round(Parameters.MainSlopeDirectionDeg),
                Parameters.MainSlopePercent,
                Parameters.CrossSlopePercent,
                Ratio,
                ImportM3,
                ExportM3,
                out int GridW,
                out int GridH,
                out double MinX,
                out double MinY,
                out double MaxX,
                out double MaxY,
                out int UtmZone,
                out bool UtmNorth,
                out double MeanLat,
                out double MeanLon,
                out double TotalCutCy,
                out double TotalFillCy,
                out double PlaneA,
                out double PlaneB,
                out double PlaneC,
                out int ValidBinCount,
                out List<GridBinData> BinRows);

            CompositeSlopeAndHeading(PlaneA, PlaneB, out double CompositeSlopePct, out double CompositeHeadingDeg);
            double TotalAreaM2 = ValidBinCount * FieldConstants.BIN_SIZE_M * FieldConstants.BIN_SIZE_M;
            double TotalAreaAcres = TotalAreaM2 / FieldConstants.SQUARE_METERS_PER_ACRE;
            Statistics Stats = new Statistics
            {
                TotalAreaAcres = TotalAreaAcres,
                TotalCutCY = TotalCutCy,
                TotalFillCY = TotalFillCy,
                CompositeSlopeHeadingDeg = CompositeHeadingDeg,
                CompositeSlopePercent = CompositeSlopePct
            };

            ReportStatisticsSummary(Stats);
            ReportProgress("Done.");

            DesignGrid Grid = new DesignGrid
            {
                Width = GridW,
                Height = GridH,
                UtmZone = UtmZone,
                UtmNorth = UtmNorth,
                OriginEastingM = MinX,
                OriginNorthingM = MinY,
                MinX = MinX,
                MinY = MinY,
                MaxX = MaxX,
                MaxY = MaxY,
                PlaneA = PlaneA,
                PlaneB = PlaneB,
                PlaneC = PlaneC,
                MeanLat = MeanLat,
                MeanLon = MeanLon,
                TotalCutCy = TotalCutCy,
                TotalFillCy = TotalFillCy,
                CompositeSlopeHeadingDeg = CompositeHeadingDeg,
                CompositeSlopePercent = CompositeSlopePct,
                Bins = BinRows
            };

            return (Grid, Stats);
        }

        /// <summary>Matches <c>fielddesign.composite_slope_and_heading</c>.</summary>
        /// <param name="A">Design plane easting gradient dz/dE.</param>
        /// <param name="B">Design plane northing gradient dz/dN.</param>
        /// <param name="SlopePercent">Steepest downhill grade as percent (m/m × 100).</param>
        /// <param name="DownhillHeadingDegCwFromNorth">Downhill direction in degrees clockwise from north.</param>
        private void CompositeSlopeAndHeading
            (
            double A,
            double B,
            out double SlopePercent,
            out double DownhillHeadingDegCwFromNorth
            )
        {
            double Mag = Math.Sqrt(A * A + B * B);
            SlopePercent = Mag * SLOPE_PERCENT_SCALE;
            // Downhill follows the negative gradient of the design plane.
            double De = -A;
            double Dn = -B;
            if (Math.Abs(De) < GRADIENT_MAGNITUDE_EPSILON && Math.Abs(Dn) < GRADIENT_MAGNITUDE_EPSILON)
            {
                DownhillHeadingDegCwFromNorth = DEFAULT_DOWNHILL_HEADING_DEG;
                return;
            }

            double H = Math.Atan2(De, Dn) * (FieldConstants.DEGREES_PER_RADIAN / Math.PI);
            DownhillHeadingDegCwFromNorth = (H % FieldConstants.DEGREES_FULL_CIRCLE + FieldConstants.DEGREES_FULL_CIRCLE) % FieldConstants.DEGREES_FULL_CIRCLE;
        }

        /// <summary>
        /// Orders boundary vertices counter-clockwise around their centroid for stable polygon tests.
        /// </summary>
        /// <param name="Points">Unordered boundary vertices in UTM coordinates.</param>
        /// <returns>Vertices sorted by polar angle about the centroid.</returns>
        private List<(double X, double Y)> SortBoundaryRingByAngle
            (
            List<(double X, double Y)> Points
            )
        {
            if (Points.Count <= MIN_DEGENERATE_RING_VERTICES)
                return new List<(double X, double Y)>(Points);
            // Sort by polar angle about the centroid so polygon tests use a consistent winding.
            double Cx = 0;
            double Cy = 0;
            foreach ((double X, double Y) in Points)
            {
                Cx += X;
                Cy += Y;
            }
            Cx /= Points.Count;
            Cy /= Points.Count;
            List<(double X, double Y)> Sorted = new List<(double X, double Y)>(Points);
            Sorted.Sort((A, B) => Math.Atan2(A.Y - Cy, A.X - Cx).CompareTo(Math.Atan2(B.Y - Cy, B.X - Cx)));
            return Sorted;
        }

        /// <summary>
        /// Computes the perimeter length of a closed polyline through the given vertices.
        /// </summary>
        /// <param name="Points">Vertices in traversal order; the path closes from last to first.</param>
        /// <returns>Total edge length in the same units as vertex coordinates.</returns>
        private double ClosedPathLength
            (
            List<(double X, double Y)> Points
            )
        {
            if (Points.Count < MIN_DEGENERATE_RING_VERTICES)
                return 0.0;
            // Sum edge lengths around the closed ring including the closing segment.
            double Total = 0.0;
            int N = Points.Count;
            for (int i = 0; i < N; i++)
            {
                (double X0, double Y0) = Points[i];
                (double X1, double Y1) = Points[(i + 1) % N];
                Total += Math.Sqrt((X1 - X0) * (X1 - X0) + (Y1 - Y0) * (Y1 - Y0));
            }
            return Total;
        }

        /// <summary>
        /// Picks the shorter closed boundary ring between file order and angle-sorted order.
        /// </summary>
        /// <param name="Points">Boundary vertices as read from the survey.</param>
        /// <returns>Ring expected to represent the field perimeter with minimal spurious edges.</returns>
        private List<(double X, double Y)> ChooseBoundaryRing
            (
            List<(double X, double Y)> Points
            )
        {
            if (Points.Count <= MIN_DEGENERATE_RING_VERTICES)
                return new List<(double X, double Y)>(Points);
            // Prefer the shorter closed path to avoid self-crossing boundary order from the file.
            List<(double X, double Y)> Seq = new List<(double X, double Y)>(Points);
            List<(double X, double Y)> Ang = SortBoundaryRingByAngle(Points);
            return ClosedPathLength(Seq) <= ClosedPathLength(Ang) ? Seq : Ang;
        }

        /// <summary>
        /// Tests whether a point lies inside or on the edge of a simple polygon ring.
        /// </summary>
        /// <param name="X">Point easting in the same frame as the ring.</param>
        /// <param name="Y">Point northing in the same frame as the ring.</param>
        /// <param name="Ring">Closed polygon vertices.</param>
        /// <returns>True when the point is inside or on the boundary.</returns>
        private bool PointInPolygon
            (
            double X,
            double Y,
            List<(double X, double Y)> Ring
            )
        {
            int N = Ring.Count;
            if (N < MIN_POLYGON_VERTICES)
                return false;
            // Ray-casting test with on-edge tolerance for bin centroid inclusion.
            bool Inside = false;
            int J = N - 1;
            for (int i = 0; i < N; i++)
            {
                double Xi = Ring[i].X;
                double Yi = Ring[i].Y;
                double Xj = Ring[J].X;
                double Yj = Ring[J].Y;

                double MinX = Math.Min(Xi, Xj) - FieldConstants.EPSILON_FINE;
                double MaxX = Math.Max(Xi, Xj) + FieldConstants.EPSILON_FINE;
                double MinY = Math.Min(Yi, Yj) - FieldConstants.EPSILON_FINE;
                double MaxY = Math.Max(Yi, Yj) + FieldConstants.EPSILON_FINE;
                double Cross = (X - Xi) * (Yj - Yi) - (Y - Yi) * (Xj - Xi);
                if (Math.Abs(Cross) <= FieldConstants.EPSILON_FINE && X >= MinX && X <= MaxX && Y >= MinY && Y <= MaxY)
                    return true;

                bool Intersects = (Yi > Y) != (Yj > Y);
                if (Intersects)
                {
                    double XAtY = (Xj - Xi) * (Y - Yi) / ((Yj - Yi) + FieldConstants.EPSILON_FINE) + Xi;
                    if (X <= XAtY)
                        Inside = !Inside;
                }
                J = i;
            }
            return Inside;
        }

        /// <summary>
        /// Marks grid bins whose centroids fall inside the survey boundary polygon.
        /// </summary>
        /// <param name="BoundaryRing">Boundary vertices in UTM coordinates.</param>
        /// <param name="MinX">Grid origin easting in meters.</param>
        /// <param name="MinY">Grid origin northing in meters.</param>
        /// <param name="GridW">Grid width in bins.</param>
        /// <param name="GridH">Grid height in bins.</param>
        /// <returns>Flat mask indexed by row-major bin order.</returns>
        private bool[] BuildValidMaskFromBoundary
            (
            List<(double X, double Y)> BoundaryRing,
            double MinX,
            double MinY,
            int GridW,
            int GridH
            )
        {
            int NCells = GridW * GridH;
            bool[] Mask = new bool[NCells];
            if (BoundaryRing.Count < MIN_POLYGON_VERTICES)
            {
                // Without a polygon, treat the padded extent as fully valid.
                for (int i = 0; i < NCells; i++)
                    Mask[i] = true;
                return Mask;
            }

            List<(double X, double Y)> Ring = ChooseBoundaryRing(BoundaryRing);
            for (int By = 0; By < GridH; By++)
            {
                double Cy = MinY + (By + FieldConstants.BIN_CENTER_OFFSET) * FieldConstants.BIN_SIZE_M;
                int Row = By * GridW;
                for (int Bx = 0; Bx < GridW; Bx++)
                {
                    double Cx = MinX + (Bx + FieldConstants.BIN_CENTER_OFFSET) * FieldConstants.BIN_SIZE_M;
                    Mask[Row + Bx] = PointInPolygon(Cx, Cy, Ring);
                }
            }

            return Mask;
        }

        /// <summary>
        /// Expands true cells in a binary mask using an 8-connected structuring element.
        /// </summary>
        /// <param name="Mask">Input flat mask in row-major order.</param>
        /// <param name="Width">Grid width in cells.</param>
        /// <param name="Height">Grid height in cells.</param>
        /// <param name="Passes">Number of dilation iterations to apply.</param>
        /// <returns>Dilated mask with the same dimensions as the input.</returns>
        private bool[] BinaryDilate
            (
            bool[] Mask,
            int Width,
            int Height,
            int Passes
            )
        {
            bool[] OutMask = new bool[Mask.Length];
            Array.Copy(Mask, OutMask, Mask.Length);
            // Expand foreground with an 8-connected structuring element for each pass.
            for (int P = 0; P < Math.Max(0, Passes); P++)
            {
                bool[] Next = new bool[OutMask.Length];
                for (int Y = 0; Y < Height; Y++)
                {
                    for (int X = 0; X < Width; X++)
                    {
                        bool Any = false;
                        for (int Dy = MORPHOLOGY_KERNEL_MIN_OFFSET; Dy <= MORPHOLOGY_KERNEL_MAX_OFFSET && !Any; Dy++)
                        {
                            int Yy = Y + Dy;
                            if (Yy < 0 || Yy >= Height)
                                continue;
                            for (int Dx = MORPHOLOGY_KERNEL_MIN_OFFSET; Dx <= MORPHOLOGY_KERNEL_MAX_OFFSET; Dx++)
                            {
                                int Xx = X + Dx;
                                if (Xx < 0 || Xx >= Width)
                                    continue;
                                if (OutMask[Yy * Width + Xx])
                                {
                                    Any = true;
                                    break;
                                }
                            }
                        }
                        Next[Y * Width + X] = Any;
                    }
                }
                OutMask = Next;
            }
            return OutMask;
        }

        /// <summary>
        /// Shrinks true cells in a binary mask using an 8-connected structuring element.
        /// </summary>
        /// <param name="Mask">Input flat mask in row-major order.</param>
        /// <param name="Width">Grid width in cells.</param>
        /// <param name="Height">Grid height in cells.</param>
        /// <param name="Passes">Number of erosion iterations to apply.</param>
        /// <returns>Eroded mask with the same dimensions as the input.</returns>
        private bool[] BinaryErode
            (
            bool[] Mask,
            int Width,
            int Height,
            int Passes
            )
        {
            bool[] OutMask = new bool[Mask.Length];
            Array.Copy(Mask, OutMask, Mask.Length);
            // Shrink foreground only where the full 8-neighbourhood remains true.
            for (int P = 0; P < Math.Max(0, Passes); P++)
            {
                bool[] Next = new bool[OutMask.Length];
                for (int Y = 0; Y < Height; Y++)
                {
                    for (int X = 0; X < Width; X++)
                    {
                        bool All = true;
                        for (int Dy = MORPHOLOGY_KERNEL_MIN_OFFSET; Dy <= MORPHOLOGY_KERNEL_MAX_OFFSET && All; Dy++)
                        {
                            int Yy = Y + Dy;
                            if (Yy < 0 || Yy >= Height)
                            {
                                All = false;
                                break;
                            }
                            for (int Dx = MORPHOLOGY_KERNEL_MIN_OFFSET; Dx <= MORPHOLOGY_KERNEL_MAX_OFFSET; Dx++)
                            {
                                int Xx = X + Dx;
                                if (Xx < 0 || Xx >= Width || !OutMask[Yy * Width + Xx])
                                {
                                    All = false;
                                    break;
                                }
                            }
                        }
                        Next[Y * Width + X] = All;
                    }
                }
                OutMask = Next;
            }
            return OutMask;
        }

        /// <summary>
        /// Fills enclosed false regions inside a true foreground mask.
        /// </summary>
        /// <param name="Mask">Foreground mask in row-major order.</param>
        /// <param name="Width">Grid width in cells.</param>
        /// <param name="Height">Grid height in cells.</param>
        /// <returns>Mask with interior holes set to true.</returns>
        private bool[] FillHoles
            (
            bool[] Mask,
            int Width,
            int Height
            )
        {
            bool[] Inv = new bool[Mask.Length];
            for (int i = 0; i < Mask.Length; i++)
                Inv[i] = !Mask[i];

            bool[] Outside = new bool[Mask.Length];
            Queue<(int X, int Y)> Q = new Queue<(int X, int Y)>();

            /// <summary>Enqueues an exterior false cell reachable from the grid border flood fill.</summary>
            void TryEnqueue
                (
                int X,
                int Y
                )
            {
                if (X < 0 || X >= Width || Y < 0 || Y >= Height)
                    return;
                int I = Y * Width + X;
                if (!Inv[I] || Outside[I])
                    return;
                Outside[I] = true;
                Q.Enqueue((X, Y));
            }

            // Seed flood fill from all border cells to label exterior background.
            for (int X = 0; X < Width; X++)
            {
                TryEnqueue(X, 0);
                TryEnqueue(X, Height - 1);
            }
            for (int Y = 0; Y < Height; Y++)
            {
                TryEnqueue(0, Y);
                TryEnqueue(Width - 1, Y);
            }

            while (Q.Count > 0)
            {
                (int X, int Y) = Q.Dequeue();
                TryEnqueue(X - 1, Y);
                TryEnqueue(X + 1, Y);
                TryEnqueue(X, Y - 1);
                TryEnqueue(X, Y + 1);
            }

            bool[] OutMask = new bool[Mask.Length];
            for (int i = 0; i < Mask.Length; i++)
            {
                bool Hole = Inv[i] && !Outside[i];
                OutMask[i] = Mask[i] || Hole;
            }
            return OutMask;
        }

        /// <summary>
        /// Retains only the largest 8-connected true component in a binary mask.
        /// </summary>
        /// <param name="Mask">Input flat mask in row-major order.</param>
        /// <param name="Width">Grid width in cells.</param>
        /// <param name="Height">Grid height in cells.</param>
        /// <returns>Mask containing only the largest connected foreground region.</returns>
        private bool[] LargestComponent
            (
            bool[] Mask,
            int Width,
            int Height
            )
        {
            bool[] Seen = new bool[Mask.Length];
            List<int> Best = new List<int>();
            int[] Nx = new int[EIGHT_CONNECTED_NEIGHBOR_COUNT] { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] Ny = new int[EIGHT_CONNECTED_NEIGHBOR_COUNT] { -1, -1, -1, 0, 0, 1, 1, 1 };

            // Flood-fill each 8-connected component and retain the largest one.
            for (int Y0 = 0; Y0 < Height; Y0++)
            {
                for (int X0 = 0; X0 < Width; X0++)
                {
                    int I0 = Y0 * Width + X0;
                    if (Seen[I0] || !Mask[I0])
                        continue;
                    List<int> Comp = new List<int>();
                    Stack<(int X, int Y)> St = new Stack<(int X, int Y)>();
                    Seen[I0] = true;
                    St.Push((X0, Y0));
                    while (St.Count > 0)
                    {
                        (int X, int Y) = St.Pop();
                        int I = Y * Width + X;
                        Comp.Add(I);
                        for (int k = 0; k < EIGHT_CONNECTED_NEIGHBOR_COUNT; k++)
                        {
                            int Xx = X + Nx[k];
                            int Yy = Y + Ny[k];
                            if (Xx < 0 || Xx >= Width || Yy < 0 || Yy >= Height)
                                continue;
                            int Ii = Yy * Width + Xx;
                            if (Seen[Ii] || !Mask[Ii])
                                continue;
                            Seen[Ii] = true;
                            St.Push((Xx, Yy));
                        }
                    }
                    if (Comp.Count > Best.Count)
                        Best = Comp;
                }
            }

            bool[] OutMask = new bool[Mask.Length];
            foreach (int I in Best)
                OutMask[I] = true;
            return OutMask;
        }

        /// <summary>
        /// Builds a valid-bin mask from observed survey points when no boundary polygon is available.
        /// </summary>
        /// <param name="UtmPoints">Survey samples in UTM coordinates with boundary flags.</param>
        /// <param name="MinX">Grid origin easting in meters.</param>
        /// <param name="MinY">Grid origin northing in meters.</param>
        /// <param name="GridW">Grid width in bins.</param>
        /// <param name="GridH">Grid height in bins.</param>
        /// <returns>Flat mask of bins considered inside the inferred field envelope.</returns>
        private bool[] BuildValidMaskFromObservedPoints
            (
            List<(double E, double N, double Z, bool IsBoundary, double Lat, double Lon)> UtmPoints,
            double MinX,
            double MinY,
            int GridW,
            int GridH
            )
        {
            int NCells = GridW * GridH;
            bool[] Observed = new bool[NCells];
            foreach ((double E, double N, double Z, bool IsBoundary, double Lat, double Lon) P in UtmPoints)
            {
                int Bx = (int)Math.Floor((P.E - MinX) / FieldConstants.BIN_SIZE_M);
                int By = (int)Math.Floor((P.N - MinY) / FieldConstants.BIN_SIZE_M);
                if (Bx < 0 || Bx >= GridW || By < 0 || By >= GridH)
                    continue;
                Observed[By * GridW + Bx] = true;
            }

            bool AnyObserved = false;
            for (int i = 0; i < NCells; i++)
            {
                if (Observed[i])
                {
                    AnyObserved = true;
                    break;
                }
            }
            if (!AnyObserved)
            {
                bool[] All = new bool[NCells];
                for (int i = 0; i < NCells; i++)
                    All[i] = true;
                return All;
            }

            // Row-wise span fill connects observed bins horizontally within each row.
            bool[] RowFill = new bool[NCells];
            Array.Copy(Observed, RowFill, NCells);
            for (int By = 0; By < GridH; By++)
            {
                int Row = By * GridW;
                int MinBx = int.MaxValue;
                int MaxBx = int.MinValue;
                for (int Bx = 0; Bx < GridW; Bx++)
                {
                    if (!Observed[Row + Bx])
                        continue;
                    if (Bx < MinBx) MinBx = Bx;
                    if (Bx > MaxBx) MaxBx = Bx;
                }
                if (MinBx != int.MaxValue && MaxBx != int.MinValue && MaxBx > MinBx)
                {
                    for (int Bx = MinBx; Bx <= MaxBx; Bx++)
                        RowFill[Row + Bx] = true;
                }
            }

            bool[] ColFill = new bool[NCells];
            Array.Copy(Observed, ColFill, NCells);
            for (int Bx = 0; Bx < GridW; Bx++)
            {
                int MinBy = int.MaxValue;
                int MaxBy = int.MinValue;
                for (int By = 0; By < GridH; By++)
                {
                    if (!Observed[By * GridW + Bx])
                        continue;
                    if (By < MinBy) MinBy = By;
                    if (By > MaxBy) MaxBy = By;
                }
                if (MinBy != int.MaxValue && MaxBy != int.MinValue && MaxBy > MinBy)
                {
                    for (int By = MinBy; By <= MaxBy; By++)
                        ColFill[By * GridW + Bx] = true;
                }
            }

            bool[] Envelope = new bool[NCells];
            for (int i = 0; i < NCells; i++)
                Envelope[i] = RowFill[i] || ColFill[i];

            bool[] Closed = BinaryErode(BinaryDilate(Envelope, GridW, GridH, OBSERVED_MASK_MORPHOLOGY_PASSES), GridW, GridH, OBSERVED_MASK_MORPHOLOGY_PASSES);
            bool[] Filled = FillHoles(Closed, GridW, GridH);
            bool[] Main = LargestComponent(Filled, GridW, GridH);
            for (int i = 0; i < NCells; i++)
                Main[i] = Main[i] || Observed[i];
            return Main;
        }

        /// <summary>
        /// Converts cubic yards to cubic meters for plane-offset root finding.
        /// </summary>
        /// <param name="CubicYards">Volume in cubic yards.</param>
        /// <returns>Equivalent volume in cubic meters.</returns>
        private double CubicYardsToM3
            (
            double CubicYards
            )
        {
            return CubicYards / FieldConstants.CUBIC_YARDS_PER_CUBIC_METER;
        }

        /// <summary>
        /// Derives design-plane easting and northing gradients from main/cross slope percents and heading.
        /// </summary>
        /// <param name="MainSlopePct">Main slope percent along the design heading.</param>
        /// <param name="CrossSlopePct">Cross slope percent perpendicular to the design heading.</param>
        /// <param name="HeadingDeg">Main slope heading in degrees clockwise from north.</param>
        /// <param name="A">Output dz/dE gradient component.</param>
        /// <param name="B">Output dz/dN gradient component.</param>
        private void ComputePlaneSlopes
            (
            double MainSlopePct,
            double CrossSlopePct,
            double HeadingDeg,
            out double A,
            out double B
            )
        {
            // Rotate main/cross slope percents into easting/northing plane gradients.
            double H = HeadingDeg * Math.PI / FieldConstants.DEGREES_PER_RADIAN;
            double SM = MainSlopePct / SLOPE_PERCENT_SCALE;
            double SC = CrossSlopePct / SLOPE_PERCENT_SCALE;
            double SinH = Math.Sin(H);
            double CosH = Math.Cos(H);
            A = -SM * SinH + SC * CosH;
            B = -SM * CosH - SC * SinH;
        }

        /// <summary>
        /// Evaluates cut/fill volumes and the cut-fill balance residual for a candidate plane offset.
        /// </summary>
        private void VolumesF
            (
            IReadOnlyList<BinSample> Bins,
            double A,
            double B,
            double C,
            double RTarget,
            double E0,
            double N0,
            double ImportM3,
            double ExportM3,
            out double VCut,
            out double VFill,
            out double F
            )
        {
            VCut = 0;
            VFill = 0;
            double BinArea = FieldConstants.BIN_SIZE_M * FieldConstants.BIN_SIZE_M;
            // Accumulate cut above the plane and fill below it, then form the balance residual.
            foreach (BinSample Ob in Bins)
            {
                if (Ob.ZInit == FieldConstants.BIN_NO_DATA_SENTINEL)
                    continue;
                double Zf = A * (Ob.EastingC - E0) + B * (Ob.NorthingC - N0) + C;
                double D = Ob.ZInit - Zf;
                if (D > 0)
                    VCut += D * BinArea;
                else
                    VFill += (-D) * BinArea;
            }

            F = (VCut + ImportM3) - RTarget * VFill - ExportM3;
        }

        /// <summary>
        /// Vertical shift <c>c</c> (m) so the design plane equals existing elevation at this bin centroid.
        /// Matches <c>AI Field Design/fielddesign.py</c> <c>_c_for_bin_level</c>.
        /// </summary>
        private static double CForBinLevel
            (
            BinSample Ob,
            double A,
            double B,
            double E0,
            double N0
            )
        {
            double De = Ob.EastingC - E0;
            double Dn = Ob.NorthingC - N0;
            return Ob.ZInit - A * De - B * Dn;
        }

        /// <summary>
        /// True when geometric cut/fill volume ratio is within tolerance of the target ratio.
        /// Matches <c>AI Field Design/fielddesign.py</c> <c>_ratio_satisfied</c>.
        /// </summary>
        private static bool RatioSatisfied
            (
            double VCut,
            double VFill,
            double RTarget,
            double RatioTol = RATIO_TOLERANCE
            )
        {
            if (VFill <= FieldConstants.EPSILON)
                return VCut <= FieldConstants.EPSILON;
            double Ratio = VCut / VFill;
            return Math.Abs(Ratio - RTarget) <= RatioTol * RTarget;
        }

        /// <summary>
        /// Finds <paramref name="Lo"/> and <paramref name="Hi"/> such that the cut/fill balance residual changes sign.
        /// Brackets from per-bin <c>c</c> levels where the tilted plane matches existing elevation, matching
        /// <c>AI Field Design/fielddesign.py</c> <c>_bracket_c_for_ratio</c>.
        /// </summary>
        private void BracketCForRatio
            (
            IReadOnlyList<BinSample> Bins,
            double A,
            double B,
            double RTarget,
            double E0,
            double N0,
            double ImportM3,
            double ExportM3,
            out double Lo,
            out double Hi,
            out double Flo,
            out double Fhi
            )
        {
            List<double> CLevels = new List<double>();
            foreach (BinSample Ob in Bins)
            {
                if (!Ob.IsValid || Ob.ZInit == FieldConstants.BIN_NO_DATA_SENTINEL)
                    continue;
                CLevels.Add(CForBinLevel(Ob, A, B, E0, N0));
            }

            if (CLevels.Count == 0)
                throw new InvalidOperationException("No valid bins available for cut/fill ratio solve.");

            double CMin = CLevels[0];
            double CMax = CLevels[0];
            foreach (double Level in CLevels)
            {
                if (Level < CMin)
                    CMin = Level;
                if (Level > CMax)
                    CMax = Level;
            }

            double Span = Math.Max(CMax - CMin, MIN_C_SPAN_M);
            double Margin = Math.Max(C_BRACKET_MARGIN_M, C_BRACKET_MARGIN_FRAC * Span);
            Lo = CMin - Margin;
            Hi = CMax + Margin;

            double Fval
                (
                double Cc
                )
            {
                VolumesF(Bins, A, B, Cc, RTarget, E0, N0, ImportM3, ExportM3, out _, out _, out double Fn);
                return Fn;
            }

            Flo = Fval(Lo);
            Fhi = Fval(Hi);
            double Expand = Margin;
            for (int i = 0; i < C_BRACKET_EXPAND_MAX; i++)
            {
                if (Flo * Fhi < 0)
                    return;
                if (Flo > 0 && Fhi > 0)
                {
                    Hi += Expand;
                    Fhi = Fval(Hi);
                }
                else if (Flo < 0 && Fhi < 0)
                {
                    Lo -= Expand;
                    Flo = Fval(Lo);
                }
                else
                    break;

                Expand = Math.Max(Expand * C_BRACKET_EXPAND_FACTOR, C_BRACKET_MARGIN_M);
            }

            throw new InvalidOperationException(
                "Could not bracket cut/fill ratio root for c; "
                + $"c_range=[{Lo:F3}, {Hi:F3}] f(lo)={Flo:F3} f(hi)={Fhi:F3} "
                + $"(survey c_levels [{CMin:F3}, {CMax:F3}]); try different slopes or import/export.");
        }

        /// <summary>
        /// Bisects the plane offset <c>c</c> so cut/fill volumes satisfy the target ratio and import/export offsets.
        /// Brackets <c>c</c> from survey elevation range per <c>AI Field Design/fielddesign.py</c> <c>solve_c</c>.
        /// </summary>
        /// <param name="Bins">Per-bin existing elevations and centroids.</param>
        /// <param name="A">Design plane dz/dE gradient.</param>
        /// <param name="B">Design plane dz/dN gradient.</param>
        /// <param name="RTarget">Target cut-to-fill volume ratio.</param>
        /// <param name="E0">Plane origin easting in meters.</param>
        /// <param name="N0">Plane origin northing in meters.</param>
        /// <param name="ImportM3">Import volume added to the cut side of the balance, in cubic meters.</param>
        /// <param name="ExportM3">Export volume subtracted from fill side of the balance, in cubic meters.</param>
        /// <returns>Solved plane offset <c>c</c> in meters.</returns>
        private double SolveC
            (
            IReadOnlyList<BinSample> Bins,
            double A,
            double B,
            double RTarget,
            double E0,
            double N0,
            double ImportM3,
            double ExportM3
            )
        {
            BracketCForRatio(
                Bins, A, B, RTarget, E0, N0, ImportM3, ExportM3,
                out double Lo, out double Hi, out double Flo, out _);

            for (int i = 0; i < SOLVE_C_MAX_ITERATIONS; i++)
            {
                double Mid = FieldConstants.EXTENT_MIDPOINT_FRACTION * (Lo + Hi);
                VolumesF(Bins, A, B, Mid, RTarget, E0, N0, ImportM3, ExportM3, out double VCut, out double VFill, out double Fm);
                if (RatioSatisfied(VCut, VFill, RTarget))
                    return Mid;
                if (Math.Abs(Fm) < SOLVE_C_RESIDUAL_TOLERANCE)
                    return Mid;
                if (Math.Abs(Hi - Lo) < SOLVE_C_INTERVAL_TOLERANCE)
                    return Mid;
                if (Fm * Flo < 0)
                    Hi = Mid;
                else
                {
                    Lo = Mid;
                    Flo = Fm;
                }
            }

            return FieldConstants.EXTENT_MIDPOINT_FRACTION * (Lo + Hi);
        }

        /// <summary>
        /// Matches <c>fieldcreator.py</c> <c>fill_missing_bins</c>: a missing bin is filled only if it has at least two
        /// neighbors in the original sparse data; value is the mean of cardinal neighbors present in the output dictionary.
        /// </summary>
        /// <param name="Values">Sparse bin elevations keyed by bin coordinates.</param>
        /// <param name="GridWidth">Grid width in bins.</param>
        /// <param name="GridHeight">Grid height in bins.</param>
        /// <returns>Dictionary including inferred values for eligible empty bins.</returns>
        private Dictionary<(int Bx, int By), double> FillMissingBins
            (
            Dictionary<(int Bx, int By), double> Values,
            int GridWidth,
            int GridHeight
            )
        {
            HashSet<(int, int)> Original = new HashSet<(int, int)>();
            foreach ((int Bx, int By) in Values.Keys)
                Original.Add((Bx, By));

            Dictionary<(int, int), double> OutDict = new Dictionary<(int, int), double>();
            foreach (KeyValuePair<(int Bx, int By), double> Kv in Values)
                OutDict[Kv.Key] = Kv.Value;

            if (Original.Count == 0)
                return OutDict;

            int MaxBinX = GridWidth - 1;
            int MaxBinY = GridHeight - 1;

            for (int By = 0; By <= MaxBinY; By++)
            {
                for (int Bx = 0; Bx <= MaxBinX; Bx++)
                {
                    if (Original.Contains((Bx, By)))
                        continue;

                    int OrigNeighbors = 0;
                    if (Bx >= 0 && Bx <= MaxBinX && By - 1 >= 0 && By - 1 <= MaxBinY && Original.Contains((Bx, By - 1))) OrigNeighbors++;
                    if (Bx >= 0 && Bx <= MaxBinX && By + 1 >= 0 && By + 1 <= MaxBinY && Original.Contains((Bx, By + 1))) OrigNeighbors++;
                    if (Bx - 1 >= 0 && Bx - 1 <= MaxBinX && By >= 0 && By <= MaxBinY && Original.Contains((Bx - 1, By))) OrigNeighbors++;
                    if (Bx + 1 >= 0 && Bx + 1 <= MaxBinX && By >= 0 && By <= MaxBinY && Original.Contains((Bx + 1, By))) OrigNeighbors++;

                    // Require at least two original neighbors before interpolating a hole.
                    if (OrigNeighbors < MIN_ORIG_NEIGHBORS_FOR_FILL)
                        continue;

                    double SumNv = 0;
                    int CountNv = 0;
                    if (Bx >= 0 && Bx <= MaxBinX && By - 1 >= 0 && By - 1 <= MaxBinY && OutDict.TryGetValue((Bx, By - 1), out double V0))
                    {
                        SumNv += V0;
                        CountNv++;
                    }

                    if (Bx >= 0 && Bx <= MaxBinX && By + 1 >= 0 && By + 1 <= MaxBinY && OutDict.TryGetValue((Bx, By + 1), out double V1))
                    {
                        SumNv += V1;
                        CountNv++;
                    }

                    if (Bx - 1 >= 0 && Bx - 1 <= MaxBinX && By >= 0 && By <= MaxBinY && OutDict.TryGetValue((Bx - 1, By), out double V2))
                    {
                        SumNv += V2;
                        CountNv++;
                    }

                    if (Bx + 1 >= 0 && Bx + 1 <= MaxBinX && By >= 0 && By <= MaxBinY && OutDict.TryGetValue((Bx + 1, By), out double V3))
                    {
                        SumNv += V3;
                        CountNv++;
                    }

                    if (CountNv > 0)
                        OutDict[(Bx, By)] = SumNv / CountNv;
                }
            }

            return OutDict;
        }

        /// <summary>
        /// Buckets survey point indices into uniform spatial cells for k-nearest queries.
        /// </summary>
        /// <param name="Pe">Point eastings in meters.</param>
        /// <param name="Pn">Point northings in meters.</param>
        /// <param name="N">Number of points in the parallel arrays.</param>
        /// <returns>Cell key to list of point indices contained in that cell.</returns>
        private Dictionary<(int Ix, int Iy), List<int>> BuildSpatialHash
            (
            double[] Pe,
            double[] Pn,
            int N
            )
        {
            Dictionary<(int Ix, int Iy), List<int>> D = new Dictionary<(int Ix, int Iy), List<int>>();
            for (int i = 0; i < N; i++)
            {
                int Ix = (int)Math.Floor(Pe[i] / SPATIAL_HASH_CELL_M);
                int Iy = (int)Math.Floor(Pn[i] / SPATIAL_HASH_CELL_M);
                (int Ix, int Iy) Key = (Ix, Iy);
                if (!D.TryGetValue(Key, out List<int>? List))
                {
                    List = new List<int>();
                    D[Key] = List;
                }

                List.Add(i);
            }

            return D;
        }

        /// <summary>
        /// Finds up to <paramref name="KTarget"/> nearest survey points to a bin centroid using expanding ring search.
        /// </summary>
        private void GatherKNearest
            (
            double Cx,
            double Cy,
            double[] Pe,
            double[] Pn,
            Dictionary<(int Ix, int Iy), List<int>> Buck,
            int MaxRing,
            int KTarget,
            double[] Kd2,
            int[] Kidx
            )
        {
            int NPts = Pe.Length;
            // Reset k-nearest slots before each bin centroid query.
            for (int NeighborSlot = 0; NeighborSlot < KTarget; NeighborSlot++)
                Kd2[NeighborSlot] = double.PositiveInfinity;

            int Qx = (int)Math.Floor(Cx / SPATIAL_HASH_CELL_M);
            int Qy = (int)Math.Floor(Cy / SPATIAL_HASH_CELL_M);

            for (int Ring = 0; Ring < MaxRing; Ring++)
            {
                for (int Dx = -Ring; Dx <= Ring; Dx++)
                {
                    for (int Dy = -Ring; Dy <= Ring; Dy++)
                    {
                        if (Ring > 0 && Math.Abs(Dx) < Ring && Math.Abs(Dy) < Ring)
                            continue;
                        if (!Buck.TryGetValue((Qx + Dx, Qy + Dy), out List<int>? List))
                            continue;
                        foreach (int Idx in List)
                            ConsiderCandidate(Cx, Cy, Pe, Pn, Idx, KTarget, Kd2, Kidx);
                    }
                }

                // Stop expanding once the next ring cannot beat the current worst neighbor distance.
                if (CountFilled(Kd2, KTarget) >= KTarget)
                {
                    double WorstSq = WorstFilledD2(Kd2, KTarget);
                    if ((Ring + 1) * SPATIAL_HASH_CELL_M > Math.Sqrt(WorstSq) + SPATIAL_RING_SEARCH_EPSILON_M)
                        break;
                }
            }

            if (CountFilled(Kd2, KTarget) < KTarget)
            {
                for (int i = 0; i < NPts; i++)
                    ConsiderCandidate(Cx, Cy, Pe, Pn, i, KTarget, Kd2, Kidx);
            }
        }

        /// <summary>
        /// Inserts a candidate point into the running k-nearest distance arrays when it qualifies.
        /// </summary>
        private void ConsiderCandidate
            (
            double Cx,
            double Cy,
            double[] Pe,
            double[] Pn,
            int Idx,
            int KTarget,
            double[] Kd2,
            int[] Kidx
            )
        {
            double Tdx = Cx - Pe[Idx];
            double Tdy = Cy - Pn[Idx];
            double D2 = Tdx * Tdx + Tdy * Tdy;
            int Slot = -1;
            // Fill the first empty slot, otherwise evict the farthest neighbour when closer.
            for (int NeighborSlot = 0; NeighborSlot < KTarget; NeighborSlot++)
            {
                if (Kd2[NeighborSlot] >= double.PositiveInfinity)
                {
                    Slot = NeighborSlot;
                    break;
                }
            }

            if (Slot >= 0)
            {
                Kd2[Slot] = D2;
                Kidx[Slot] = Idx;
                return;
            }

            int Worst = 0;
            double WorstD = Kd2[0];
            for (int NeighborSlot = 1; NeighborSlot < KTarget; NeighborSlot++)
            {
                if (Kd2[NeighborSlot] > WorstD)
                {
                    WorstD = Kd2[NeighborSlot];
                    Worst = NeighborSlot;
                }
            }

            if (D2 < WorstD)
            {
                Kd2[Worst] = D2;
                Kidx[Worst] = Idx;
            }
        }

        /// <summary>
        /// Counts how many k-nearest slots currently hold a finite squared distance.
        /// </summary>
        /// <param name="Kd2">Squared distances for each k-nearest slot.</param>
        /// <param name="KTarget">Number of nearest neighbors requested.</param>
        /// <returns>Count of filled neighbor slots.</returns>
        private int CountFilled
            (
            double[] Kd2,
            int KTarget
            )
        {
            int N = 0;
            for (int NeighborSlot = 0; NeighborSlot < KTarget; NeighborSlot++)
            {
                if (Kd2[NeighborSlot] < double.PositiveInfinity)
                    N++;
            }

            return N;
        }

        /// <summary>
        /// Returns the largest finite squared distance among filled k-nearest slots.
        /// </summary>
        /// <param name="Kd2">Squared distances for each k-nearest slot.</param>
        /// <param name="KTarget">Number of nearest neighbors requested.</param>
        /// <returns>Worst (largest) squared distance among filled slots.</returns>
        private double WorstFilledD2
            (
            double[] Kd2,
            int KTarget
            )
        {
            double W = 0;
            for (int NeighborSlot = 0; NeighborSlot < KTarget; NeighborSlot++)
            {
                if (Kd2[NeighborSlot] < double.PositiveInfinity && Kd2[NeighborSlot] > W)
                    W = Kd2[NeighborSlot];
            }

            return W;
        }

        /// <summary>
        /// Solves a 3×3 linear system using partial pivoting without external linear algebra dependencies.
        /// </summary>
        /// <returns>True when a finite solution was found.</returns>
        private bool TrySolve3x3
            (
            double A00,
            double A01,
            double A02,
            double A11,
            double A12,
            double A22,
            double B0,
            double B1,
            double B2,
            out double X0,
            out double X1,
            out double X2
            )
        {
            double[,] M = new double[MLS_NORMAL_EQUATION_DIM, MLS_NORMAL_EQUATION_DIM];
            double[] V = new double[MLS_NORMAL_EQUATION_DIM];
            M[0, 0] = A00; M[0, 1] = A01; M[0, 2] = A02;
            M[1, 0] = A01; M[1, 1] = A11; M[1, 2] = A12;
            M[2, 0] = A02; M[2, 1] = A12; M[2, 2] = A22;
            V[0] = B0; V[1] = B1; V[2] = B2;

            // Gaussian elimination with partial pivoting on the 3×3 normal equations.
            for (int Column = 0; Column < MLS_NORMAL_EQUATION_DIM; Column++)
            {
                int P = Column;
                double Best = Math.Abs(M[Column, Column]);
                for (int Row = Column + 1; Row < MLS_NORMAL_EQUATION_DIM; Row++)
                {
                    double Av = Math.Abs(M[Row, Column]);
                    if (Av > Best)
                    {
                        Best = Av;
                        P = Row;
                    }
                }

                if (Best <= MATRIX_SINGULAR_EPSILON)
                {
                    X0 = X1 = X2 = double.NaN;
                    return false;
                }

                if (P != Column)
                {
                    for (int Col = Column; Col < MLS_NORMAL_EQUATION_DIM; Col++)
                    {
                        double Tmp = M[Column, Col];
                        M[Column, Col] = M[P, Col];
                        M[P, Col] = Tmp;
                    }

                    double Tv = V[Column];
                    V[Column] = V[P];
                    V[P] = Tv;
                }

                double Piv = M[Column, Column];
                for (int Col = Column; Col < MLS_NORMAL_EQUATION_DIM; Col++)
                    M[Column, Col] /= Piv;
                V[Column] /= Piv;

                for (int Row = 0; Row < MLS_NORMAL_EQUATION_DIM; Row++)
                {
                    if (Row == Column)
                        continue;
                    double F = M[Row, Column];
                    if (Math.Abs(F) <= 0)
                        continue;
                    for (int Col = Column; Col < MLS_NORMAL_EQUATION_DIM; Col++)
                        M[Row, Col] -= F * M[Column, Col];
                    V[Row] -= F * V[Column];
                }
            }

            X0 = V[0];
            X1 = V[1];
            X2 = V[2];
            return double.IsFinite(X0) && double.IsFinite(X1) && double.IsFinite(X2);
        }

        /// <summary>
        /// Applies a separable box blur along one grid axis with edge clamping.
        /// </summary>
        /// <param name="Arr">Input elevation grid in row-major order.</param>
        /// <param name="Width">Grid width in cells.</param>
        /// <param name="Height">Grid height in cells.</param>
        /// <param name="Radius">Blur radius in cells.</param>
        /// <param name="AxisX">True to blur along X; false to blur along Y.</param>
        /// <returns>Blurred grid copy.</returns>
        private double[] BoxBlur1dAxis
            (
            double[] Arr,
            int Width,
            int Height,
            int Radius,
            bool AxisX
            )
        {
            if (Radius <= 0)
            {
                double[] Clone = new double[Arr.Length];
                Array.Copy(Arr, Clone, Arr.Length);
                return Clone;
            }

            int Window = BOX_BLUR_DIAMETER_FACTOR * Radius + 1;
            double[] OutArr = new double[Arr.Length];
            // Separable box filter with edge clamping along one axis per call.
            if (AxisX)
            {
                for (int Y = 0; Y < Height; Y++)
                {
                    int Row = Y * Width;
                    for (int X = 0; X < Width; X++)
                    {
                        double Sum = 0;
                        for (int k = -Radius; k <= Radius; k++)
                        {
                            int Xx = X + k;
                            if (Xx < 0) Xx = 0;
                            else if (Xx >= Width) Xx = Width - 1;
                            Sum += Arr[Row + Xx];
                        }

                        OutArr[Row + X] = Sum / Window;
                    }
                }
            }
            else
            {
                for (int Y = 0; Y < Height; Y++)
                {
                    for (int X = 0; X < Width; X++)
                    {
                        double Sum = 0;
                        for (int k = -Radius; k <= Radius; k++)
                        {
                            int Yy = Y + k;
                            if (Yy < 0) Yy = 0;
                            else if (Yy >= Height) Yy = Height - 1;
                            Sum += Arr[Yy * Width + X];
                        }

                        OutArr[Y * Width + X] = Sum / Window;
                    }
                }
            }

            return OutArr;
        }

        /// <summary>
        /// Applies separable X/Y box blurs for the requested number of passes.
        /// </summary>
        /// <param name="Arr">Input elevation grid in row-major order.</param>
        /// <param name="Width">Grid width in cells.</param>
        /// <param name="Height">Grid height in cells.</param>
        /// <param name="Radius">Blur radius in cells.</param>
        /// <param name="Passes">Number of blur iterations.</param>
        /// <returns>Smoothed grid copy.</returns>
        private double[] BoxBlur2d
            (
            double[] Arr,
            int Width,
            int Height,
            int Radius,
            int Passes
            )
        {
            double[] OutArr = new double[Arr.Length];
            Array.Copy(Arr, OutArr, Arr.Length);
            if (Radius <= 0 || Passes <= 0)
                return OutArr;

            // Alternate horizontal and vertical passes for a cheap 2D box blur.
            for (int P = 0; P < Passes; P++)
            {
                OutArr = BoxBlur1dAxis(OutArr, Width, Height, Radius, AxisX: true);
                OutArr = BoxBlur1dAxis(OutArr, Width, Height, Radius, AxisX: false);
            }

            return OutArr;
        }

        /// <summary>
        /// Spreads observed-bin residuals across the grid using normalized box-blur weights.
        /// </summary>
        /// <param name="ResidualAtObs">Per-cell residual values at observed bins.</param>
        /// <param name="ObservedMask">True where a residual sample exists.</param>
        /// <param name="Width">Grid width in cells.</param>
        /// <param name="Height">Grid height in cells.</param>
        /// <param name="RadiusBins">Blur radius in bins.</param>
        /// <param name="Passes">Blur pass count.</param>
        /// <returns>Normalized correction field aligned with the input grid.</returns>
        private double[] NormalizedResidualWarp
            (
            double[] ResidualAtObs,
            bool[] ObservedMask,
            int Width,
            int Height,
            int RadiusBins,
            int Passes
            )
        {
            double[] MaskedResidual = new double[ResidualAtObs.Length];
            double[] MaskAsDouble = new double[ResidualAtObs.Length];
            // Zero out non-observed cells so blur spreads only from known residuals.
            for (int i = 0; i < ResidualAtObs.Length; i++)
            {
                if (ObservedMask[i])
                {
                    MaskedResidual[i] = ResidualAtObs[i];
                    MaskAsDouble[i] = OBSERVED_MASK_TRUE;
                }
            }

            double[] Num = BoxBlur2d(MaskedResidual, Width, Height, RadiusBins, Passes);
            double[] Den = BoxBlur2d(MaskAsDouble, Width, Height, RadiusBins, Passes);
            double[] Corr = new double[ResidualAtObs.Length];
            for (int i = 0; i < Corr.Length; i++)
                Corr[i] = Num[i] / Math.Max(Den[i], FieldConstants.EPSILON_FINE);
            return Corr;
        }

        /// <summary>
        /// Builds the existing-elevation surface over the full bin grid using MLS interpolation and residual warping.
        /// </summary>
        /// <param name="UtmPoints">Survey samples in UTM coordinates.</param>
        /// <param name="MinX">Grid origin easting in meters.</param>
        /// <param name="MinY">Grid origin northing in meters.</param>
        /// <param name="GridW">Grid width in bins.</param>
        /// <param name="GridH">Grid height in bins.</param>
        /// <param name="ValidMask">True for bins inside the field envelope.</param>
        /// <returns>Per-bin existing elevations in row-major order.</returns>
        private double[] BuildFielddesignSurface
            (
            List<(double E, double N, double Z, bool IsBoundary, double Lat, double Lon)> UtmPoints,
            double MinX,
            double MinY,
            int GridW,
            int GridH,
            bool[] ValidMask
            )
        {
            ReportProgress($"Interpolating existing elevations (MLS, {GridW}×{GridH} bins)…");
            int NPts = UtmPoints.Count;
            double[] Pe = new double[NPts];
            double[] Pn = new double[NPts];
            double[] Pz = new double[NPts];
            for (int i = 0; i < NPts; i++)
            {
                Pe[i] = UtmPoints[i].E;
                Pn[i] = UtmPoints[i].N;
                Pz[i] = UtmPoints[i].Z;
            }

            Dictionary<(int Bx, int By), (double SumZ, int Count)> Grouped = new Dictionary<(int Bx, int By), (double SumZ, int Count)>();
            foreach ((double E, double N, double Z, bool IsBoundary, double Lat, double Lon) T in UtmPoints)
            {
                int Bx = (int)Math.Floor((T.E - MinX) / FieldConstants.BIN_SIZE_M);
                int By = (int)Math.Floor((T.N - MinY) / FieldConstants.BIN_SIZE_M);
                if (Bx < 0 || Bx >= GridW || By < 0 || By >= GridH)
                    continue;
                if (!ValidMask[By * GridW + Bx])
                    continue;
                (int Bx, int By) Key = (Bx, By);
                if (Grouped.TryGetValue(Key, out (double SumZ, int Count) Agg))
                    Grouped[Key] = (Agg.SumZ + T.Z, Agg.Count + 1);
                else
                    Grouped[Key] = (T.Z, 1);
            }

            int NCells = GridW * GridH;
            double[] ZMls = new double[NCells];
            bool[] ObservedMask = new bool[NCells];
            double[] ObservedValues = new double[NCells];

            Dictionary<(int Ix, int Iy), List<int>> Spatial = BuildSpatialHash(Pe, Pn, NPts);
            int MaxRing = (int)Math.Ceiling(Math.Max(
                    Math.Max(MIN_GRID_EXTENT_METRES, GridW * FieldConstants.BIN_SIZE_M),
                    Math.Max(MIN_GRID_EXTENT_METRES, GridH * FieldConstants.BIN_SIZE_M)
                ) / SPATIAL_HASH_CELL_M) + SPATIAL_HASH_RING_PADDING;
            int KUse = Math.Min(EMPTY_BIN_MLS_K, NPts);
            double[] Kd2 = new double[KUse];
            int[] Kidx = new int[KUse];

            int ReportStep = Math.Max(1, GridH / MLS_PROGRESS_ROW_DIVISOR);
            for (int By = 0; By < GridH; By++)
            {
                if (By % ReportStep == 0)
                    ReportProgress($"MLS interpolation… {By + 1} / {GridH} rows");
                for (int Bx = 0; Bx < GridW; Bx++)
                {
                    int Gi = By * GridW + Bx;
                    if (!ValidMask[Gi])
                    {
                        ZMls[Gi] = FieldConstants.BIN_NO_DATA_SENTINEL;
                        continue;
                    }

                    double Cx = MinX + (Bx + FieldConstants.BIN_CENTER_OFFSET) * FieldConstants.BIN_SIZE_M;
                    double Cy = MinY + (By + FieldConstants.BIN_CENTER_OFFSET) * FieldConstants.BIN_SIZE_M;
                    GatherKNearest(Cx, Cy, Pe, Pn, Spatial, MaxRing, KUse, Kd2, Kidx);

                    double R2k = 0;
                    for (int NeighborSlot = 0; NeighborSlot < KUse; NeighborSlot++)
                    {
                        if (Kd2[NeighborSlot] < double.PositiveInfinity && Kd2[NeighborSlot] > R2k)
                            R2k = Kd2[NeighborSlot];
                    }

                    double Smooth2 = EMPTY_BIN_MLS_SMOOTH_FRAC * EMPTY_BIN_MLS_SMOOTH_FRAC * R2k;
                    double A00 = 0, A01 = 0, A02 = 0, A11 = 0, A12 = 0, A22 = 0;
                    double B0 = 0, B1 = 0, B2 = 0;
                    double WSum = 0, WzSum = 0;
                    for (int NeighborSlot = 0; NeighborSlot < KUse; NeighborSlot++)
                    {
                        if (Kd2[NeighborSlot] >= double.PositiveInfinity)
                            continue;
                        int Idx = Kidx[NeighborSlot];
                        double Dx = Pe[Idx] - Cx;
                        double Dy = Pn[Idx] - Cy;
                        double Z = Pz[Idx];
                        double W = MLS_WEIGHT_NUMERATOR / (Kd2[NeighborSlot] + Smooth2 + EMPTY_BIN_INTERP_EPS_SQ);
                        A00 += W * Dx * Dx;
                        A01 += W * Dx * Dy;
                        A02 += W * Dx;
                        A11 += W * Dy * Dy;
                        A12 += W * Dy;
                        A22 += W;
                        B0 += W * Dx * Z;
                        B1 += W * Dy * Z;
                        B2 += W * Z;
                        WSum += W;
                        WzSum += W * Z;
                    }

                    double Trace = (A00 + A11 + A22) / MLS_NORMAL_EQUATION_DIM;
                    double Lam = Math.Max(EMPTY_BIN_INTERP_EPS_SQ, EMPTY_BIN_MLS_REG_SCALE * Trace);
                    bool Solved = TrySolve3x3(
                        A00 + Lam, A01, A02,
                        A11 + Lam, A12,
                        A22 + Lam,
                        B0, B1, B2,
                        out _, out _, out double C0);
                    if (!Solved || !double.IsFinite(C0))
                        C0 = WSum > 0 ? WzSum / WSum : Pz[0];

                    ZMls[Gi] = C0;

                    if (Grouped.TryGetValue((Bx, By), out (double SumZ, int Count) G))
                    {
                        ObservedMask[Gi] = true;
                        ObservedValues[Gi] = G.SumZ / G.Count;
                    }
                }
            }

            ReportProgress("Smoothing baseline and warping toward survey bins…");
            double[] ZBase = new double[NCells];
            bool HasValid = false;
            double SumValid = 0;
            int CntValid = 0;
            for (int i = 0; i < NCells; i++)
            {
                if (!ValidMask[i])
                    continue;
                HasValid = true;
                SumValid += ZMls[i];
                CntValid++;
            }
            double FillV = HasValid && CntValid > 0 ? SumValid / CntValid : DEFAULT_FILL_ELEVATION;
            for (int i = 0; i < NCells; i++)
                ZBase[i] = ValidMask[i] ? ZMls[i] : FillV;

            double[] ZSmooth = BoxBlur2d(ZBase, GridW, GridH, WARP_BASE_RADIUS_BINS, WARP_BASE_PASSES);
            double[] ResidualObs = new double[NCells];
            for (int i = 0; i < NCells; i++)
            {
                if (ObservedMask[i])
                    ResidualObs[i] = ObservedValues[i] - ZSmooth[i];
            }

            double[] Corr = NormalizedResidualWarp(ResidualObs, ObservedMask, GridW, GridH, WARP_RADIUS_BINS, WARP_PASSES);
            double[] ZFinal = new double[NCells];
            for (int i = 0; i < NCells; i++)
                ZFinal[i] = ZSmooth[i] + WARP_ALPHA * Corr[i];

            double[] Err = new double[NCells];
            double[] Excess = new double[NCells];
            bool[] Need = new bool[NCells];
            for (int It = 0; It < WARP_TOL_ITERS; It++)
            {
                bool AnyNeed = false;
                for (int i = 0; i < NCells; i++)
                {
                    Err[i] = 0;
                    Excess[i] = 0;
                    Need[i] = false;
                    if (!ObservedMask[i])
                        continue;
                    Err[i] = ObservedValues[i] - ZFinal[i];
                    double AbsErr = Math.Abs(Err[i]);
                    if (AbsErr > WARP_TOL_M)
                    {
                        Need[i] = true;
                        AnyNeed = true;
                        Excess[i] = Math.Sign(Err[i]) * (AbsErr - WARP_TOL_M);
                    }
                }

                if (!AnyNeed)
                    break;
                double[] CorrEx = NormalizedResidualWarp(Excess, Need, GridW, GridH, WARP_TOL_RADIUS_BINS, WARP_TOL_PASSES);
                for (int i = 0; i < NCells; i++)
                    ZFinal[i] += WARP_TOL_ALPHA * CorrEx[i];
            }

            if (WARP_POST_RADIUS_BINS > 0 && WARP_POST_PASSES > 0)
            {
                ZFinal = BoxBlur2d(ZFinal, GridW, GridH, WARP_POST_RADIUS_BINS, WARP_POST_PASSES);
                for (int It = 0; It < WARP_POST_TOL_ITERS; It++)
                {
                    bool AnyNeed = false;
                    for (int i = 0; i < NCells; i++)
                    {
                        Err[i] = 0;
                        Excess[i] = 0;
                        Need[i] = false;
                        if (!ObservedMask[i])
                            continue;
                        Err[i] = ObservedValues[i] - ZFinal[i];
                        double AbsErr = Math.Abs(Err[i]);
                        if (AbsErr > WARP_TOL_M)
                        {
                            Need[i] = true;
                            AnyNeed = true;
                            Excess[i] = Math.Sign(Err[i]) * (AbsErr - WARP_TOL_M);
                        }
                    }

                    if (!AnyNeed)
                        break;
                    double[] CorrEx = NormalizedResidualWarp(Excess, Need, GridW, GridH, WARP_TOL_RADIUS_BINS, WARP_TOL_PASSES);
                    for (int i = 0; i < NCells; i++)
                        ZFinal[i] += WARP_TOL_ALPHA * CorrEx[i];
                }
            }

            // Clamp observed bins to tolerance band so warping never overshoots survey elevations.
            for (int i = 0; i < NCells; i++)
            {
                if (!ObservedMask[i])
                    continue;
                double Eo = ObservedValues[i] - ZFinal[i];
                if (Eo < -WARP_TOL_M) Eo = -WARP_TOL_M;
                else if (Eo > WARP_TOL_M) Eo = WARP_TOL_M;
                ZFinal[i] = ObservedValues[i] - Eo;
            }

            for (int i = 0; i < NCells; i++)
            {
                if (!ValidMask[i])
                    ZFinal[i] = FieldConstants.BIN_NO_DATA_SENTINEL;
            }

            ReportProgress("Existing surface ready.");
            return ZFinal;
        }

        /// <summary>
        /// Builds the full bin grid, interpolates existing elevations, solves the design plane, and aggregates volumes.
        /// </summary>
        private void BuildGrid
            (
            SurveyModel Survey,
            bool UseExistingTopology,
            int HeadingDeg,
            double MainSlopePct,
            double CrossSlopePct,
            double CutFillRatio,
            double ImportM3,
            double ExportM3,
            out int GridW,
            out int GridH,
            out double MinX,
            out double MinY,
            out double MaxX,
            out double MaxY,
            out int UtmZone,
            out bool UtmNorth,
            out double MeanLat,
            out double MeanLon,
            out double TotalCutCy,
            out double TotalFillCy,
            out double PlaneA,
            out double PlaneB,
            out double PlaneC,
            out int ValidBinCount,
            out List<GridBinData> BinRows
            )
        {
            PlaneA = 0;
            PlaneB = 0;
            PlaneC = 0;

            List<(double E, double N, double Z, bool IsBoundary, double Lat, double Lon)> UtmPoints = new List<(double E, double N, double Z, bool IsBoundary, double Lat, double Lon)>();
            double SumLat = 0, SumLon = 0;
            int NTopo = 0;

            foreach (TopologyPoint P in Survey.InteriorPoints)
            {
                UTM.UTMCoordinate C = UTM.FromLatLon(P.Latitude, P.Longitude);
                UtmPoints.Add((C.Easting, C.Northing, P.ExistingElevation, false, P.Latitude, P.Longitude));
                SumLat += P.Latitude;
                SumLon += P.Longitude;
                NTopo++;
            }

            foreach (TopologyPoint P in Survey.BoundaryPoints)
            {
                UTM.UTMCoordinate C = UTM.FromLatLon(P.Latitude, P.Longitude);
                UtmPoints.Add((C.Easting, C.Northing, P.ExistingElevation, true, P.Latitude, P.Longitude));
                SumLat += P.Latitude;
                SumLon += P.Longitude;
                NTopo++;
            }

            MeanLat = SumLat / NTopo;
            MeanLon = SumLon / NTopo;

            double MinLatTopo = double.MaxValue;
            double MinLonTopo = double.MaxValue;
            foreach (TopologyPoint P in Survey.InteriorPoints)
            {
                if (P.Latitude < MinLatTopo) MinLatTopo = P.Latitude;
                if (P.Longitude < MinLonTopo) MinLonTopo = P.Longitude;
            }

            foreach (TopologyPoint P in Survey.BoundaryPoints)
            {
                if (P.Latitude < MinLatTopo) MinLatTopo = P.Latitude;
                if (P.Longitude < MinLonTopo) MinLonTopo = P.Longitude;
            }

            UTM.UTMCoordinate Sw = UTM.FromLatLon(MinLatTopo, MinLonTopo);
            UtmZone = Sw.Zone;
            UtmNorth = Sw.IsNorthernHemisphere;

            MinX = double.MaxValue;
            MinY = double.MaxValue;
            MaxX = double.MinValue;
            MaxY = double.MinValue;
            List<(double X, double Y)> BoundaryXY = new List<(double X, double Y)>();

            foreach ((double E, double N, double Z, bool IsBoundary, double Lat, double Lon) T in UtmPoints)
            {
                UTM.UTMCoordinate C = UTM.FromLatLon(T.Lat, T.Lon);
                if (C.Zone != UtmZone || C.IsNorthernHemisphere != UtmNorth)
                    throw new InvalidDataException("Field crosses UTM zone boundaries (not supported).");

                if (T.E < MinX) MinX = T.E;
                if (T.E > MaxX) MaxX = T.E;
                if (T.N < MinY) MinY = T.N;
                if (T.N > MaxY) MaxY = T.N;
                if (T.IsBoundary)
                    BoundaryXY.Add((T.E, T.N));
            }

            // Match fielddesign.py safety padding so edge bins are not clipped.
            MinX -= FieldConstants.BIN_SIZE_M;
            MinY -= FieldConstants.BIN_SIZE_M;
            MaxX += FieldConstants.BIN_SIZE_M;
            MaxY += FieldConstants.BIN_SIZE_M;

            GridW = (int)Math.Ceiling((MaxX - MinX) / FieldConstants.BIN_SIZE_M) + 1;
            GridH = (int)Math.Ceiling((MaxY - MinY) / FieldConstants.BIN_SIZE_M) + 1;
            if (GridW <= 0 || GridH <= 0)
                throw new InvalidDataException("Computed non-positive grid dimensions from survey extent.");
            MaxX = MinX + GridW * FieldConstants.BIN_SIZE_M;
            MaxY = MinY + GridH * FieldConstants.BIN_SIZE_M;

            double E0 = MinX;
            double N0 = MinY;
            bool[] ValidMask =
                BoundaryXY.Count >= MIN_POLYGON_VERTICES
                ? BuildValidMaskFromBoundary(BoundaryXY, MinX, MinY, GridW, GridH)
                : BuildValidMaskFromObservedPoints(UtmPoints, MinX, MinY, GridW, GridH);

            double[] FilledZ = BuildFielddesignSurface(UtmPoints, MinX, MinY, GridW, GridH, ValidMask);

            ReportProgress("Building per-bin samples and solving design plane…");
            List<BinSample> Samples = new List<BinSample>(GridW * GridH);
            for (int By = 0; By < GridH; By++)
            {
                for (int Bx = 0; Bx < GridW; Bx++)
                {
                    double Cx = MinX + (Bx + FieldConstants.BIN_CENTER_OFFSET) * FieldConstants.BIN_SIZE_M;
                    double Cy = MinY + (By + FieldConstants.BIN_CENTER_OFFSET) * FieldConstants.BIN_SIZE_M;
                    UTM.ToLatLon(UtmZone, UtmNorth, Cx, Cy, out double CLat, out double CLon);

                    int Gi = By * GridW + Bx;
                    bool IsValid = ValidMask[Gi];
                    double ZInit = IsValid ? FilledZ[Gi] : FieldConstants.BIN_NO_DATA_SENTINEL;

                    Samples.Add(new BinSample(Bx, By, ZInit, Cx, Cy, CLat, CLon, IsValid));
                }
            }

            if (UseExistingTopology)
            {
                ReportProgress("Copying existing surface to proposed…");
                // intent: skip plane solve and mirror existing elevations into proposed bins
                PlaneA = 0;
                PlaneB = 0;
                PlaneC = 0;
                TotalCutCy = 0;
                TotalFillCy = 0;
                foreach (BinSample S in Samples)
                {
                    if (!S.IsValid || S.ZInit == FieldConstants.BIN_NO_DATA_SENTINEL)
                    {
                        S.TargetZ = FieldConstants.BIN_NO_DATA_SENTINEL;
                        continue;
                    }

                    S.TargetZ = S.ZInit;
                }
            }
            else
            {
                ComputePlaneSlopes(MainSlopePct, CrossSlopePct, HeadingDeg, out double A, out double B);
                PlaneA = A;
                PlaneB = B;
                ReportProgress("Solving cut/fill ratio (plane offset)…");
                double CStar = SolveC(Samples, A, B, CutFillRatio, E0, N0, ImportM3, ExportM3);

                ReportProgress("Computing per-bin targets and volumes…");
                double BinArea = FieldConstants.BIN_SIZE_M * FieldConstants.BIN_SIZE_M;
                double VCutM3 = 0;
                double VFillM3 = 0;
                foreach (BinSample S in Samples)
                {
                    if (!S.IsValid || S.ZInit == FieldConstants.BIN_NO_DATA_SENTINEL)
                    {
                        S.TargetZ = FieldConstants.BIN_NO_DATA_SENTINEL;
                        continue;
                    }
                    double Zf = A * (S.EastingC - E0) + B * (S.NorthingC - N0) + CStar;
                    S.TargetZ = Zf;
                    double D = S.ZInit - Zf;
                    if (D > 0)
                        VCutM3 += D * BinArea;
                    else if (D < 0)
                        VFillM3 += (-D) * BinArea;
                }

                TotalCutCy = VCutM3 * FieldConstants.CUBIC_YARDS_PER_CUBIC_METER;
                TotalFillCy = VFillM3 * FieldConstants.CUBIC_YARDS_PER_CUBIC_METER;
                PlaneC = CStar;
            }

            BinRows = new List<GridBinData>(Samples.Count);
            foreach (BinSample S in Samples)
            {
                BinRows.Add(new GridBinData
                {
                    X = S.Bx,
                    Y = S.By,
                    ExistingM = S.ZInit,
                    ProposedM = S.TargetZ,
                    CentroidLat = S.CentroidLat,
                    CentroidLon = S.CentroidLon,
                    Valid = S.IsValid && S.ZInit != FieldConstants.BIN_NO_DATA_SENTINEL
                });
            }

            int ValidBins = 0;
            foreach (bool V in ValidMask)
                if (V) ValidBins++;
            ValidBinCount = ValidBins;
        }

        /// <summary>Mutable per-bin working record used while solving the design plane.</summary>
        private sealed class BinSample
        {
            /// <summary>Bin column index.</summary>
            public int Bx;
            /// <summary>Bin row index.</summary>
            public int By;
            /// <summary>Interpolated existing elevation at bin centroid, in meters.</summary>
            public double ZInit;
            /// <summary>Bin centroid easting in meters.</summary>
            public double EastingC;
            /// <summary>Bin centroid northing in meters.</summary>
            public double NorthingC;
            /// <summary>Bin centroid latitude in decimal degrees.</summary>
            public double CentroidLat;
            /// <summary>Bin centroid longitude in decimal degrees.</summary>
            public double CentroidLon;
            /// <summary>Solved proposed elevation on the design plane, in meters.</summary>
            public double TargetZ;
            /// <summary>True when the bin lies inside the valid field mask.</summary>
            public bool IsValid;

            /// <summary>
            /// Initializes a bin sample with grid indices, elevations, centroid coordinates, and validity.
            /// </summary>
            /// <param name="Bx">Bin column index.</param>
            /// <param name="By">Bin row index.</param>
            /// <param name="ZInit">Existing elevation at centroid in meters.</param>
            /// <param name="EastingC">Centroid easting in meters.</param>
            /// <param name="NorthingC">Centroid northing in meters.</param>
            /// <param name="CentroidLat">Centroid latitude in decimal degrees.</param>
            /// <param name="CentroidLon">Centroid longitude in decimal degrees.</param>
            /// <param name="IsValid">True when the bin is inside the valid field envelope.</param>
            public BinSample
                (
                int Bx,
                int By,
                double ZInit,
                double EastingC,
                double NorthingC,
                double CentroidLat,
                double CentroidLon,
                bool IsValid
                )
            {
                this.Bx = Bx;
                this.By = By;
                this.ZInit = ZInit;
                this.EastingC = EastingC;
                this.NorthingC = NorthingC;
                this.CentroidLat = CentroidLat;
                this.CentroidLon = CentroidLon;
                this.IsValid = IsValid;
            }
        }
    }
}
