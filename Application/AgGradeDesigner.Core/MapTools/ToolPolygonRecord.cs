namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// One completed tool polygon comprising ordered boundary vertices and an implied interior (TOOLS-SPEC §3.4).
    /// </summary>
    public sealed class ToolPolygonRecord
    {
        /// <summary>
        /// Creates a completed tool polygon with a stable session identifier.
        /// </summary>
        /// <param name="Id">Stable identifier (<c>pg_n</c>).</param>
        /// <param name="Vertices">Ordered boundary vertices from first to last around the ring.</param>
        public ToolPolygonRecord
            (
            string Id,
            IReadOnlyList<ToolPolylineVertexRecord> Vertices
            )
        {
            this.Id = Id;
            this.Vertices = Vertices;
        }

        /// <summary>Stable session identifier unique among points, polylines, and polygons.</summary>
        public string Id { get; }

        /// <summary>Ordered boundary vertices; consecutive pairs plus the closing edge define segments.</summary>
        public IReadOnlyList<ToolPolylineVertexRecord> Vertices { get; }
    }
}
