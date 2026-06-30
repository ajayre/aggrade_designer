namespace AgGradeDesigner.Core.Models
{
    /// <summary>
    /// A survey or design point with geodetic and local coordinates plus cut/fill metadata.
    /// </summary>
    public class TopologyPoint
    {
        /// <summary>Latitude in decimal degrees.</summary>
        public double Latitude { get; set; }

        /// <summary>Longitude in decimal degrees.</summary>
        public double Longitude { get; set; }

        /// <summary>Existing ground elevation in metres.</summary>
        public double ExistingElevation { get; set; }

        /// <summary>Proposed design elevation in metres.</summary>
        public double ProposedElevation { get; set; }

        /// <summary>Cut or fill depth in metres (proposed minus existing).</summary>
        public double CutFill { get; set; }

        /// <summary>Survey or grid point code (for example 2PER, 3GRD, BM1).</summary>
        public string? Code { get; set; }

        /// <summary>Optional free-text comments from the source file.</summary>
        public string? Comments { get; set; }

        /// <summary>Local easting offset in metres relative to the field origin.</summary>
        public double X { get; set; }

        /// <summary>Local northing offset in metres relative to the field origin.</summary>
        public double Y { get; set; }
    }
}
