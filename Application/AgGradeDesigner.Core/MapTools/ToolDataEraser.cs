namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// Identifies one tool polyline segment selected for erase preview or deletion.
    /// </summary>
    public readonly struct ToolPolylineSegmentRef
    {
        /// <summary>Parent polyline identifier.</summary>
        public string PolylineId { get; init; }

        /// <summary>Zero-based index of the segment's start vertex.</summary>
        public int SegmentIndex { get; init; }
    }

    /// <summary>
    /// Preview of tool data that would be removed by an in-progress erase rectangle (TOOLS-SPEC §3.3.4).
    /// </summary>
    public sealed class ToolErasePreview
    {
        /// <summary>Screen-space erase rectangle.</summary>
        public ToolScreenRect ScreenRect { get; init; }

        /// <summary>Active erase selection rule for this drag.</summary>
        public EraseSelectionMode SelectionMode { get; init; }

        /// <summary>Tool point identifiers that would be deleted.</summary>
        public IReadOnlyList<string> PointIds { get; init; } = Array.Empty<string>();

        /// <summary>Tool polyline segments that would be deleted.</summary>
        public IReadOnlyList<ToolPolylineSegmentRef> Segments { get; init; } = Array.Empty<ToolPolylineSegmentRef>();
    }

    /// <summary>
    /// Applies erase rectangle rules to session tool data (TOOLS-SPEC §3.3, §4.4).
    /// </summary>
    static public class ToolDataEraser
    {
        /// <summary>
        /// Builds a deletion preview for the supplied rectangle and selection mode.
        /// </summary>
        /// <param name="Session">Current tool data session.</param>
        /// <param name="ScreenRect">Normalized erase rectangle in viewport pixels.</param>
        /// <param name="SelectionMode">Segment selection rule for this drag direction.</param>
        /// <param name="ProjectToScreen">Projects geographic points to screen space.</param>
        /// <returns>Preview of affected points and segments.</returns>
        static public ToolErasePreview BuildPreview
            (
            ToolDataSession Session,
            ToolScreenRect ScreenRect,
            EraseSelectionMode SelectionMode,
            ToolDataProjectToScreenHandler ProjectToScreen
            )
        {
            List<string> PointIds = new List<string>();
            List<ToolPolylineSegmentRef> Segments = new List<ToolPolylineSegmentRef>();

            foreach (ToolPointRecord Point in Session.Points)
            {
                if (!ProjectToScreen(Point.Latitude, Point.Longitude, out float X, out float Y))
                    continue;

                if (ScreenRect.ContainsScreenPoint(X, Y))
                    PointIds.Add(Point.Id);
            }

            foreach (ToolPolylineRecord Polyline in Session.Polylines)
                CollectSegmentsForRect(Polyline, ScreenRect, SelectionMode, ProjectToScreen, Segments);

            if (Session.CurrentPolylineVertices.Count >= 2)
            {
                ToolPolylineRecord InProgress = new ToolPolylineRecord(
                    string.Empty,
                    Session.CurrentPolylineVertices);
                CollectSegmentsForRect(InProgress, ScreenRect, SelectionMode, ProjectToScreen, Segments);
            }

            return new ToolErasePreview
            {
                ScreenRect = ScreenRect,
                SelectionMode = SelectionMode,
                PointIds = PointIds,
                Segments = Segments,
            };
        }

        /// <summary>
        /// Removes tool data selected by the erase rectangle from the session.
        /// </summary>
        /// <param name="Session">Session mutated in place.</param>
        /// <param name="ScreenRect">Normalized erase rectangle in viewport pixels.</param>
        /// <param name="SelectionMode">Segment selection rule for this drag direction.</param>
        /// <param name="ProjectToScreen">Projects geographic points to screen space.</param>
        static public void Apply
            (
            ToolDataSession Session,
            ToolScreenRect ScreenRect,
            EraseSelectionMode SelectionMode,
            ToolDataProjectToScreenHandler ProjectToScreen
            )
        {
            ToolErasePreview Preview = BuildPreview(Session, ScreenRect, SelectionMode, ProjectToScreen);

            HashSet<string> DeletePointIds = new HashSet<string>(Preview.PointIds, StringComparer.Ordinal);
            Session.RemovePointsById(DeletePointIds);

            HashSet<string> AffectedPolylineIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (ToolPolylineSegmentRef Segment in Preview.Segments)
                AffectedPolylineIds.Add(Segment.PolylineId);

            foreach (string PolylineId in AffectedPolylineIds)
            {
                if (string.IsNullOrEmpty(PolylineId))
                {
                    RemoveInProgressSegments(Session, ScreenRect, SelectionMode, ProjectToScreen);
                    continue;
                }

                ToolPolylineRecord? Polyline = Session.FindPolyline(PolylineId);
                if (Polyline == null)
                    continue;

                bool[] KeepSegment = BuildKeepSegmentMask(Polyline, ScreenRect, SelectionMode, ProjectToScreen);
                List<IReadOnlyList<ToolPolylineVertexRecord>> Chains = RebuildVertexChainsAfterSegmentRemoval(
                    Polyline.Vertices,
                    KeepSegment,
                    ScreenRect,
                    ProjectToScreen);
                Session.ReplacePolylineWithChains(PolylineId, Chains);
            }

            Session.FinalizePolylineFragments();
        }

        /// <summary>
        /// Collects segment references on one polyline that match the erase rectangle rules.
        /// </summary>
        /// <param name="Polyline">Polyline to inspect.</param>
        /// <param name="ScreenRect">Erase rectangle in screen space.</param>
        /// <param name="SelectionMode">Segment selection rule.</param>
        /// <param name="ProjectToScreen">Screen projection delegate.</param>
        /// <param name="Segments">Output list appended with matching segments.</param>
        static private void CollectSegmentsForRect
            (
            ToolPolylineRecord Polyline,
            ToolScreenRect ScreenRect,
            EraseSelectionMode SelectionMode,
            ToolDataProjectToScreenHandler ProjectToScreen,
            List<ToolPolylineSegmentRef> Segments
            )
        {
            bool[] KeepSegment = BuildKeepSegmentMask(Polyline, ScreenRect, SelectionMode, ProjectToScreen);
            for (Int32 Index = 0; Index < KeepSegment.Length; Index++)
            {
                if (KeepSegment[Index])
                    continue;

                Segments.Add(new ToolPolylineSegmentRef
                {
                    PolylineId = Polyline.Id,
                    SegmentIndex = Index,
                });
            }
        }

        /// <summary>
        /// Builds a per-segment mask where true means the segment survives the erase operation.
        /// </summary>
        /// <param name="Polyline">Polyline whose segments are evaluated.</param>
        /// <param name="ScreenRect">Erase rectangle in screen space.</param>
        /// <param name="SelectionMode">Segment selection rule.</param>
        /// <param name="ProjectToScreen">Screen projection delegate.</param>
        /// <returns>One flag per segment; true retains the segment.</returns>
        static private bool[] BuildKeepSegmentMask
            (
            ToolPolylineRecord Polyline,
            ToolScreenRect ScreenRect,
            EraseSelectionMode SelectionMode,
            ToolDataProjectToScreenHandler ProjectToScreen
            )
        {
            Int32 VertexCount = Polyline.Vertices.Count;
            if (VertexCount < 2)
                return Array.Empty<bool>();

            bool[] Inside = new bool[VertexCount];
            for (Int32 Index = 0; Index < VertexCount; Index++)
            {
                ToolPolylineVertexRecord Vertex = Polyline.Vertices[Index];
                if (!ProjectToScreen(Vertex.Latitude, Vertex.Longitude, out float X, out float Y))
                {
                    Inside[Index] = false;
                    continue;
                }

                Inside[Index] = ScreenRect.ContainsScreenPoint(X, Y);
            }

            Int32 SegmentCount = VertexCount - 1;
            bool[] Keep = new bool[SegmentCount];
            for (Int32 SegmentIndex = 0; SegmentIndex < SegmentCount; SegmentIndex++)
            {
                if (!Inside[SegmentIndex] || !Inside[SegmentIndex + 1])
                {
                    Keep[SegmentIndex] = true;
                    continue;
                }

                if (SelectionMode == EraseSelectionMode.CompletelyInside)
                {
                    Keep[SegmentIndex] = false;
                    continue;
                }

                // Enter-and-inside removes only boundary segments of an inside run (TOOLS-SPEC §3.3.7).
                bool PreviousInside = (SegmentIndex > 0) && Inside[SegmentIndex - 1];
                bool NextInside = (SegmentIndex + 2 < VertexCount) && Inside[SegmentIndex + 2];
                Keep[SegmentIndex] = PreviousInside && NextInside;
            }

            return Keep;
        }

        /// <summary>
        /// Rebuilds vertex chains after segment removal and excises interior vertices in the rectangle.
        /// </summary>
        /// <param name="Vertices">Original ordered vertices.</param>
        /// <param name="KeepSegment">Per-segment retention mask.</param>
        /// <param name="ScreenRect">Erase rectangle in screen space.</param>
        /// <param name="ProjectToScreen">Screen projection delegate.</param>
        /// <returns>One vertex list per surviving contiguous chain.</returns>
        static private List<IReadOnlyList<ToolPolylineVertexRecord>> RebuildVertexChainsAfterSegmentRemoval
            (
            IReadOnlyList<ToolPolylineVertexRecord> Vertices,
            bool[] KeepSegment,
            ToolScreenRect ScreenRect,
            ToolDataProjectToScreenHandler ProjectToScreen
            )
        {
            List<IReadOnlyList<ToolPolylineVertexRecord>> Chains = new List<IReadOnlyList<ToolPolylineVertexRecord>>();
            if (Vertices.Count == 0)
                return Chains;

            Int32 SegmentIndex = 0;
            while (SegmentIndex < KeepSegment.Length)
            {
                if (!KeepSegment[SegmentIndex])
                {
                    SegmentIndex++;
                    continue;
                }

                Int32 RunStart = SegmentIndex;
                while ((SegmentIndex < KeepSegment.Length) && KeepSegment[SegmentIndex])
                    SegmentIndex++;

                Int32 RunEndSegment = SegmentIndex - 1;
                Int32 VertexStart = RunStart;
                Int32 VertexEnd = RunEndSegment + 1;
                List<ToolPolylineVertexRecord> Chain = new List<ToolPolylineVertexRecord>();
                for (Int32 VertexIndex = VertexStart; VertexIndex <= VertexEnd; VertexIndex++)
                {
                    if (IsInteriorVertexToRemove(Vertices, VertexIndex, KeepSegment, ScreenRect, ProjectToScreen))
                        continue;

                    Chain.Add(Vertices[VertexIndex]);
                }

                if (Chain.Count > 0)
                    Chains.Add(Chain.ToArray());
            }

            return Chains;
        }

        /// <summary>
        /// Returns whether a vertex lies inside the erase rectangle and is not an endpoint of a remaining segment.
        /// </summary>
        /// <param name="Vertices">Full vertex list before excision.</param>
        /// <param name="VertexIndex">Candidate vertex index.</param>
        /// <param name="KeepSegment">Per-segment retention mask.</param>
        /// <param name="ScreenRect">Erase rectangle in screen space.</param>
        /// <param name="ProjectToScreen">Screen projection delegate.</param>
        /// <returns>True when the vertex should be removed per §3.3.10.</returns>
        static private bool IsInteriorVertexToRemove
            (
            IReadOnlyList<ToolPolylineVertexRecord> Vertices,
            Int32 VertexIndex,
            bool[] KeepSegment,
            ToolScreenRect ScreenRect,
            ToolDataProjectToScreenHandler ProjectToScreen
            )
        {
            ToolPolylineVertexRecord Vertex = Vertices[VertexIndex];
            if (!ProjectToScreen(Vertex.Latitude, Vertex.Longitude, out float X, out float Y))
                return false;

            if (!ScreenRect.ContainsScreenPoint(X, Y))
                return false;

            bool EndpointOfRemaining = false;
            if ((VertexIndex > 0) && KeepSegment[VertexIndex - 1])
                EndpointOfRemaining = true;
            if ((VertexIndex < KeepSegment.Length) && KeepSegment[VertexIndex])
                EndpointOfRemaining = true;

            return !EndpointOfRemaining;
        }

        /// <summary>
        /// Applies erase rules to the in-progress polyline held by the session.
        /// </summary>
        /// <param name="Session">Session whose current polyline is updated.</param>
        /// <param name="ScreenRect">Erase rectangle in screen space.</param>
        /// <param name="SelectionMode">Segment selection rule.</param>
        /// <param name="ProjectToScreen">Screen projection delegate.</param>
        static private void RemoveInProgressSegments
            (
            ToolDataSession Session,
            ToolScreenRect ScreenRect,
            EraseSelectionMode SelectionMode,
            ToolDataProjectToScreenHandler ProjectToScreen
            )
        {
            if (Session.CurrentPolylineVertices.Count < 2)
                return;

            ToolPolylineRecord InProgress = new ToolPolylineRecord(string.Empty, Session.CurrentPolylineVertices);
            bool[] KeepSegment = BuildKeepSegmentMask(InProgress, ScreenRect, SelectionMode, ProjectToScreen);
            List<IReadOnlyList<ToolPolylineVertexRecord>> Chains = RebuildVertexChainsAfterSegmentRemoval(
                InProgress.Vertices,
                KeepSegment,
                ScreenRect,
                ProjectToScreen);
            List<ToolPolylineVertexRecord> Flat = new List<ToolPolylineVertexRecord>();
            foreach (IReadOnlyList<ToolPolylineVertexRecord> Chain in Chains)
                Flat.AddRange(Chain);
            Session.SetCurrentPolylineVertices(Flat);
        }
    }
}
