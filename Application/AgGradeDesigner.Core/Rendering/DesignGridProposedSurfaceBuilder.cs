using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using System.Threading.Tasks;

namespace AgGradeDesigner.Core.Rendering
{
    /// <summary>
    /// Builds a continuous proposed elevation field on the design grid using the same
    /// baseline blur and normalized residual warp used for survey existing surfaces (§2.4.14).
    /// </summary>
    internal static class DesignGridProposedSurfaceBuilder
    {
        /// <summary>Box blur diameter factor matching <see cref="Design.FieldCreator"/>.</summary>
        private const int BOX_BLUR_DIAMETER_FACTOR = 2;

        /// <summary>Mask weight used when normalizing residual warp denominators.</summary>
        private const double OBSERVED_MASK_TRUE = 1.0;

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

        /// <summary>Target maximum absolute elevation error at observed bins after warping, in metres.</summary>
        private const double WARP_TOL_M = 0.009;

        /// <summary>Iteration cap for tolerance-driven residual warp refinement.</summary>
        private const int WARP_TOL_ITERS = 8;

        /// <summary>Normalized residual warp radius in bins during tolerance refinement.</summary>
        private const int WARP_TOL_RADIUS_BINS = 8;

        /// <summary>Normalized residual warp pass count during tolerance refinement.</summary>
        private const int WARP_TOL_PASSES = 3;

        /// <summary>Blend factor for tolerance refinement warp corrections.</summary>
        private const double WARP_TOL_ALPHA = 1.0;

        /// <summary>Post-warp box blur radius in bins before final tolerance pass.</summary>
        private const int WARP_POST_RADIUS_BINS = 4;

        /// <summary>Post-warp box blur pass count before final tolerance pass.</summary>
        private const int WARP_POST_PASSES = 3;

        /// <summary>Iteration cap for tolerance refinement after post-warp smoothing.</summary>
        private const int WARP_POST_TOL_ITERS = 6;

        /// <summary>Default fill elevation when the grid has no valid bins.</summary>
        private const double DEFAULT_FILL_ELEVATION_M = 0.0;

        /// <summary>
        /// Builds a continuous proposed elevation raster aligned with the design grid.
        /// </summary>
        /// <param name="Grid">Design grid with per-bin proposed elevations.</param>
        /// <returns>Row-major proposed elevations and a parallel valid-cell mask.</returns>
        public static (double[] Field, bool[] ValidMask) BuildContinuousProposedField
            (
            DesignGrid Grid
            )
        {
            int Width = Grid.Width;
            int Height = Grid.Height;
            int CellCount = Width * Height;
            double[] ObservedValues = new double[CellCount];
            bool[] ObservedMask = new bool[CellCount];
            bool[] ValidMask = new bool[CellCount];

            int MinX = Width;
            int MinY = Height;
            int MaxX = -1;
            int MaxY = -1;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                int Idx = Bin.Y * Width + Bin.X;
                if (Idx < 0 || Idx >= CellCount)
                    continue;

                ObservedMask[Idx] = true;
                ValidMask[Idx] = true;
                ObservedValues[Idx] = Bin.ProposedM;
                MinX = Math.Min(MinX, Bin.X);
                MinY = Math.Min(MinY, Bin.Y);
                MaxX = Math.Max(MaxX, Bin.X);
                MaxY = Math.Max(MaxY, Bin.Y);
            }

            if (!ObservedMask.Any(Mask => Mask))
                return (ObservedValues, ValidMask);

            // intent: warp only the tight bin bounding box so empty envelope padding does not dominate runtime
            int Pad = WARP_RADIUS_BINS + WARP_POST_RADIUS_BINS + 2;
            MinX = Math.Max(0, MinX - Pad);
            MinY = Math.Max(0, MinY - Pad);
            MaxX = Math.Min(Width - 1, MaxX + Pad);
            MaxY = Math.Min(Height - 1, MaxY + Pad);
            int SubWidth = MaxX - MinX + 1;
            int SubHeight = MaxY - MinY + 1;
            int SubCount = SubWidth * SubHeight;

            double[] SubObservedValues = new double[SubCount];
            bool[] SubObservedMask = new bool[SubCount];
            for (int SubY = 0; SubY < SubHeight; SubY++)
            {
                for (int SubX = 0; SubX < SubWidth; SubX++)
                {
                    int SourceIdx = (MinY + SubY) * Width + (MinX + SubX);
                    int TargetIdx = SubY * SubWidth + SubX;
                    SubObservedMask[TargetIdx] = ObservedMask[SourceIdx];
                    SubObservedValues[TargetIdx] = ObservedValues[SourceIdx];
                }
            }

            double[] SubFinal = BuildWarpSmoothedField(SubObservedValues, SubObservedMask, SubWidth, SubHeight);
            SubFinal = BoxBlur2d(SubFinal, SubWidth, SubHeight, VIEWER_EXTRA_BLUR_RADIUS_BINS, VIEWER_EXTRA_BLUR_PASSES);
            ClampObservedCells(SubFinal, SubObservedValues, SubObservedMask);

            double[] Field = new double[CellCount];
            Array.Copy(ObservedValues, Field, CellCount);
            for (int SubY = 0; SubY < SubHeight; SubY++)
            {
                for (int SubX = 0; SubX < SubWidth; SubX++)
                {
                    int TargetIdx = (MinY + SubY) * Width + (MinX + SubX);
                    Field[TargetIdx] = SubFinal[SubY * SubWidth + SubX];
                    // intent: the warp field is valid across the whole padded subgrid, not only at bin centres
                    ValidMask[TargetIdx] = true;
                }
            }

            return (Field, ValidMask);
        }

        /// <summary>Extra display-only blur applied after warp to reduce contour chatter on shallow slopes.</summary>
        private const int VIEWER_EXTRA_BLUR_RADIUS_BINS = 2;

        /// <summary>Pass count for the post-warp display blur.</summary>
        private const int VIEWER_EXTRA_BLUR_PASSES = 1;

        /// <summary>Maximum deviation from the design plane treated as a uniform plane design.</summary>
        private const double PLANE_BIN_TOLERANCE_M = 0.001;

        /// <summary>
        /// Evaluates the design plane proposed elevation at a UTM location.
        /// </summary>
        /// <param name="Grid">Design grid with plane coefficients.</param>
        /// <param name="EastingM">Sample easting in metres.</param>
        /// <param name="NorthingM">Sample northing in metres.</param>
        /// <returns>Proposed elevation in metres from the solved plane.</returns>
        public static double EvaluateProposedPlaneAtUtm
            (
            DesignGrid Grid,
            double EastingM,
            double NorthingM
            )
        {
            double DeltaEastingM = EastingM - Grid.OriginEastingM;
            double DeltaNorthingM = NorthingM - Grid.OriginNorthingM;
            return (Grid.PlaneA * DeltaEastingM) + (Grid.PlaneB * DeltaNorthingM) + Grid.PlaneC;
        }

        /// <summary>
        /// True when every valid bin proposed elevation lies on the solved design plane.
        /// </summary>
        /// <param name="Grid">Design grid with plane coefficients and bin elevations.</param>
        /// <returns>True for uniform plane designs such as constant-slope fields.</returns>
        public static bool IsUniformPlaneDesign
            (
            DesignGrid Grid
            )
        {
            bool HasValidBin = false;
            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                HasValidBin = true;
                UTM.UTMCoordinate Utm = UTM.FromLatLon(Bin.CentroidLat, Bin.CentroidLon);
                double PlaneM = EvaluateProposedPlaneAtUtm(Grid, Utm.Easting, Utm.Northing);
                if (Math.Abs(Bin.ProposedM - PlaneM) > PLANE_BIN_TOLERANCE_M)
                    return false;
            }

            return HasValidBin;
        }

        /// <summary>
        /// Samples the continuous proposed field with partial bilinear and nearest fallbacks.
        /// </summary>
        /// <param name="Field">Row-major warp-smoothed elevations.</param>
        /// <param name="SampleMask">True where a sample may be taken.</param>
        /// <param name="Width">Grid width in bins.</param>
        /// <param name="Height">Grid height in bins.</param>
        /// <param name="Ex">Fractional bin index in the easting direction.</param>
        /// <param name="Ey">Fractional bin index in the northing direction.</param>
        /// <param name="ValueM">Output elevation in metres.</param>
        /// <returns>True when any fallback produced a sample.</returns>
        public static bool TrySampleContinuousField
            (
            double[] Field,
            bool[] SampleMask,
            int Width,
            int Height,
            double Ex,
            double Ey,
            out double ValueM
            )
        {
            if (TrySampleBilinear(Field, SampleMask, Width, Height, Ex, Ey, out ValueM))
                return true;

            int X0 = (int)Math.Floor(Ex);
            int Y0 = (int)Math.Floor(Ey);
            int X1 = X0 + 1;
            int Y1 = Y0 + 1;
            double Tx = Ex - X0;
            double Ty = Ey - Y0;

            bool Has00 = TryReadCell(Field, SampleMask, Width, Height, X0, Y0, out double V00);
            bool Has10 = TryReadCell(Field, SampleMask, Width, Height, X1, Y0, out double V10);
            bool Has01 = TryReadCell(Field, SampleMask, Width, Height, X0, Y1, out double V01);
            bool Has11 = TryReadCell(Field, SampleMask, Width, Height, X1, Y1, out double V11);

            if (Has00 || Has10 || Has01 || Has11)
            {
                ValueM = AverageAvailableCorners(V00, V10, V01, V11, Has00, Has10, Has01, Has11, Tx, Ty);
                return !double.IsNaN(ValueM);
            }

            return TrySampleNearest(Field, SampleMask, Width, Height, Ex, Ey, out ValueM);
        }

        /// <summary>
        /// Bilinearly blends whichever corner samples are available at a field edge.
        /// </summary>
        static private double AverageAvailableCorners
            (
            double V00,
            double V10,
            double V01,
            double V11,
            bool Has00,
            bool Has10,
            bool Has01,
            bool Has11,
            double Tx,
            double Ty
            )
        {
            if (Has00 && Has10 && Has01 && Has11)
            {
                double Top = V00 + (V10 - V00) * Tx;
                double Bottom = V01 + (V11 - V01) * Tx;
                return Top + (Bottom - Top) * Ty;
            }

            double Sum = 0.0;
            int Count = 0;
            if (Has00)
            {
                Sum += V00;
                Count++;
            }

            if (Has10)
            {
                Sum += V10;
                Count++;
            }

            if (Has01)
            {
                Sum += V01;
                Count++;
            }

            if (Has11)
            {
                Sum += V11;
                Count++;
            }

            return Count > 0 ? Sum / Count : double.NaN;
        }

        /// <summary>Maximum ring radius when searching for a nearest sampleable cell.</summary>
        private const int NEAREST_SEARCH_MAX_RADIUS_BINS = 24;

        /// <summary>
        /// Finds the nearest sampleable cell to a fractional bin coordinate.
        /// </summary>
        static private bool TrySampleNearest
            (
            double[] Field,
            bool[] SampleMask,
            int Width,
            int Height,
            double Ex,
            double Ey,
            out double ValueM
            )
        {
            ValueM = double.NaN;
            int CentreX = (int)Math.Round(Ex);
            int CentreY = (int)Math.Round(Ey);
            int MaxRadius = Math.Min(NEAREST_SEARCH_MAX_RADIUS_BINS, Math.Max(Width, Height));
            for (int Radius = 0; Radius <= MaxRadius; Radius++)
            {
                for (int Dy = -Radius; Dy <= Radius; Dy++)
                {
                    for (int Dx = -Radius; Dx <= Radius; Dx++)
                    {
                        if (Radius > 0 && Math.Abs(Dx) < Radius && Math.Abs(Dy) < Radius)
                            continue;

                        if (TryReadCell(Field, SampleMask, Width, Height, CentreX + Dx, CentreY + Dy, out ValueM))
                            return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// Applies baseline blur and normalized residual warp to a compact proposed field.
        /// </summary>
        /// <param name="ObservedValues">Known proposed elevations at observed cells.</param>
        /// <param name="ObservedMask">True where a bin proposed elevation exists.</param>
        /// <param name="Width">Grid width in bins.</param>
        /// <param name="Height">Grid height in bins.</param>
        /// <returns>Warp-smoothed proposed elevations.</returns>
        static private double[] BuildWarpSmoothedField
            (
            double[] ObservedValues,
            bool[] ObservedMask,
            int Width,
            int Height
            )
        {
            int CellCount = Width * Height;
            double[] ZBase = new double[CellCount];
            bool HasValid = false;
            double SumValid = 0.0;
            int ValidCount = 0;
            for (int Idx = 0; Idx < CellCount; Idx++)
            {
                if (!ObservedMask[Idx])
                    continue;

                HasValid = true;
                SumValid += ObservedValues[Idx];
                ValidCount++;
            }

            double FillM = HasValid && ValidCount > 0 ? SumValid / ValidCount : DEFAULT_FILL_ELEVATION_M;
            for (int Idx = 0; Idx < CellCount; Idx++)
                ZBase[Idx] = ObservedMask[Idx] ? ObservedValues[Idx] : FillM;

            double[] ZSmooth = BoxBlur2d(ZBase, Width, Height, WARP_BASE_RADIUS_BINS, WARP_BASE_PASSES);
            double[] ResidualObs = new double[CellCount];
            for (int Idx = 0; Idx < CellCount; Idx++)
            {
                if (ObservedMask[Idx])
                    ResidualObs[Idx] = ObservedValues[Idx] - ZSmooth[Idx];
            }

            double[] Corr = NormalizedResidualWarp(ResidualObs, ObservedMask, Width, Height, WARP_RADIUS_BINS, WARP_PASSES);
            double[] ZFinal = new double[CellCount];
            for (int Idx = 0; Idx < CellCount; Idx++)
                ZFinal[Idx] = ZSmooth[Idx] + WARP_ALPHA * Corr[Idx];

            double[] Err = new double[CellCount];
            double[] Excess = new double[CellCount];
            bool[] Need = new bool[CellCount];
            RefineWarpTolerance(ObservedValues, ObservedMask, ZFinal, Err, Excess, Need, Width, Height,
                WARP_TOL_ITERS, WARP_TOL_RADIUS_BINS, WARP_TOL_PASSES, WARP_TOL_ALPHA);

            if (WARP_POST_RADIUS_BINS > 0 && WARP_POST_PASSES > 0)
            {
                ZFinal = BoxBlur2d(ZFinal, Width, Height, WARP_POST_RADIUS_BINS, WARP_POST_PASSES);
                RefineWarpTolerance(ObservedValues, ObservedMask, ZFinal, Err, Excess, Need, Width, Height,
                    WARP_POST_TOL_ITERS, WARP_TOL_RADIUS_BINS, WARP_TOL_PASSES, WARP_TOL_ALPHA);
            }

            for (int Idx = 0; Idx < CellCount; Idx++)
            {
                if (!ObservedMask[Idx])
                    continue;

                double ErrorM = ObservedValues[Idx] - ZFinal[Idx];
                if (ErrorM < -WARP_TOL_M)
                    ErrorM = -WARP_TOL_M;
                else if (ErrorM > WARP_TOL_M)
                    ErrorM = WARP_TOL_M;

                ZFinal[Idx] = ObservedValues[Idx] - ErrorM;
            }

            return ZFinal;
        }

        /// <summary>
        /// Clamps observed cells back to the warp tolerance band after a display blur pass.
        /// </summary>
        /// <param name="Surface">Surface values to clamp in place.</param>
        /// <param name="ObservedValues">Target elevations at observed cells.</param>
        /// <param name="ObservedMask">True where a bin observation exists.</param>
        static private void ClampObservedCells
            (
            double[] Surface,
            double[] ObservedValues,
            bool[] ObservedMask
            )
        {
            for (int Idx = 0; Idx < Surface.Length; Idx++)
            {
                if (!ObservedMask[Idx])
                    continue;

                double ErrorM = ObservedValues[Idx] - Surface[Idx];
                if (ErrorM < -WARP_TOL_M)
                    ErrorM = -WARP_TOL_M;
                else if (ErrorM > WARP_TOL_M)
                    ErrorM = WARP_TOL_M;

                Surface[Idx] = ObservedValues[Idx] - ErrorM;
            }
        }

        /// <summary>
        /// Bilinearly samples a continuous design-grid field at fractional bin coordinates.
        /// </summary>
        /// <param name="Field">Row-major elevation samples.</param>
        /// <param name="ValidMask">True for cells that may participate in interpolation.</param>
        /// <param name="Width">Grid width in bins.</param>
        /// <param name="Height">Grid height in bins.</param>
        /// <param name="Ex">Fractional bin index in the easting direction.</param>
        /// <param name="Ey">Fractional bin index in the northing direction.</param>
        /// <param name="ValueM">Output interpolated elevation in metres.</param>
        /// <returns>True when all four corner cells are valid.</returns>
        public static bool TrySampleBilinear
            (
            double[] Field,
            bool[] ValidMask,
            int Width,
            int Height,
            double Ex,
            double Ey,
            out double ValueM
            )
        {
            ValueM = double.NaN;
            int X0 = (int)Math.Floor(Ex);
            int Y0 = (int)Math.Floor(Ey);
            int X1 = X0 + 1;
            int Y1 = Y0 + 1;
            double Tx = Ex - X0;
            double Ty = Ey - Y0;

            if (!TryReadCell(Field, ValidMask, Width, Height, X0, Y0, out double V00) ||
                !TryReadCell(Field, ValidMask, Width, Height, X1, Y0, out double V10) ||
                !TryReadCell(Field, ValidMask, Width, Height, X0, Y1, out double V01) ||
                !TryReadCell(Field, ValidMask, Width, Height, X1, Y1, out double V11))
                return false;

            double Top = V00 + (V10 - V00) * Tx;
            double Bottom = V01 + (V11 - V01) * Tx;
            ValueM = Top + (Bottom - Top) * Ty;
            return true;
        }

        /// <summary>
        /// Reads one valid grid cell from a row-major field.
        /// </summary>
        static private bool TryReadCell
            (
            double[] Field,
            bool[] ValidMask,
            int Width,
            int Height,
            int X,
            int Y,
            out double ValueM
            )
        {
            ValueM = double.NaN;
            if (X < 0 || Y < 0 || X >= Width || Y >= Height)
                return false;

            int Idx = Y * Width + X;
            if (!ValidMask[Idx])
                return false;

            ValueM = Field[Idx];
            return true;
        }

        /// <summary>
        /// Iteratively warps a surface toward observed bin elevations within tolerance.
        /// </summary>
        static private void RefineWarpTolerance
            (
            double[] ObservedValues,
            bool[] ObservedMask,
            double[] Surface,
            double[] Err,
            double[] Excess,
            bool[] Need,
            int Width,
            int Height,
            int MaxIterations,
            int RadiusBins,
            int Passes,
            double Alpha
            )
        {
            for (int Iter = 0; Iter < MaxIterations; Iter++)
            {
                bool AnyNeed = false;
                for (int Idx = 0; Idx < Surface.Length; Idx++)
                {
                    Err[Idx] = 0.0;
                    Excess[Idx] = 0.0;
                    Need[Idx] = false;
                    if (!ObservedMask[Idx])
                        continue;

                    Err[Idx] = ObservedValues[Idx] - Surface[Idx];
                    double AbsErr = Math.Abs(Err[Idx]);
                    if (AbsErr > WARP_TOL_M)
                    {
                        Need[Idx] = true;
                        AnyNeed = true;
                        Excess[Idx] = Math.Sign(Err[Idx]) * (AbsErr - WARP_TOL_M);
                    }
                }

                if (!AnyNeed)
                    break;

                double[] CorrEx = NormalizedResidualWarp(Excess, Need, Width, Height, RadiusBins, Passes);
                for (int Idx = 0; Idx < Surface.Length; Idx++)
                    Surface[Idx] += Alpha * CorrEx[Idx];
            }
        }

        /// <summary>
        /// Spreads observed residuals across the grid using normalized box-blur weights.
        /// </summary>
        static private double[] NormalizedResidualWarp
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
            Parallel.For(0, ResidualAtObs.Length, Idx =>
            {
                if (!ObservedMask[Idx])
                    return;

                MaskedResidual[Idx] = ResidualAtObs[Idx];
                MaskAsDouble[Idx] = OBSERVED_MASK_TRUE;
            });

            double[] Num = BoxBlur2d(MaskedResidual, Width, Height, RadiusBins, Passes);
            double[] Den = BoxBlur2d(MaskAsDouble, Width, Height, RadiusBins, Passes);
            double[] Corr = new double[ResidualAtObs.Length];
            Parallel.For(0, Corr.Length, Idx =>
                Corr[Idx] = Num[Idx] / Math.Max(Den[Idx], FieldConstants.EPSILON_FINE));

            return Corr;
        }

        /// <summary>
        /// Applies separable X/Y box blurs for the requested number of passes.
        /// </summary>
        static private double[] BoxBlur2d
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

            for (int Pass = 0; Pass < Passes; Pass++)
            {
                OutArr = BoxBlur1dAxis(OutArr, Width, Height, Radius, AxisX: true);
                OutArr = BoxBlur1dAxis(OutArr, Width, Height, Radius, AxisX: false);
            }

            return OutArr;
        }

        /// <summary>
        /// Applies one axis of a separable box blur with edge clamping.
        /// </summary>
        static private double[] BoxBlur1dAxis
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
            if (AxisX)
            {
                Parallel.For(0, Height, Y =>
                {
                    int Row = Y * Width;
                    for (int X = 0; X < Width; X++)
                    {
                        double Sum = 0.0;
                        for (int Offset = -Radius; Offset <= Radius; Offset++)
                        {
                            int SampleX = X + Offset;
                            if (SampleX < 0)
                                SampleX = 0;
                            else if (SampleX >= Width)
                                SampleX = Width - 1;

                            Sum += Arr[Row + SampleX];
                        }

                        OutArr[Row + X] = Sum / Window;
                    }
                });
            }
            else
            {
                Parallel.For(0, Height, Y =>
                {
                    for (int X = 0; X < Width; X++)
                    {
                        double Sum = 0.0;
                        for (int Offset = -Radius; Offset <= Radius; Offset++)
                        {
                            int SampleY = Y + Offset;
                            if (SampleY < 0)
                                SampleY = 0;
                            else if (SampleY >= Height)
                                SampleY = Height - 1;

                            Sum += Arr[SampleY * Width + X];
                        }

                        OutArr[Y * Width + X] = Sum / Window;
                    }
                });
            }

            return OutArr;
        }
    }
}
