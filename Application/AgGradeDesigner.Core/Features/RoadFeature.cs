namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// One committed road: an ordered centreline, a bed width, a height, and a side slope.
    /// </summary>
    public sealed class RoadFeature
    {
        /// <summary>Ordered centreline vertices. At least two after a successful commit.</summary>
        private readonly List<FeatureVertex> _Vertices = new List<FeatureVertex>();

        /// <summary>
        /// Creates a road with a stable identifier and centreline.
        /// </summary>
        /// <param name="Id">Stable identifier such as road_1.</param>
        /// <param name="Vertices">Centreline from start to end.</param>
        /// <param name="BedWidthM">Full road bed width in metres.</param>
        /// <param name="HeightM">Embankment height in metres.</param>
        /// <param name="SideSlope">Horizontal run per one vertical.</param>
        public RoadFeature
            (
            string Id,
            IReadOnlyList<FeatureVertex> Vertices,
            double BedWidthM,
            double HeightM,
            double SideSlope
            )
        {
            this.Id = Id;
            this.BedWidthM = BedWidthM > 0.0 ? BedWidthM : RoadCrossSection.DefaultBedWidthM(0.0, RoadCrossSection.DEFAULT_SIDE_SLOPE);
            this.HeightM = HeightM > 0.0 ? HeightM : RoadCrossSection.DefaultHeightM;
            this.SideSlope = (SideSlope >= 0.0) && !double.IsNaN(SideSlope) && !double.IsInfinity(SideSlope)
                ? SideSlope
                : RoadCrossSection.DEFAULT_SIDE_SLOPE;
            _Vertices.AddRange(Vertices);
        }

        /// <summary>Stable identifier unique among roads in the project.</summary>
        public string Id { get; }

        /// <summary>Full road bed width in metres.</summary>
        public double BedWidthM { get; set; }

        /// <summary>Embankment height in metres.</summary>
        public double HeightM { get; set; }

        /// <summary>Side slope as horizontal run per one vertical.</summary>
        public double SideSlope { get; set; }

        /// <summary>Ordered centreline vertices from start to end.</summary>
        public IReadOnlyList<FeatureVertex> Vertices => _Vertices;

        /// <summary>First centreline vertex.</summary>
        public FeatureVertex Start => _Vertices[0];

        /// <summary>Last centreline vertex.</summary>
        public FeatureVertex End => _Vertices[_Vertices.Count - 1];

        /// <summary>
        /// Replaces the centreline. Used when an edit is saved.
        /// </summary>
        /// <param name="Vertices">New centreline from start to end.</param>
        public void ReplaceVertices
            (
            IReadOnlyList<FeatureVertex> Vertices
            )
        {
            _Vertices.Clear();
            _Vertices.AddRange(Vertices);
        }
    }
}
