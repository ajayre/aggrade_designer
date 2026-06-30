namespace AgGradeDesigner.Core.Models
{
    /// <summary>
    /// Rectangular UTM-aligned design grid with plane coefficients and per-bin elevations.
    /// </summary>
    public sealed class DesignGrid
    {
        /// <summary>Bin edge length in metres.</summary>
        public double BinSizeM { get; set; } = FieldConstants.BIN_SIZE_M;

        /// <summary>Number of bin columns.</summary>
        public int Width { get; set; }

        /// <summary>Number of bin rows.</summary>
        public int Height { get; set; }

        /// <summary>UTM zone number for the grid origin.</summary>
        public int UtmZone { get; set; }

        /// <summary>True when the grid lies in the northern UTM hemisphere.</summary>
        public bool UtmNorth { get; set; }

        /// <summary>UTM easting of the grid origin corner in metres.</summary>
        public double OriginEastingM { get; set; }

        /// <summary>UTM northing of the grid origin corner in metres.</summary>
        public double OriginNorthingM { get; set; }

        /// <summary>Minimum UTM easting covered by the grid in metres.</summary>
        public double MinX { get; set; }

        /// <summary>Minimum UTM northing covered by the grid in metres.</summary>
        public double MinY { get; set; }

        /// <summary>Maximum UTM easting covered by the grid in metres.</summary>
        public double MaxX { get; set; }

        /// <summary>Maximum UTM northing covered by the grid in metres.</summary>
        public double MaxY { get; set; }

        /// <summary>Design plane coefficient dz/dE (metres per metre easting).</summary>
        public double PlaneA { get; set; }

        /// <summary>Design plane coefficient dz/dN (metres per metre northing).</summary>
        public double PlaneB { get; set; }

        /// <summary>Design plane intercept elevation at the grid origin in metres.</summary>
        public double PlaneC { get; set; }

        /// <summary>Mean latitude of valid bin centroids in decimal degrees.</summary>
        public double MeanLat { get; set; }

        /// <summary>Mean longitude of valid bin centroids in decimal degrees.</summary>
        public double MeanLon { get; set; }

        /// <summary>Total cut volume in cubic yards across valid bins.</summary>
        public double TotalCutCy { get; set; }

        /// <summary>Total fill volume in cubic yards across valid bins.</summary>
        public double TotalFillCy { get; set; }

        /// <summary>Steepest downhill heading of the design plane in degrees clockwise from north.</summary>
        public double CompositeSlopeHeadingDeg { get; set; }

        /// <summary>Steepest downhill grade of the design plane as percent.</summary>
        public double CompositeSlopePercent { get; set; }

        /// <summary>All bin cells including invalid padding outside the boundary.</summary>
        public List<GridBinData> Bins { get; set; } = new List<GridBinData>();

        /// <summary>
        /// Returns the existing elevation at a bin coordinate, or the no-data sentinel when absent.
        /// </summary>
        /// <param name="X">Bin column index.</param>
        /// <param name="Y">Bin row index.</param>
        /// <returns>Existing elevation in metres, or <see cref="FieldConstants.BIN_NO_DATA_SENTINEL"/>.</returns>
        public double GetExisting
            (
            int X,
            int Y
            )
        {
            // Linear scan matches sparse bin storage keyed by grid coordinates.
            GridBinData? Bin = Bins.Find(B => B.X == X && B.Y == Y);
            return Bin?.ExistingM ?? FieldConstants.BIN_NO_DATA_SENTINEL;
        }

        /// <summary>
        /// Returns the proposed elevation at a bin coordinate, or the no-data sentinel when absent.
        /// </summary>
        /// <param name="X">Bin column index.</param>
        /// <param name="Y">Bin row index.</param>
        /// <returns>Proposed elevation in metres, or <see cref="FieldConstants.BIN_NO_DATA_SENTINEL"/>.</returns>
        public double GetProposed
            (
            int X,
            int Y
            )
        {
            // Linear scan matches sparse bin storage keyed by grid coordinates.
            GridBinData? Bin = Bins.Find(B => B.X == X && B.Y == Y);
            return Bin?.ProposedM ?? FieldConstants.BIN_NO_DATA_SENTINEL;
        }

        /// <summary>
        /// Returns whether a bin coordinate holds valid survey and design data.
        /// </summary>
        /// <param name="X">Bin column index.</param>
        /// <param name="Y">Bin row index.</param>
        /// <returns>True when the bin exists and is marked valid.</returns>
        public bool IsValid
            (
            int X,
            int Y
            )
        {
            // Missing bins outside the field boundary are treated as invalid.
            GridBinData? Bin = Bins.Find(B => B.X == X && B.Y == Y);
            return Bin?.Valid ?? false;
        }
    }
}
