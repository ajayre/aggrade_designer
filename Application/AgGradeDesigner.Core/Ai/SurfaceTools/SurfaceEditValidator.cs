using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Validates a candidate surface against optional edit constraints.
    /// </summary>
    static public class SurfaceEditValidator
    {
        /// <summary>Cell-span used by central-difference slope estimation.</summary>
        private const double CENTRAL_DIFFERENCE_SPAN = 2.0;

        /// <summary>
        /// Validates the requested surface layer against optional constraints.
        /// </summary>
        /// <param name="Grid">Design grid containing the candidate surface.</param>
        /// <param name="Layer">Surface layer to validate.</param>
        /// <param name="Constraints">Optional validation thresholds.</param>
        /// <returns>Validation outcome with zero or more violations.</returns>
        static public SurfaceValidationResult ValidateSurfaceEdit
            (
            DesignGrid Grid,
            SurfaceLayerKind Layer,
            SurfaceEditConstraints? Constraints
            )
        {
            List<SurfaceConstraintViolation> Violations = new List<SurfaceConstraintViolation>();
            if (Constraints == null)
            {
                return new SurfaceValidationResult
                {
                    Valid = true,
                    Surface = Layer.ToString().ToLowerInvariant(),
                    Violations = Violations
                };
            }

            ValidateElevations(Grid, Layer, Constraints, Violations);
            ValidateSlopes(Grid, Layer, Constraints, Violations);
            ValidateVolumes(Grid, Constraints, Violations);

            return new SurfaceValidationResult
            {
                Valid = Violations.Count == 0,
                Surface = Layer.ToString().ToLowerInvariant(),
                Violations = Violations
            };
        }

        /// <summary>
        /// Checks proposed elevation limits across valid bins.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Layer">Surface layer.</param>
        /// <param name="Constraints">Validation thresholds.</param>
        /// <param name="Violations">Violation list appended in place.</param>
        static private void ValidateElevations
            (
            DesignGrid Grid,
            SurfaceLayerKind Layer,
            SurfaceEditConstraints Constraints,
            List<SurfaceConstraintViolation> Violations
            )
        {
            if (!Constraints.MinElevationM.HasValue && !Constraints.MaxElevationM.HasValue)
                return;

            int BelowCount = 0;
            int AboveCount = 0;
            double? MinObserved = null;
            double? MaxObserved = null;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                double Z = ReadLayer(Bin, Layer);
                MinObserved = MinObserved.HasValue ? Math.Min(MinObserved.Value, Z) : Z;
                MaxObserved = MaxObserved.HasValue ? Math.Max(MaxObserved.Value, Z) : Z;

                if (Constraints.MinElevationM.HasValue && Z < Constraints.MinElevationM.Value)
                    BelowCount++;

                if (Constraints.MaxElevationM.HasValue && Z > Constraints.MaxElevationM.Value)
                    AboveCount++;
            }

            if (BelowCount > 0)
            {
                Violations.Add(new SurfaceConstraintViolation
                {
                    Code = "min_elevation_exceeded",
                    Message = $"{BelowCount} bins fall below min_elevation_m",
                    BinCount = BelowCount,
                    Limit = Constraints.MinElevationM,
                    MinObservedSlopeMm = MinObserved
                });
            }

            if (AboveCount > 0)
            {
                Violations.Add(new SurfaceConstraintViolation
                {
                    Code = "max_elevation_exceeded",
                    Message = $"{AboveCount} bins exceed max_elevation_m",
                    BinCount = AboveCount,
                    Limit = Constraints.MaxElevationM,
                    MaxObservedSlopeMm = MaxObserved
                });
            }
        }

        /// <summary>
        /// Checks local slope limits across valid bins.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Layer">Surface layer.</param>
        /// <param name="Constraints">Validation thresholds.</param>
        /// <param name="Violations">Violation list appended in place.</param>
        static private void ValidateSlopes
            (
            DesignGrid Grid,
            SurfaceLayerKind Layer,
            SurfaceEditConstraints Constraints,
            List<SurfaceConstraintViolation> Violations
            )
        {
            if (!Constraints.MaxSlopeMm.HasValue && !Constraints.MinSlopeMm.HasValue)
                return;

            int MaxViolations = 0;
            int MinViolations = 0;
            double MaxObserved = 0.0;
            double MinObserved = double.PositiveInfinity;

            foreach (GridBinData Bin in Grid.Bins)
            {
                if (!Bin.Valid)
                    continue;

                if (!TryComputeBinSlope(Grid, Layer, Bin.X, Bin.Y, out double SlopeMm))
                    continue;

                MaxObserved = Math.Max(MaxObserved, SlopeMm);
                MinObserved = Math.Min(MinObserved, SlopeMm);

                if (Constraints.MaxSlopeMm.HasValue && SlopeMm > Constraints.MaxSlopeMm.Value)
                    MaxViolations++;

                if (Constraints.MinSlopeMm.HasValue && SlopeMm < Constraints.MinSlopeMm.Value)
                    MinViolations++;
            }

            if (MaxViolations > 0)
            {
                Violations.Add(new SurfaceConstraintViolation
                {
                    Code = "max_slope_exceeded",
                    Message = $"{MaxViolations} bins exceed max_slope_mm",
                    BinCount = MaxViolations,
                    Limit = Constraints.MaxSlopeMm,
                    MaxObservedSlopeMm = MaxObserved
                });
            }

            if (MinViolations > 0)
            {
                Violations.Add(new SurfaceConstraintViolation
                {
                    Code = "min_slope_exceeded",
                    Message = $"{MinViolations} bins fall below min_slope_mm",
                    BinCount = MinViolations,
                    Limit = Constraints.MinSlopeMm,
                    MinObservedSlopeMm = MinObserved
                });
            }
        }

        /// <summary>
        /// Checks cut/fill volume limits against existing ground.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Constraints">Validation thresholds.</param>
        /// <param name="Violations">Violation list appended in place.</param>
        static private void ValidateVolumes
            (
            DesignGrid Grid,
            SurfaceEditConstraints Constraints,
            List<SurfaceConstraintViolation> Violations
            )
        {
            SurfaceCutFillVolumes Volumes = SurfaceVolumeCalculator.CalculateCutFill(
                Grid,
                SurfaceLayerKind.Existing,
                SurfaceLayerKind.Proposed);

            if (Constraints.MaxCutM3.HasValue && Volumes.CutM3 > Constraints.MaxCutM3.Value)
            {
                Violations.Add(new SurfaceConstraintViolation
                {
                    Code = "max_cut_exceeded",
                    Message = "Cut volume exceeds max_cut_m3",
                    Limit = Constraints.MaxCutM3
                });
            }

            if (Constraints.MaxFillM3.HasValue && Volumes.FillM3 > Constraints.MaxFillM3.Value)
            {
                Violations.Add(new SurfaceConstraintViolation
                {
                    Code = "max_fill_exceeded",
                    Message = "Fill volume exceeds max_fill_m3",
                    Limit = Constraints.MaxFillM3
                });
            }

            if (Constraints.MaxNetExportM3.HasValue)
            {
                double NetExportM3 = Math.Max(Volumes.NetM3, 0.0);
                if (NetExportM3 > Constraints.MaxNetExportM3.Value)
                {
                    Violations.Add(new SurfaceConstraintViolation
                    {
                        Code = "max_net_export_exceeded",
                        Message = "Net export exceeds max_net_export_m3",
                        Limit = Constraints.MaxNetExportM3
                    });
                }
            }
        }

        /// <summary>
        /// Estimates local slope magnitude at a bin using central differences.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Layer">Surface layer.</param>
        /// <param name="X">Bin column index.</param>
        /// <param name="Y">Bin row index.</param>
        /// <param name="SlopeMm">Slope magnitude in m/m.</param>
        /// <returns>True when neighbours exist to estimate slope.</returns>
        static private bool TryComputeBinSlope
            (
            DesignGrid Grid,
            SurfaceLayerKind Layer,
            int X,
            int Y,
            out double SlopeMm
            )
        {
            SlopeMm = 0.0;
            if (!Grid.TryGetValidBin(X, Y, out GridBinData Center))
                return false;

            double DzDx = 0.0;
            double DzDy = 0.0;
            bool HasX = false;
            bool HasY = false;

            if (Grid.TryGetValidBin(X + 1, Y, out GridBinData East) &&
                Grid.TryGetValidBin(X - 1, Y, out GridBinData West))
            {
                DzDx = (ReadLayer(East, Layer) - ReadLayer(West, Layer)) / (CENTRAL_DIFFERENCE_SPAN * Grid.BinSizeM);
                HasX = true;
            }

            if (Grid.TryGetValidBin(X, Y + 1, out GridBinData North) &&
                Grid.TryGetValidBin(X, Y - 1, out GridBinData South))
            {
                DzDy = (ReadLayer(North, Layer) - ReadLayer(South, Layer)) / (CENTRAL_DIFFERENCE_SPAN * Grid.BinSizeM);
                HasY = true;
            }

            if (!HasX && !HasY)
                return false;

            SlopeMm = Math.Sqrt(DzDx * DzDx + DzDy * DzDy);
            return true;
        }

        /// <summary>
        /// Reads the requested elevation from a bin.
        /// </summary>
        /// <param name="Bin">Grid bin.</param>
        /// <param name="Layer">Surface layer.</param>
        /// <returns>Elevation in metres.</returns>
        static private double ReadLayer
            (
            GridBinData Bin,
            SurfaceLayerKind Layer
            )
        {
            return Layer == SurfaceLayerKind.Existing ? Bin.ExistingM : Bin.ProposedM;
        }
    }

    /// <summary>
    /// Outcome of <c>validate_surface_edit</c>.
    /// </summary>
    public readonly struct SurfaceValidationResult
    {
        /// <summary>True when no constraint violations were found.</summary>
        public bool Valid { get; init; }

        /// <summary>Validated surface layer identifier.</summary>
        public string Surface { get; init; }

        /// <summary>Constraint violations detected during validation.</summary>
        public IReadOnlyList<SurfaceConstraintViolation> Violations { get; init; }
    }
}
