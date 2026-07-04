using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.MapTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Draws session map-editing tool data on the field viewer (TOOLS-SPEC §3–§4.5).
    /// </summary>
    static internal class ToolDataRenderer
    {
        /// <summary>
        /// True when tool features use display-snapped elevations (TOOLS-SPEC §4.5.2).
        /// </summary>
        /// <param name="ViewMode">Active viewer mode.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <returns>True in 2D mode or when Cut/fill map is active.</returns>
        static public bool ShouldUseDisplaySnapping
            (
            ViewMode ViewMode,
            FieldContentView ContentView
            )
        {
            return (ViewMode == ViewMode.TwoD) || (ContentView == FieldContentView.CutFill);
        }

        /// <summary>Fill colour for tool point markers.</summary>
        private static readonly SKColor ToolPointFillColor = new SKColor(
            ToolDataConstants.TOOL_POINT_FILL_RED,
            ToolDataConstants.TOOL_POINT_FILL_GREEN,
            ToolDataConstants.TOOL_POINT_FILL_BLUE,
            ToolDataConstants.TOOL_POINT_FILL_ALPHA);

        /// <summary>Stroke colour for tool polyline segments.</summary>
        private static readonly SKColor ToolPolylineStrokeColor = new SKColor(
            ToolDataConstants.TOOL_POLYLINE_STROKE_RED,
            ToolDataConstants.TOOL_POLYLINE_STROKE_GREEN,
            ToolDataConstants.TOOL_POLYLINE_STROKE_BLUE,
            ToolDataConstants.TOOL_POLYLINE_STROKE_ALPHA);

        /// <summary>Fill colour for erase preview highlights on tool points.</summary>
        private static readonly SKColor ErasePreviewPointColor = new SKColor(
            ToolDataConstants.ERASE_PREVIEW_POINT_RED,
            ToolDataConstants.ERASE_PREVIEW_POINT_GREEN,
            ToolDataConstants.ERASE_PREVIEW_POINT_BLUE,
            ToolDataConstants.ERASE_PREVIEW_POINT_ALPHA);

        /// <summary>White outline stroke for tool points and polyline segments.</summary>
        private static readonly SKColor ToolOutlineColor = new SKColor(
            ToolDataConstants.TOOL_OUTLINE_RED,
            ToolDataConstants.TOOL_OUTLINE_GREEN,
            ToolDataConstants.TOOL_OUTLINE_BLUE,
            ToolDataConstants.TOOL_OUTLINE_ALPHA);

        /// <summary>
        /// Draws one tool point marker with a white outline ring.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="X">Screen X in pixels.</param>
        /// <param name="Y">Screen Y in pixels.</param>
        /// <param name="FillPaint">Interior fill paint.</param>
        static private void DrawToolPointMarker
            (
            SKCanvas Canvas,
            float X,
            float Y,
            SKPaint FillPaint
            )
        {
            using SKPaint OutlinePaint = new SKPaint
            {
                Color = ToolOutlineColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = ToolDataConstants.TOOL_POINT_OUTLINE_WIDTH_PX,
            };
            Canvas.DrawCircle(X, Y, ToolDataConstants.TOOL_POINT_RADIUS_PX, OutlinePaint);
            Canvas.DrawCircle(X, Y, ToolDataConstants.TOOL_POINT_RADIUS_PX, FillPaint);
        }

        /// <summary>
        /// Draws one tool polyline segment with a white outline under the coloured stroke.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="StartX">Start screen X in pixels.</param>
        /// <param name="StartY">Start screen Y in pixels.</param>
        /// <param name="EndX">End screen X in pixels.</param>
        /// <param name="EndY">End screen Y in pixels.</param>
        /// <param name="LinePaint">Foreground segment stroke.</param>
        static private void DrawToolPolylineSegment
            (
            SKCanvas Canvas,
            float StartX,
            float StartY,
            float EndX,
            float EndY,
            SKPaint LinePaint
            )
        {
            using SKPaint OutlinePaint = new SKPaint
            {
                Color = ToolOutlineColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = ToolDataConstants.TOOL_POLYLINE_STROKE_WIDTH_PX +
                    (ToolDataConstants.TOOL_POLYLINE_OUTLINE_EXTRA_PX * 2f),
            };
            Canvas.DrawLine(StartX, StartY, EndX, EndY, OutlinePaint);
            Canvas.DrawLine(StartX, StartY, EndX, EndY, LinePaint);
        }

        /// <summary>
        /// Draws tool points and polylines in 2D plan view with display snapping (TOOLS-SPEC §4.5.2).
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Viewport">Viewport rectangle in pixels.</param>
        /// <param name="Session">Session tool data.</param>
        /// <param name="Surface">Sampled field surface for display elevation.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="CentreLat">Map centre latitude in degrees.</param>
        /// <param name="CentreLon">Map centre longitude in degrees.</param>
        /// <param name="MetresPerPixel">Ground metres per screen pixel.</param>
        /// <param name="Preview">Optional erase preview selection.</param>
        static public void Draw2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ToolDataSession? Session,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            ToolErasePreview? Preview = null
            )
        {
            if ((Session == null) || !Session.HasData)
                return;

            MapViewport MapViewport = new MapViewport { Width = Viewport.Width, Height = Viewport.Height };
            HashSet<string> HighlightPoints = BuildHighlightPointSet(Preview);

            using SKPaint PointPaint = new SKPaint
            {
                Color = ToolPointFillColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };
            using SKPaint LinePaint = new SKPaint
            {
                Color = ToolPolylineStrokeColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = ToolDataConstants.TOOL_POLYLINE_STROKE_WIDTH_PX,
            };
            using SKPaint PreviewPaint = new SKPaint
            {
                Color = ErasePreviewPointColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };

            foreach (ToolPointRecord Point in Session.Points)
            {
                if (!MapProjection.LatLonToScreen(
                        Point.Latitude,
                        Point.Longitude,
                        CentreLat,
                        CentreLon,
                        MetresPerPixel,
                        MapViewport,
                        out float X,
                        out float Y))
                    continue;

                SKPaint Paint = HighlightPoints.Contains(Point.Id) ? PreviewPaint : PointPaint;
                DrawToolPointMarker(Canvas, X, Y, Paint);
            }

            DrawPolylineVertices2D(
                Canvas,
                Session.CurrentPolylineVertices,
                Surface,
                ContentView,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                MapViewport,
                PointPaint);
            DrawPolylineSegments2D(
                Canvas,
                Session.CurrentPolylineVertices,
                Surface,
                ContentView,
                CentreLat,
                CentreLon,
                MetresPerPixel,
                MapViewport,
                LinePaint,
                Preview,
                string.Empty);

            foreach (ToolPolylineRecord Polyline in Session.Polylines)
            {
                DrawPolylineVertices2D(
                    Canvas,
                    Polyline.Vertices,
                    Surface,
                    ContentView,
                    CentreLat,
                    CentreLon,
                    MetresPerPixel,
                    MapViewport,
                    PointPaint);
                DrawPolylineSegments2D(
                    Canvas,
                    Polyline.Vertices,
                    Surface,
                    ContentView,
                    CentreLat,
                    CentreLon,
                    MetresPerPixel,
                    MapViewport,
                    LinePaint,
                    Preview,
                    Polyline.Id);
            }
        }

        /// <summary>
        /// Draws tool points and polylines in 3D orbit view (TOOLS-SPEC §3.1.8, §4.5).
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Viewport">Viewport rectangle in pixels.</param>
        /// <param name="Session">Session tool data.</param>
        /// <param name="Surface">Sampled field surface for display snapping.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="Camera">Active orbit camera.</param>
        /// <param name="UseDisplaySnapping">When true, snap display to the active field content view (§4.5.2).</param>
        /// <param name="Preview">Optional erase preview selection.</param>
        static public void Draw3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ToolDataSession? Session,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            bool UseDisplaySnapping = false,
            ToolErasePreview? Preview = null
            )
        {
            if ((Session == null) || !Session.HasData)
                return;

            HashSet<string> HighlightPoints = BuildHighlightPointSet(Preview);
            using SKPaint PointPaint = new SKPaint
            {
                Color = ToolPointFillColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };
            using SKPaint LinePaint = new SKPaint
            {
                Color = ToolPolylineStrokeColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = ToolDataConstants.TOOL_POLYLINE_STROKE_WIDTH_PX,
            };
            using SKPaint PreviewPaint = new SKPaint
            {
                Color = ErasePreviewPointColor,
                IsAntialias = true,
                Style = SKPaintStyle.Fill,
            };

            foreach (ToolPointRecord Point in Session.Points)
                DrawPoint3D(
                    Canvas,
                    Viewport,
                    Point,
                    Surface,
                    ContentView,
                    Camera,
                    UseDisplaySnapping,
                    HighlightPoints.Contains(Point.Id) ? PreviewPaint : PointPaint);

            DrawPolyline3D(
                Canvas,
                Viewport,
                Session.CurrentPolylineVertices,
                Surface,
                ContentView,
                Camera,
                UseDisplaySnapping,
                LinePaint,
                PointPaint,
                Preview,
                string.Empty);
            foreach (ToolPolylineRecord Polyline in Session.Polylines)
            {
                DrawPolyline3D(
                    Canvas,
                    Viewport,
                    Polyline.Vertices,
                    Surface,
                    ContentView,
                    Camera,
                    UseDisplaySnapping,
                    LinePaint,
                    PointPaint,
                    Preview,
                    Polyline.Id);
            }
        }

        /// <summary>
        /// Draws the erase rectangle and preview chrome during an active erase drag.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Preview">Active erase preview state.</param>
        static public void DrawEraseOverlay
            (
            SKCanvas Canvas,
            ToolErasePreview Preview
            )
        {
            ToolScreenRect Rect = Preview.ScreenRect;
            SKRect SkRect = SKRect.Create(Rect.Left, Rect.Top, Rect.Right - Rect.Left, Rect.Bottom - Rect.Top);

            using SKPaint FillPaint = new SKPaint
            {
                Color = new SKColor(
                    ToolDataConstants.ERASE_RECT_FILL_RED,
                    ToolDataConstants.ERASE_RECT_FILL_GREEN,
                    ToolDataConstants.ERASE_RECT_FILL_BLUE,
                    ToolDataConstants.ERASE_RECT_FILL_ALPHA),
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
            };
            using SKPaint BorderPaint = new SKPaint
            {
                Color = new SKColor(
                    ToolDataConstants.ERASE_RECT_BORDER_RED,
                    ToolDataConstants.ERASE_RECT_BORDER_GREEN,
                    ToolDataConstants.ERASE_RECT_BORDER_BLUE,
                    ToolDataConstants.ERASE_RECT_BORDER_ALPHA),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = ToolDataConstants.ERASE_RECT_BORDER_WIDTH_PX,
                IsAntialias = true,
            };

            Canvas.DrawRect(SkRect, FillPaint);
            Canvas.DrawRect(SkRect, BorderPaint);
        }

        /// <summary>
        /// Builds a set of tool point identifiers selected for erase preview highlighting.
        /// </summary>
        /// <param name="Preview">Optional erase preview.</param>
        /// <returns>Highlight identifier set.</returns>
        static private HashSet<string> BuildHighlightPointSet
            (
            ToolErasePreview? Preview
            )
        {
            if (Preview == null)
                return new HashSet<string>(StringComparer.Ordinal);

            return new HashSet<string>(Preview.PointIds, StringComparer.Ordinal);
        }

        /// <summary>
        /// Draws polyline vertex markers in 2D using display-snapped positions.
        /// </summary>
        static private void DrawPolylineVertices2D
            (
            SKCanvas Canvas,
            IReadOnlyList<ToolPolylineVertexRecord> Vertices,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport MapViewport,
            SKPaint Paint
            )
        {
            foreach (ToolPolylineVertexRecord Vertex in Vertices)
            {
                if (!MapProjection.LatLonToScreen(
                        Vertex.Latitude,
                        Vertex.Longitude,
                        CentreLat,
                        CentreLon,
                        MetresPerPixel,
                        MapViewport,
                        out float X,
                        out float Y))
                    continue;

                DrawToolPointMarker(Canvas, X, Y, Paint);
            }
        }

        /// <summary>
        /// Draws straight segments between consecutive polyline vertices in 2D.
        /// </summary>
        static private void DrawPolylineSegments2D
            (
            SKCanvas Canvas,
            IReadOnlyList<ToolPolylineVertexRecord> Vertices,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            MapViewport MapViewport,
            SKPaint LinePaint,
            ToolErasePreview? Preview,
            string PolylineId
            )
        {
            if (Vertices.Count < 2)
                return;

            HashSet<int> HighlightSegments = BuildHighlightSegmentSet(Preview, PolylineId);
            using SKPaint PreviewLinePaint = new SKPaint
            {
                Color = ErasePreviewPointColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = ToolDataConstants.TOOL_POLYLINE_STROKE_WIDTH_PX,
            };

            for (Int32 Index = 0; Index < (Vertices.Count - 1); Index++)
            {
                ToolPolylineVertexRecord A = Vertices[Index];
                ToolPolylineVertexRecord B = Vertices[Index + 1];
                if (!MapProjection.LatLonToScreen(A.Latitude, A.Longitude, CentreLat, CentreLon, MetresPerPixel, MapViewport, out float Ax, out float Ay))
                    continue;
                if (!MapProjection.LatLonToScreen(B.Latitude, B.Longitude, CentreLat, CentreLon, MetresPerPixel, MapViewport, out float Bx, out float By))
                    continue;

                SKPaint Paint = HighlightSegments.Contains(Index) ? PreviewLinePaint : LinePaint;
                DrawToolPolylineSegment(Canvas, Ax, Ay, Bx, By, Paint);
            }
        }

        /// <summary>
        /// Draws one tool polyline in 3D including vertices and segments.
        /// </summary>
        static private void DrawPolyline3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            IReadOnlyList<ToolPolylineVertexRecord> Vertices,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            bool UseDisplaySnapping,
            SKPaint LinePaint,
            SKPaint PointPaint,
            ToolErasePreview? Preview,
            string PolylineId
            )
        {
            if (Vertices.Count == 0)
                return;

            List<(float X, float Y)> ScreenPoints = new List<(float X, float Y)>();
            foreach (ToolPolylineVertexRecord Vertex in Vertices)
            {
                double DisplayElevation = ResolveDisplayElevation(Surface, ContentView, Vertex, UseDisplaySnapping);
                Camera.Project(
                    Vertex.Latitude,
                    Vertex.Longitude,
                    DisplayElevation,
                    Viewport,
                    FlatPlane: false,
                    out float X,
                    out float Y,
                    out _);
                ScreenPoints.Add((X, Y));
                DrawToolPointMarker(Canvas, X, Y, PointPaint);
            }

            if (ScreenPoints.Count < 2)
                return;

            HashSet<int> HighlightSegments = BuildHighlightSegmentSet(Preview, PolylineId);
            using SKPaint PreviewLinePaint = new SKPaint
            {
                Color = ErasePreviewPointColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = ToolDataConstants.TOOL_POLYLINE_STROKE_WIDTH_PX,
            };

            for (Int32 Index = 0; Index < (ScreenPoints.Count - 1); Index++)
            {
                (float Ax, float Ay) = ScreenPoints[Index];
                (float Bx, float By) = ScreenPoints[Index + 1];
                SKPaint Paint = HighlightSegments.Contains(Index) ? PreviewLinePaint : LinePaint;
                DrawToolPolylineSegment(Canvas, Ax, Ay, Bx, By, Paint);
            }
        }

        /// <summary>
        /// Draws one tool point in 3D at the resolved display elevation.
        /// </summary>
        static private void DrawPoint3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            ToolPointRecord Point,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            bool UseDisplaySnapping,
            SKPaint Paint
            )
        {
            double DisplayElevation = ResolveDisplayElevation(
                Surface,
                ContentView,
                Point.Latitude,
                Point.Longitude,
                Point.ElevationM,
                UseDisplaySnapping);
            Camera.Project(
                Point.Latitude,
                Point.Longitude,
                DisplayElevation,
                Viewport,
                FlatPlane: false,
                out float X,
                out float Y,
                out _);
            DrawToolPointMarker(Canvas, X, Y, Paint);
        }

        /// <summary>
        /// Resolves display elevation for a polyline vertex (TOOLS-SPEC §4.5).
        /// </summary>
        static private double ResolveDisplayElevation
            (
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            ToolPolylineVertexRecord Vertex,
            bool UseDisplaySnapping
            )
        {
            return ResolveDisplayElevation(
                Surface,
                ContentView,
                Vertex.Latitude,
                Vertex.Longitude,
                Vertex.ElevationM,
                UseDisplaySnapping);
        }

        /// <summary>
        /// Resolves display elevation for a geographic location (TOOLS-SPEC §4.5).
        /// </summary>
        static private double ResolveDisplayElevation
            (
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            double Latitude,
            double Longitude,
            double StoredElevationM,
            bool UseDisplaySnapping
            )
        {
            if (UseDisplaySnapping)
                return Surface.SampleScalar(ContentView, Latitude, Longitude);

            return StoredElevationM;
        }

        /// <summary>
        /// Builds segment indices highlighted for erase preview on one polyline.
        /// </summary>
        static private HashSet<int> BuildHighlightSegmentSet
            (
            ToolErasePreview? Preview,
            string PolylineId
            )
        {
            HashSet<int> Segments = new HashSet<int>();
            if (Preview == null)
                return Segments;

            foreach (ToolPolylineSegmentRef Segment in Preview.Segments)
            {
                if (string.Equals(Segment.PolylineId, PolylineId, StringComparison.Ordinal))
                    Segments.Add(Segment.SegmentIndex);
            }

            return Segments;
        }
    }
}
