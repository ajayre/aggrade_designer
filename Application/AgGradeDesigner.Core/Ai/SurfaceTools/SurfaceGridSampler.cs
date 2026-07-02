using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Ai.SurfaceTools
{
    /// <summary>
    /// Samples proposed and existing elevations from a <see cref="DesignGrid"/> in UTM metres.
    /// </summary>
    static public class SurfaceGridSampler
    {
        /// <summary>Offset from bin index to bin centre in cell units (0.5 = centre).</summary>
        private const double BIN_CELL_CENTRE_OFFSET = 0.5;
        /// <summary>
        /// Returns the elevation of a surface layer at a UTM point using bilinear interpolation.
        /// </summary>
        /// <param name="Grid">Design grid containing bin elevations.</param>
        /// <param name="Layer">Surface layer to sample.</param>
        /// <param name="EastingM">UTM easting in metres.</param>
        /// <param name="NorthingM">UTM northing in metres.</param>
        /// <param name="ElevationM">Interpolated elevation in metres when successful.</param>
        /// <returns>True when the point lies inside the grid and valid bins were found.</returns>
        static public bool TrySampleElevation
            (
            DesignGrid Grid,
            SurfaceLayerKind Layer,
            double EastingM,
            double NorthingM,
            out double ElevationM
            )
        {
            ElevationM = FieldConstants.BIN_NO_DATA_SENTINEL;
            if (Grid.BinSizeM <= 0 || Grid.Bins.Count == 0)
                return false;

            double LocalX = (EastingM - Grid.OriginEastingM) / Grid.BinSizeM - BIN_CELL_CENTRE_OFFSET;
            double LocalY = (NorthingM - Grid.OriginNorthingM) / Grid.BinSizeM - BIN_CELL_CENTRE_OFFSET;
            int X0 = (int)Math.Floor(LocalX);
            int Y0 = (int)Math.Floor(LocalY);
            double FracX = LocalX - X0;
            double FracY = LocalY - Y0;

            if (!TryGetLayerElevation(Grid, Layer, X0, Y0, out double Z00))
                return false;
            if (!TryGetLayerElevation(Grid, Layer, X0 + 1, Y0, out double Z10))
                return false;
            if (!TryGetLayerElevation(Grid, Layer, X0, Y0 + 1, out double Z01))
                return false;
            if (!TryGetLayerElevation(Grid, Layer, X0 + 1, Y0 + 1, out double Z11))
                return false;

            double Z0 = Z00 + (Z10 - Z00) * FracX;
            double Z1 = Z01 + (Z11 - Z01) * FracX;
            ElevationM = Z0 + (Z1 - Z0) * FracY;
            return true;
        }

        /// <summary>
        /// Reads one bin elevation for the requested surface layer.
        /// </summary>
        /// <param name="Grid">Design grid.</param>
        /// <param name="Layer">Surface layer.</param>
        /// <param name="X">Bin column index.</param>
        /// <param name="Y">Bin row index.</param>
        /// <param name="ElevationM">Elevation in metres when valid.</param>
        /// <returns>True when the bin exists and is valid.</returns>
        static private bool TryGetLayerElevation
            (
            DesignGrid Grid,
            SurfaceLayerKind Layer,
            int X,
            int Y,
            out double ElevationM
            )
        {
            if (!Grid.TryGetValidBin(X, Y, out GridBinData Bin))
            {
                ElevationM = FieldConstants.BIN_NO_DATA_SENTINEL;
                return false;
            }

            ElevationM = Layer == SurfaceLayerKind.Existing ? Bin.ExistingM : Bin.ProposedM;
            return true;
        }
    }
}
