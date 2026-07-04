namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// One completed tool polyline comprising ordered vertices and implied segments (TOOLS-SPEC §3.2).
    /// </summary>
    public sealed class ToolPolylineRecord
    {
        /// <summary>
        /// Creates a completed tool polyline with a stable session identifier.
        /// </summary>
        /// <param name="Id">Stable identifier (<c>pl_n</c>).</param>
        /// <param name="Vertices">Ordered vertex chain from first to last.</param>
        public ToolPolylineRecord
            (
            string Id,
            IReadOnlyList<ToolPolylineVertexRecord> Vertices
            )
        {
            this.Id = Id;
            this.Vertices = Vertices;
        }

        /// <summary>Stable session identifier unique among points and polylines.</summary>
        public string Id { get; }

        /// <summary>Ordered vertices defining segments between consecutive pairs.</summary>
        public IReadOnlyList<ToolPolylineVertexRecord> Vertices { get; }
    }
}
