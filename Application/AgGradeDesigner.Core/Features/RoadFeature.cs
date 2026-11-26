namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// One committed road: an ordered centreline and a crown width.
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
        /// <param name="CrownWidthM">Full crown width in metres.</param>
        public RoadFeature
            (
            string Id,
            IReadOnlyList<FeatureVertex> Vertices,
            double CrownWidthM
            )
        {
            this.Id = Id;
            this.CrownWidthM = CrownWidthM;
            _Vertices.AddRange(Vertices);
        }

        /// <summary>Stable identifier unique among roads in the project.</summary>
        public string Id { get; }

        /// <summary>Full crown width in metres.</summary>
        public double CrownWidthM { get; set; }

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
