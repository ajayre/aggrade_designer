using AgGradeDesigner.Core.Geo;

namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// In-memory session store for map-editing tool data (TOOLS-SPEC §4).
    /// </summary>
    public sealed class ToolDataSession
    {
        /// <summary>Raised when points, polylines, or the in-progress polyline change.</summary>
        public event Action? Changed;

        /// <summary>Completed tool points in creation order.</summary>
        private readonly List<ToolPointRecord> _Points = new List<ToolPointRecord>();

        /// <summary>Completed tool polylines in creation order.</summary>
        private readonly List<ToolPolylineRecord> _Polylines = new List<ToolPolylineRecord>();

        /// <summary>Vertices for the polyline currently being drawn.</summary>
        private readonly List<ToolPolylineVertexRecord> _CurrentPolylineVertices = new List<ToolPolylineVertexRecord>();

        /// <summary>Next ordinal assigned to a new tool point identifier.</summary>
        private int _NextPointId = ToolDataConstants.FIRST_FEATURE_ID;

        /// <summary>Next ordinal assigned to a new tool polyline identifier.</summary>
        private int _NextPolylineId = ToolDataConstants.FIRST_FEATURE_ID;

        /// <summary>Completed tool points exposed read-only.</summary>
        public IReadOnlyList<ToolPointRecord> Points => _Points;

        /// <summary>Completed tool polylines exposed read-only.</summary>
        public IReadOnlyList<ToolPolylineRecord> Polylines => _Polylines;

        /// <summary>Vertices appended to the current in-progress polyline.</summary>
        public IReadOnlyList<ToolPolylineVertexRecord> CurrentPolylineVertices => _CurrentPolylineVertices;

        /// <summary>True when any tool data is present in the session.</summary>
        public bool HasData =>
            (_Points.Count > 0) ||
            (_Polylines.Count > 0) ||
            (_CurrentPolylineVertices.Count > 0);

        /// <summary>
        /// Monotonic revision incremented on every mutation; used to invalidate viewer frame caches.
        /// </summary>
        public int ChangeRevision => _ChangeRevision;

        /// <summary>Revision counter backing <see cref="ChangeRevision"/>.</summary>
        private int _ChangeRevision;

        /// <summary>
        /// Clears all tool data from the session (TOOLS-SPEC §4.2.3).
        /// </summary>
        public void Clear
            (
            )
        {
            _Points.Clear();
            _Polylines.Clear();
            _CurrentPolylineVertices.Clear();
            _NextPointId = ToolDataConstants.FIRST_FEATURE_ID;
            _NextPolylineId = ToolDataConstants.FIRST_FEATURE_ID;
            RaiseChanged();
        }

        /// <summary>
        /// Appends one tool point with frozen proposed elevation (TOOLS-SPEC §3.1.3).
        /// </summary>
        /// <param name="Latitude">Click latitude in decimal degrees.</param>
        /// <param name="Longitude">Click longitude in decimal degrees.</param>
        /// <param name="ElevationM">Proposed surface elevation in metres at the click location.</param>
        public void AddPoint
            (
            double Latitude,
            double Longitude,
            double ElevationM
            )
        {
            string Id = FormatPointId(_NextPointId);
            _NextPointId++;
            _Points.Add(new ToolPointRecord(Id, Latitude, Longitude, ElevationM));
            RaiseChanged();
        }

        /// <summary>
        /// Discards any in-progress polyline vertices when Add Polyline is activated again.
        /// </summary>
        public void BeginNewPolyline
            (
            )
        {
            _CurrentPolylineVertices.Clear();
            RaiseChanged();
        }

        /// <summary>
        /// Appends one vertex to the current in-progress polyline (TOOLS-SPEC §3.2.3).
        /// </summary>
        /// <param name="Latitude">Click latitude in decimal degrees.</param>
        /// <param name="Longitude">Click longitude in decimal degrees.</param>
        /// <param name="ElevationM">Proposed surface elevation in metres at the click location.</param>
        public void AppendPolylineVertex
            (
            double Latitude,
            double Longitude,
            double ElevationM
            )
        {
            _CurrentPolylineVertices.Add(new ToolPolylineVertexRecord(Latitude, Longitude, ElevationM));
            RaiseChanged();
        }

        /// <summary>
        /// Completes the current polyline when Add Polyline is deactivated (TOOLS-SPEC §3.2.11–§3.2.14).
        /// </summary>
        public void CompleteCurrentPolyline
            (
            )
        {
            if (_CurrentPolylineVertices.Count == 0)
                return;

            string Id = FormatPolylineId(_NextPolylineId);
            _NextPolylineId++;
            _Polylines.Add(new ToolPolylineRecord(Id, _CurrentPolylineVertices.ToArray()));
            _CurrentPolylineVertices.Clear();
            RaiseChanged();
        }

        /// <summary>
        /// Replaces the in-progress polyline vertex list after partial erase.
        /// </summary>
        /// <param name="Vertices">Remaining vertices for the in-progress polyline.</param>
        public void SetCurrentPolylineVertices
            (
            IReadOnlyList<ToolPolylineVertexRecord> Vertices
            )
        {
            _CurrentPolylineVertices.Clear();
            _CurrentPolylineVertices.AddRange(Vertices);
            RaiseChanged();
        }

        /// <summary>
        /// Removes completed tool points by identifier.
        /// </summary>
        /// <param name="PointIds">Identifiers to remove.</param>
        public void RemovePointsById
            (
            IReadOnlyCollection<string> PointIds
            )
        {
            if (PointIds.Count == 0)
                return;

            _Points.RemoveAll(Point => PointIds.Contains(Point.Id));
            RaiseChanged();
        }

        /// <summary>
        /// Finds one completed polyline by identifier.
        /// </summary>
        /// <param name="PolylineId">Polyline identifier.</param>
        /// <returns>The polyline or null when not found.</returns>
        public ToolPolylineRecord? FindPolyline
            (
            string PolylineId
            )
        {
            for (Int32 Index = 0; Index < _Polylines.Count; Index++)
            {
                if (string.Equals(_Polylines[Index].Id, PolylineId, StringComparison.Ordinal))
                    return _Polylines[Index];
            }

            return null;
        }

        /// <summary>
        /// Replaces one polyline with zero or more vertex chains after segment removal.
        /// </summary>
        /// <param name="PolylineId">Polyline identifier to replace.</param>
        /// <param name="Chains">Surviving vertex chains in path order.</param>
        public void ReplacePolylineWithChains
            (
            string PolylineId,
            IReadOnlyList<IReadOnlyList<ToolPolylineVertexRecord>> Chains
            )
        {
            for (Int32 Index = 0; Index < _Polylines.Count; Index++)
            {
                if (!string.Equals(_Polylines[Index].Id, PolylineId, StringComparison.Ordinal))
                    continue;

                _Polylines.RemoveAt(Index);
                foreach (IReadOnlyList<ToolPolylineVertexRecord> Chain in Chains)
                    AddChainAsPolylineOrPoint(Chain);

                RaiseChanged();
                return;
            }
        }

        /// <summary>
        /// Replaces one polyline with rebuilt vertex chains after segment removal.
        /// </summary>
        /// <param name="PolylineId">Polyline identifier to replace.</param>
        /// <param name="Vertices">Flattened vertex list that may contain multiple chains.</param>
        public void ReplacePolyline
            (
            string PolylineId,
            IReadOnlyList<ToolPolylineVertexRecord> Vertices
            )
        {
            ReplacePolylineWithChains(PolylineId, new List<IReadOnlyList<ToolPolylineVertexRecord>> { Vertices });
        }

        /// <summary>
        /// Converts any single-vertex polylines into tool points (TOOLS-SPEC §4.4.4).
        /// </summary>
        public void FinalizePolylineFragments
            (
            )
        {
            bool ChangedAny = false;
            List<ToolPolylineRecord> Updated = new List<ToolPolylineRecord>();

            foreach (ToolPolylineRecord Polyline in _Polylines)
            {
                if (Polyline.Vertices.Count == 1)
                {
                    PromoteVertexToPoint(Polyline.Vertices[0]);
                    ChangedAny = true;
                    continue;
                }

                Updated.Add(Polyline);
            }

            if (ChangedAny)
            {
                _Polylines.Clear();
                _Polylines.AddRange(Updated);
                RaiseChanged();
            }
        }

        /// <summary>
        /// Builds the <c>get_tool_data</c> JSON payload object (AI Features §9.10).
        /// </summary>
        /// <returns>Anonymous object ready for JSON serialization.</returns>
        public object BuildGetToolDataPayload
            (
            )
        {
            List<object> PointPayloads = new List<object>();
            foreach (ToolPointRecord Point in _Points)
            {
                UTM.UTMCoordinate Utm = UTM.FromLatLon(Point.Latitude, Point.Longitude);
                PointPayloads.Add(new
                {
                    id = Point.Id,
                    x = Utm.Easting,
                    y = Utm.Northing,
                    elevation_m = Point.ElevationM,
                });
            }

            List<object> PolylinePayloads = new List<object>();
            foreach (ToolPolylineRecord Polyline in _Polylines)
                PolylinePayloads.Add(BuildPolylinePayload(Polyline));

            return new
            {
                coordinate_frame = ToolDataConstants.COORDINATE_FRAME_UTM_METRES,
                elevation_reference = ToolDataConstants.ELEVATION_REFERENCE_PROPOSED,
                elevations_frozen_at_creation = true,
                point_count = PointPayloads.Count,
                polyline_count = PolylinePayloads.Count,
                polygon_count = 0,
                points = PointPayloads,
                polylines = PolylinePayloads,
                polygons = Array.Empty<object>(),
            };
        }

        /// <summary>
        /// Builds one polyline entry for <c>get_tool_data</c> JSON.
        /// </summary>
        /// <param name="Polyline">Completed polyline.</param>
        /// <returns>Anonymous polyline payload object.</returns>
        static private object BuildPolylinePayload
            (
            ToolPolylineRecord Polyline
            )
        {
            List<object> VertexPayloads = new List<object>();
            foreach (ToolPolylineVertexRecord Vertex in Polyline.Vertices)
            {
                UTM.UTMCoordinate Utm = UTM.FromLatLon(Vertex.Latitude, Vertex.Longitude);
                VertexPayloads.Add(new
                {
                    x = Utm.Easting,
                    y = Utm.Northing,
                    elevation_m = Vertex.ElevationM,
                });
            }

            return new
            {
                id = Polyline.Id,
                vertex_count = VertexPayloads.Count,
                vertices = VertexPayloads,
            };
        }

        /// <summary>
        /// Stores one vertex chain as a polyline or promotes a single vertex to a tool point.
        /// </summary>
        /// <param name="Chain">Contiguous vertex chain.</param>
        private void AddChainAsPolylineOrPoint
            (
            IReadOnlyList<ToolPolylineVertexRecord> Chain
            )
        {
            if (Chain.Count == 0)
                return;

            if (Chain.Count == 1)
            {
                PromoteVertexToPoint(Chain[0]);
                return;
            }

            string Id = FormatPolylineId(_NextPolylineId);
            _NextPolylineId++;
            _Polylines.Add(new ToolPolylineRecord(Id, Chain));
        }

        /// <summary>
        /// Converts one polyline vertex into a standalone tool point.
        /// </summary>
        /// <param name="Vertex">Vertex to promote.</param>
        private void PromoteVertexToPoint
            (
            ToolPolylineVertexRecord Vertex
            )
        {
            string Id = FormatPointId(_NextPointId);
            _NextPointId++;
            _Points.Add(new ToolPointRecord(Id, Vertex.Latitude, Vertex.Longitude, Vertex.ElevationM));
        }

        /// <summary>
        /// Formats a tool point identifier.
        /// </summary>
        /// <param name="Ordinal">Positive ordinal.</param>
        /// <returns>Stable point identifier.</returns>
        static private string FormatPointId
            (
            int Ordinal
            )
        {
            return ToolDataConstants.TOOL_POINT_ID_PREFIX + Ordinal.ToString();
        }

        /// <summary>
        /// Formats a tool polyline identifier.
        /// </summary>
        /// <param name="Ordinal">Positive ordinal.</param>
        /// <returns>Stable polyline identifier.</returns>
        static private string FormatPolylineId
            (
            int Ordinal
            )
        {
            return ToolDataConstants.TOOL_POLYLINE_ID_PREFIX + Ordinal.ToString();
        }

        /// <summary>
        /// Raises the <see cref="Changed"/> event when listeners are attached.
        /// </summary>
        private void RaiseChanged
            (
            )
        {
            _ChangeRevision++;
            Action? Handler = Changed;
            Handler?.Invoke();
        }
    }
}
