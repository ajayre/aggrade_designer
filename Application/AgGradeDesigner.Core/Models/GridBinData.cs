namespace AgGradeDesigner.Core.Models
{
    /// <summary>
    /// One bin cell in the design grid with existing/proposed elevations and geodetic centroid.
    /// </summary>
    public sealed class GridBinData
    {
        /// <summary>Bin column index in the design grid.</summary>
        public int X { get; set; }

        /// <summary>Bin row index in the design grid.</summary>
        public int Y { get; set; }

        /// <summary>Existing ground elevation in metres at the bin centroid.</summary>
        public double ExistingM { get; set; }

        /// <summary>Proposed design elevation in metres at the bin centroid.</summary>
        public double ProposedM { get; set; }

        /// <summary>Latitude of the bin centroid in decimal degrees.</summary>
        public double CentroidLat { get; set; }

        /// <summary>Longitude of the bin centroid in decimal degrees.</summary>
        public double CentroidLon { get; set; }

        /// <summary>True when the bin lies inside the field boundary and has valid elevations.</summary>
        public bool Valid { get; set; }
    }
}
