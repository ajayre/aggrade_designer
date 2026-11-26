using Avalonia;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using SkiaSharp;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.MapTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Rendering;

namespace AgGradeDesigner.Controls
{
    /// <summary>
    /// Map-editing tool pointer handling and overlay state for <see cref="FieldViewerControl"/>.
    /// </summary>
    public partial class FieldViewerControl
    {
        /// <summary>Styled property backing <see cref="ToolDataSession"/>.</summary>
        static public readonly StyledProperty<ToolDataSession?> ToolDataSessionProperty =
            AvaloniaProperty.Register<FieldViewerControl, ToolDataSession?>(nameof(ToolDataSession));

        /// <summary>Styled property backing <see cref="ActiveMapTool"/>.</summary>
        static public readonly StyledProperty<MapEditingToolKind> ActiveMapToolProperty =
            AvaloniaProperty.Register<FieldViewerControl, MapEditingToolKind>(nameof(ActiveMapTool), MapEditingToolKind.None);

        /// <summary>True while the user is dragging an erase rectangle.</summary>
        private bool _IsEraseDragging;

        /// <summary>True when a map tool was active at pointer-down and may consume a click.</summary>
        private bool _MapToolPointerArmed;

        /// <summary>True when pointer movement chose pan/orbit instead of a map-tool click.</summary>
        private bool _MapToolNavigationGesture;

        /// <summary>Pointer-down position for distinguishing click from pan.</summary>
        private Point _MapToolPressPoint;

        /// <summary>True when pointer movement exceeded the click threshold.</summary>
        private bool _MapToolDragExceededThreshold;

        /// <summary>Screen X where the erase rectangle drag started.</summary>
        private float _EraseDragStartX;

        /// <summary>Screen Y where the erase rectangle drag started.</summary>
        private float _EraseDragStartY;

        /// <summary>Active erase preview updated during rectangle drag.</summary>
        private ToolErasePreview? _ErasePreview;

        /// <summary>True when the pointer lies near the first in-progress polygon vertex.</summary>
        private bool _PolygonNearFirstVertex;

        /// <summary>True when a polygon preview pointer location is available.</summary>
        private bool _HasPolygonPreviewPointer;

        /// <summary>Preview pointer latitude for the in-progress polygon.</summary>
        private double _PolygonPreviewLatitude;

        /// <summary>Preview pointer longitude for the in-progress polygon.</summary>
        private double _PolygonPreviewLongitude;

        /// <summary>Timestamp of the previous polygon click release used for double-click detection.</summary>
        private DateTime _PolygonLastClickUtc = DateTime.MinValue;

        /// <summary>Screen X of the previous polygon click release.</summary>
        private float _PolygonLastClickX;

        /// <summary>Screen Y of the previous polygon click release.</summary>
        private float _PolygonLastClickY;

        /// <summary>Session tool data rendered in the decorations pass.</summary>
        public ToolDataSession? ToolDataSession
        {
            get => GetValue(ToolDataSessionProperty);
            set => SetValue(ToolDataSessionProperty, value);
        }

        /// <summary>Active map-editing tool determining pointer interpretation.</summary>
        public MapEditingToolKind ActiveMapTool
        {
            get => GetValue(ActiveMapToolProperty);
            set => SetValue(ActiveMapToolProperty, value);
        }

        /// <summary>Requests deactivation of the active map tool in the view model.</summary>
        public Action? RequestDeactivateMapTool { get; set; }

        /// <summary>
        /// Arms map-tool click detection without blocking viewport pan or orbit drags.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        /// <returns>True when a map tool may consume a subsequent click.</returns>
        private bool TryArmMapToolPointer
            (
            Point Position
            )
        {
            if ((ActiveMapTool == MapEditingToolKind.None) || (ToolDataSession == null) || (Project == null) || !Project.HasDesign)
                return false;

            if (IsAddMapToolActive() && (ViewMode != ViewMode.TwoD))
                return false;

            ResetMapToolPointerGestureState();
            _MapToolPointerArmed = true;
            _MapToolPressPoint = Position;
            return true;
        }

        /// <summary>
        /// Resets in-progress map-tool pointer gesture state.
        /// </summary>
        private void ResetMapToolPointerGestureState
            (
            )
        {
            _MapToolPointerArmed = false;
            _MapToolNavigationGesture = false;
            _MapToolDragExceededThreshold = false;
        }

        /// <summary>
        /// True when the active tool creates new tool points, polyline vertices, or polygon vertices.
        /// </summary>
        /// <returns>True for Add Point, Add Polyline, and Polygon.</returns>
        private bool IsAddMapToolActive
            (
            )
        {
            return (ActiveMapTool == MapEditingToolKind.AddPoint) ||
                (ActiveMapTool == MapEditingToolKind.AddPolyline) ||
                (ActiveMapTool == MapEditingToolKind.Polygon);
        }

        /// <summary>
        /// Updates click-vs-drag classification and starts erase when the user drags in 2D.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        /// <param name="BlockNavigation">When true, pan/orbit must not run for this move.</param>
        /// <returns>True when the caller should stop processing the pointer move.</returns>
        private bool TryAdvanceMapToolPointerGesture
            (
            Point Position,
            out bool BlockNavigation
            )
        {
            BlockNavigation = false;
            if (!_MapToolPointerArmed && !_IsEraseDragging)
                return false;

            if (_IsEraseDragging)
            {
                UpdateErasePreview((float)Position.X, (float)Position.Y);
                InvalidateVisual();
                BlockNavigation = true;
                return true;
            }

            double Dx = Position.X - _MapToolPressPoint.X;
            double Dy = Position.Y - _MapToolPressPoint.Y;
            if ((Math.Abs(Dx) <= ToolDataConstants.MAP_CLICK_DRAG_THRESHOLD_PX) &&
                (Math.Abs(Dy) <= ToolDataConstants.MAP_CLICK_DRAG_THRESHOLD_PX))
            {
                // intent: defer pan/orbit until we know whether this is a click or a drag
                BlockNavigation = true;
                return false;
            }

            if (!_MapToolDragExceededThreshold)
            {
                _MapToolDragExceededThreshold = true;

                if (IsAddMapToolActive())
                {
                    _MapToolNavigationGesture = true;
                    return false;
                }

                if (ActiveMapTool == MapEditingToolKind.Erase)
                {
                    if (ViewMode == ViewMode.TwoD)
                    {
                        _IsEraseDragging = true;
                        _EraseDragStartX = (float)_MapToolPressPoint.X;
                        _EraseDragStartY = (float)_MapToolPressPoint.Y;
                        UpdateErasePreview((float)Position.X, (float)Position.Y);
                        InvalidateVisual();
                        BlockNavigation = true;
                        return true;
                    }

                    _MapToolNavigationGesture = true;
                    return false;
                }
            }

            if (_MapToolNavigationGesture)
                return false;

            return false;
        }

        /// <summary>
        /// Handles pointer release for map-editing tools.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        /// <returns>True when the map tool consumed the release event.</returns>
        private bool TryCompleteMapToolPointer
            (
            Point Position
            )
        {
            if (!_MapToolPointerArmed && !_IsEraseDragging)
                return false;

            if (_IsEraseDragging)
            {
                CompleteEraseDrag((float)Position.X, (float)Position.Y);
                return true;
            }

            if (_MapToolPointerArmed &&
                !_MapToolDragExceededThreshold &&
                IsAddMapToolActive() &&
                (ViewMode == ViewMode.TwoD))
            {
                TryAddToolFeatureAtScreen((float)Position.X, (float)Position.Y);
                ResetMapToolPointerGestureState();
                return true;
            }

            ResetMapToolPointerGestureState();
            return false;
        }

        /// <summary>
        /// Cancels an in-progress erase drag and deactivates Erase when Escape is pressed.
        /// </summary>
        /// <returns>True when an erase drag was cancelled.</returns>
        private bool TryCancelEraseDragOnEscape
            (
            )
        {
            if (!_IsEraseDragging)
                return false;

            CancelEraseDrag();
            RequestDeactivateMapTool?.Invoke();
            return true;
        }

        /// <summary>
        /// Handles Escape for the active map-editing tool.
        /// </summary>
        /// <returns>True when Escape was handled.</returns>
        public bool TryHandleMapToolEscape
            (
            )
        {
            if (TryCancelEraseDragOnEscape())
                return true;

            if (ActiveMapTool == MapEditingToolKind.None)
                return false;

            RequestDeactivateMapTool?.Invoke();
            return true;
        }

        /// <summary>
        /// Clears static tool overlay slots before a cached frame build so tool data is never baked in.
        /// </summary>
        /// <param name="Snapshot">Frozen render inputs captured on the UI thread.</param>
        private void ApplyMapToolRenderOverlayFromSnapshot
            (
            RenderSnapshot Snapshot
            )
        {
            FieldRenderer.ToolDataOverlay = null;
            FieldRenderer.ToolErasePreviewOverlay = null;
        }

        /// <summary>
        /// Clears static tool overlay slots after frame build (tool data is drawn as a live overlay).
        /// </summary>
        private void ClearMapToolRenderOverlay
            (
            )
        {
            FieldRenderer.ToolDataOverlay = null;
            FieldRenderer.ToolErasePreviewOverlay = null;
        }

        /// <summary>
        /// Adds one tool point or polyline vertex at the clicked screen location.
        /// </summary>
        /// <param name="ScreenX">Horizontal screen coordinate in pixels.</param>
        /// <param name="ScreenY">Vertical screen coordinate in pixels.</param>
        private void TryAddToolFeatureAtScreen
            (
            float ScreenX,
            float ScreenY
            )
        {
            if ((ViewMode != ViewMode.TwoD) || (ToolDataSession == null) || (Project == null) || !Project.HasDesign)
                return;

            EnsureSurfaceBuiltForFit();

            if (!TryPickLatLon(ScreenX, ScreenY, out double Lat, out double Lon))
                return;

            if (!_Surface.IsInsideValidRegion(Lat, Lon))
                return;

            double ElevationM = _Surface.SampleProposed(Lat, Lon);

            if (ActiveMapTool == MapEditingToolKind.AddPoint)
                ToolDataSession.AddPoint(Lat, Lon, ElevationM);
            else if (ActiveMapTool == MapEditingToolKind.AddPolyline)
                ToolDataSession.AppendPolylineVertex(Lat, Lon, ElevationM);
            else if (ActiveMapTool == MapEditingToolKind.Polygon)
                TryAddOrClosePolygonVertex(Lat, Lon, ElevationM, ScreenX, ScreenY);

            InvalidateVisual();
        }

        /// <summary>
        /// Appends one polygon vertex or closes the ring when near the first vertex or on double-click.
        /// </summary>
        /// <param name="Latitude">Click latitude in decimal degrees.</param>
        /// <param name="Longitude">Click longitude in decimal degrees.</param>
        /// <param name="ElevationM">Proposed surface elevation in metres at the click location.</param>
        /// <param name="ScreenX">Click horizontal screen coordinate in pixels.</param>
        /// <param name="ScreenY">Click vertical screen coordinate in pixels.</param>
        private void TryAddOrClosePolygonVertex
            (
            double Latitude,
            double Longitude,
            double ElevationM,
            float ScreenX,
            float ScreenY
            )
        {
            if (ToolDataSession == null)
                return;

            if (TryClosePolygonNearFirstVertex(ScreenX, ScreenY))
                return;

            bool DoubleClick = IsPolygonDoubleClick(ScreenX, ScreenY);
            ToolDataSession.AppendPolygonVertex(Latitude, Longitude, ElevationM);
            if (DoubleClick)
            {
                ToolDataSession.TryCloseCurrentPolygon();
                ClearPolygonPreviewState();
            }
        }

        /// <summary>
        /// Closes the current polygon when the click lies near the first vertex in screen space (§3.4.12).
        /// </summary>
        /// <param name="ScreenX">Click horizontal screen coordinate in pixels.</param>
        /// <param name="ScreenY">Click vertical screen coordinate in pixels.</param>
        /// <returns>True when a close was attempted at the first vertex.</returns>
        private bool TryClosePolygonNearFirstVertex
            (
            float ScreenX,
            float ScreenY
            )
        {
            if (ToolDataSession == null)
                return false;

            IReadOnlyList<ToolPolylineVertexRecord> Vertices = ToolDataSession.CurrentPolygonVertices;
            if (Vertices.Count < GeoConstants.MIN_POLYGON_VERTICES)
                return false;

            if (!ProjectToolPointToScreen(Vertices[0].Latitude, Vertices[0].Longitude, out float VertexX, out float VertexY))
                return false;

            if (!ToolPolygonGeometry.IsNearVertexScreen(ScreenX, ScreenY, VertexX, VertexY))
                return false;

            ToolDataSession.TryCloseCurrentPolygon();
            ClearPolygonPreviewState();
            return true;
        }

        /// <summary>
        /// Returns whether the current click release completes a double-click gesture for Polygon.
        /// </summary>
        /// <param name="ScreenX">Click horizontal screen coordinate in pixels.</param>
        /// <param name="ScreenY">Click vertical screen coordinate in pixels.</param>
        /// <returns>True when this release is the second click of a double-click.</returns>
        private bool IsPolygonDoubleClick
            (
            float ScreenX,
            float ScreenY
            )
        {
            DateTime Now = DateTime.UtcNow;
            double ElapsedMs = (_PolygonLastClickUtc == DateTime.MinValue)
                ? double.MaxValue
                : (Now - _PolygonLastClickUtc).TotalMilliseconds;
            float DeltaX = ScreenX - _PolygonLastClickX;
            float DeltaY = ScreenY - _PolygonLastClickY;
            float ThresholdPx = (float)ToolDataConstants.MAP_CLICK_DRAG_THRESHOLD_PX;
            float ThresholdSq = ThresholdPx * ThresholdPx;
            float DistanceSq = (DeltaX * DeltaX) + (DeltaY * DeltaY);

            bool IsDoubleClick = (_PolygonLastClickUtc != DateTime.MinValue) &&
                (ElapsedMs <= ToolDataConstants.POLYGON_DOUBLE_CLICK_INTERVAL_MS) &&
                (DistanceSq <= ThresholdSq);

            _PolygonLastClickUtc = Now;
            _PolygonLastClickX = ScreenX;
            _PolygonLastClickY = ScreenY;
            return IsDoubleClick;
        }

        /// <summary>
        /// Updates in-progress polygon pointer preview and close-proximity highlight state.
        /// </summary>
        /// <param name="ScreenX">Pointer X in viewport pixels.</param>
        /// <param name="ScreenY">Pointer Y in viewport pixels.</param>
        private void UpdatePolygonPointerPreview
            (
            float ScreenX,
            float ScreenY
            )
        {
            if ((ActiveMapTool != MapEditingToolKind.Polygon) ||
                (ViewMode != ViewMode.TwoD) ||
                (ToolDataSession == null) ||
                (Project == null) ||
                !Project.HasDesign)
            {
                ClearPolygonPreviewState();
                return;
            }

            if (!TryPickLatLon(ScreenX, ScreenY, out double Lat, out double Lon))
            {
                ClearPolygonPreviewState();
                return;
            }

            _HasPolygonPreviewPointer = true;
            _PolygonPreviewLatitude = Lat;
            _PolygonPreviewLongitude = Lon;

            IReadOnlyList<ToolPolylineVertexRecord> Vertices = ToolDataSession.CurrentPolygonVertices;
            if ((Vertices.Count >= GeoConstants.MIN_POLYGON_VERTICES) &&
                ProjectToolPointToScreen(Vertices[0].Latitude, Vertices[0].Longitude, out float VertexX, out float VertexY))
            {
                _PolygonNearFirstVertex = ToolPolygonGeometry.IsNearVertexScreen(ScreenX, ScreenY, VertexX, VertexY);
            }
            else
            {
                _PolygonNearFirstVertex = false;
            }

            InvalidateVisual();
        }

        /// <summary>
        /// Clears in-progress polygon pointer preview state.
        /// </summary>
        private void ClearPolygonPreviewState
            (
            )
        {
            if (!_HasPolygonPreviewPointer && !_PolygonNearFirstVertex)
                return;

            _HasPolygonPreviewPointer = false;
            _PolygonNearFirstVertex = false;
            _PolygonLastClickUtc = DateTime.MinValue;
            InvalidateVisual();
        }

        /// <summary>
        /// Builds optional polygon overlay state for the tool data renderer.
        /// </summary>
        /// <returns>Overlay state when Polygon is active; otherwise null.</returns>
        private ToolPolygonOverlayState? BuildPolygonOverlayState
            (
            )
        {
            if (ActiveMapTool != MapEditingToolKind.Polygon)
                return null;

            return new ToolPolygonOverlayState
            {
                HasPreviewPointer = _HasPolygonPreviewPointer,
                PreviewLatitude = _PolygonPreviewLatitude,
                PreviewLongitude = _PolygonPreviewLongitude,
                HighlightFirstVertexForClose = _PolygonNearFirstVertex,
            };
        }

        /// <summary>
        /// Updates erase preview state for the current drag position.
        /// </summary>
        /// <param name="CurrentX">Current pointer X in pixels.</param>
        /// <param name="CurrentY">Current pointer Y in pixels.</param>
        private void UpdateErasePreview
            (
            float CurrentX,
            float CurrentY
            )
        {
            if (ToolDataSession == null)
                return;

            EnsureSurfaceBuiltForFit();

            ToolScreenRect Rect = ToolScreenRect.FromDrag(_EraseDragStartX, _EraseDragStartY, CurrentX, CurrentY);
            EraseSelectionMode Mode = ToolScreenRect.ResolveSelectionMode(_EraseDragStartX, _EraseDragStartY, CurrentX, CurrentY);
            _ErasePreview = ToolDataEraser.BuildPreview(ToolDataSession, Rect, Mode, ProjectToolPointToScreen);
        }

        /// <summary>
        /// Applies erase deletion when the user releases the erase rectangle drag.
        /// </summary>
        /// <param name="EndX">Pointer-up X in pixels.</param>
        /// <param name="EndY">Pointer-up Y in pixels.</param>
        private void CompleteEraseDrag
            (
            float EndX,
            float EndY
            )
        {
            _IsEraseDragging = false;
            if (ToolDataSession == null)
            {
                CancelEraseDrag();
                return;
            }

            EnsureSurfaceBuiltForFit();

            ToolScreenRect Rect = ToolScreenRect.FromDrag(_EraseDragStartX, _EraseDragStartY, EndX, EndY);
            EraseSelectionMode Mode = ToolScreenRect.ResolveSelectionMode(_EraseDragStartX, _EraseDragStartY, EndX, EndY);
            ToolDataEraser.Apply(ToolDataSession, Rect, Mode, ProjectToolPointToScreen);
            CancelEraseDrag();
            InvalidateVisual();
        }

        /// <summary>
        /// Dismisses erase preview state without deleting tool data.
        /// </summary>
        private void CancelEraseDrag
            (
            )
        {
            _IsEraseDragging = false;
            _ErasePreview = null;
            ResetMapToolPointerGestureState();
            InvalidateVisual();
        }

        /// <summary>
        /// Cancels an in-progress erase drag when tool deactivation comes from outside the viewer.
        /// </summary>
        private void CancelEraseDragOnExternalDeactivate
            (
            )
        {
            if (!_IsEraseDragging && (_ErasePreview == null))
                return;

            CancelEraseDrag();
        }

        /// <summary>
        /// Draws session tool data and optional erase preview above cached field frames.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Width">Viewport width in pixels.</param>
        /// <param name="Height">Viewport height in pixels.</param>
        private void DrawMapToolOverlay
            (
            DrawingContext Context,
            int Width,
            int Height
            )
        {
            bool HasToolData = (ToolDataSession != null) && ToolDataSession.HasData;
            bool HasPolygonOverlay = (ActiveMapTool == MapEditingToolKind.Polygon) &&
                ((ToolDataSession?.CurrentPolygonVertices.Count ?? 0) > 0 || _HasPolygonPreviewPointer);
            if (!HasToolData && !HasPolygonOverlay && !_IsEraseDragging && (_ErasePreview == null))
                return;

            if ((Project == null) || !Project.HasSurvey)
                return;

            EnsureSurfaceBuiltForFit();

            using SKBitmap OverlayBitmap = new SKBitmap(Width, Height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using SKCanvas OverlayCanvas = new SKCanvas(OverlayBitmap);
            OverlayCanvas.Clear(SKColors.Transparent);

            SKRect Viewport = SKRect.Create(0, 0, Width, Height);
            FieldContentView ContentView = ResolveContentView();
            RenderSnapshot? OverlayCameraSource = TryResolveMapToolOverlayCameraSource();
            ResolveCameraState(
                Viewport,
                out double HeadingDeg,
                out double PitchDeg,
                out double DistanceM,
                out double OrbitLat,
                out double OrbitLon,
                OverlayCameraSource);

            FieldCamera3D Camera = new FieldCamera3D(
                _Surface,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                _Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                ContentView);

            bool UseDisplaySnapping = ToolDataRenderer.ShouldUseDisplaySnapping(ViewMode, ContentView);
            ToolPolygonOverlayState? PolygonOverlay = BuildPolygonOverlayState();
            if (HasToolData || HasPolygonOverlay)
            {
                ToolDataRenderer.Draw3D(
                    OverlayCanvas,
                    Viewport,
                    ToolDataSession,
                    _Surface,
                    ContentView,
                    Camera,
                    UseDisplaySnapping,
                    _ErasePreview,
                    PolygonOverlay);
            }

            if (_ErasePreview != null)
                ToolDataRenderer.DrawEraseOverlay(OverlayCanvas, _ErasePreview);

            OverlayCanvas.Flush();
            WriteableBitmap AvaloniaOverlay = CopySkiaToWriteableBitmap(OverlayBitmap, Width, Height);
            Context.DrawImage(AvaloniaOverlay, new Rect(0, 0, Width, Height));
        }

        /// <summary>
        /// Projects a geographic location to screen coordinates for erase hit testing.
        /// </summary>
        /// <param name="Latitude">Point latitude in decimal degrees.</param>
        /// <param name="Longitude">Point longitude in decimal degrees.</param>
        /// <param name="ScreenX">Output screen X in pixels.</param>
        /// <param name="ScreenY">Output screen Y in pixels.</param>
        /// <returns>True when projection succeeds.</returns>
        private bool ProjectToolPointToScreen
            (
            double Latitude,
            double Longitude,
            out float ScreenX,
            out float ScreenY
            )
        {
            ScreenX = 0f;
            ScreenY = 0f;
            bool Fitted = IsPresentedFrameFittedForCurrentViewport();
            int Width;
            int Height;
            if (Fitted)
            {
                // Project into the frame that is actually on screen, then map into the letterboxed rect.
                Width = (int)_PresentedFrameSourceSize.Width;
                Height = (int)_PresentedFrameSourceSize.Height;
            }
            else
            {
                (Width, Height) = GetRenderDimensions();
            }

            if ((Width <= 0) || (Height <= 0))
                return false;

            EnsureSurfaceBuiltForFit();

            SKRect Viewport = SKRect.Create(0, 0, Width, Height);
            FieldContentView ContentView = ResolveContentView();
            RenderSnapshot? OverlayCameraSource = Fitted
                ? _PresentedFrameCameraSnapshot
                : TryResolveMapToolOverlayCameraSource();
            ResolveCameraState(
                Viewport,
                out double HeadingDeg,
                out double PitchDeg,
                out double DistanceM,
                out double OrbitLat,
                out double OrbitLon,
                OverlayCameraSource);

            FieldCamera3D Camera = new FieldCamera3D(
                _Surface,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                _Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                ContentView);

            double DisplayElevation = ResolveToolFeatureDisplayElevation(Latitude, Longitude);

            Camera.Project(
                Latitude,
                Longitude,
                DisplayElevation,
                Viewport,
                FlatPlane: false,
                out ScreenX,
                out ScreenY,
                out _);
            if (Fitted)
                MapPresentedFramePointToViewport(ScreenX, ScreenY, out ScreenX, out ScreenY);

            return true;
        }

        /// <summary>
        /// Resolves the elevation used to project one tool feature for display and erase hit testing.
        /// </summary>
        /// <param name="Latitude">Feature latitude in decimal degrees.</param>
        /// <param name="Longitude">Feature longitude in decimal degrees.</param>
        /// <returns>Display elevation in metres.</returns>
        private double ResolveToolFeatureDisplayElevation
            (
            double Latitude,
            double Longitude
            )
        {
            FieldContentView ContentView = ResolveContentView();
            if (ToolDataRenderer.ShouldUseDisplaySnapping(ViewMode, ContentView))
                return _Surface.SampleScalar(ContentView, Latitude, Longitude);

            if (ToolDataSession != null)
            {
                foreach (ToolPointRecord Point in ToolDataSession.Points)
                {
                    if ((Point.Latitude == Latitude) && (Point.Longitude == Longitude))
                        return Point.ElevationM;
                }

                foreach (ToolPolylineRecord Polyline in ToolDataSession.Polylines)
                {
                    foreach (ToolPolylineVertexRecord Vertex in Polyline.Vertices)
                    {
                        if ((Vertex.Latitude == Latitude) && (Vertex.Longitude == Longitude))
                            return Vertex.ElevationM;
                    }
                }

                foreach (ToolPolylineVertexRecord Vertex in ToolDataSession.CurrentPolylineVertices)
                {
                    if ((Vertex.Latitude == Latitude) && (Vertex.Longitude == Longitude))
                        return Vertex.ElevationM;
                }

                foreach (ToolPolygonRecord Polygon in ToolDataSession.Polygons)
                {
                    foreach (ToolPolylineVertexRecord Vertex in Polygon.Vertices)
                    {
                        if ((Vertex.Latitude == Latitude) && (Vertex.Longitude == Longitude))
                            return Vertex.ElevationM;
                    }
                }

                foreach (ToolPolylineVertexRecord Vertex in ToolDataSession.CurrentPolygonVertices)
                {
                    if ((Vertex.Latitude == Latitude) && (Vertex.Longitude == Longitude))
                        return Vertex.ElevationM;
                }
            }

            return _Surface.SampleProposed(Latitude, Longitude);
        }

        /// <summary>
        /// Converts a screen click to geographic coordinates using the active camera model.
        /// </summary>
        /// <param name="ScreenX">Horizontal screen coordinate in pixels.</param>
        /// <param name="ScreenY">Vertical screen coordinate in pixels.</param>
        /// <param name="Lat">Output latitude in decimal degrees.</param>
        /// <param name="Lon">Output longitude in decimal degrees.</param>
        /// <returns>True when pick succeeds.</returns>
        private bool TryPickLatLon
            (
            float ScreenX,
            float ScreenY,
            out double Lat,
            out double Lon
            )
        {
            Lat = 0.0;
            Lon = 0.0;
            bool Fitted = IsPresentedFrameFittedForCurrentViewport();
            float PickX = ScreenX;
            float PickY = ScreenY;
            int Width;
            int Height;
            RenderSnapshot? CameraOverride = null;
            if (Fitted)
            {
                if (!TryMapViewportPointToPresentedFrame(ScreenX, ScreenY, out PickX, out PickY))
                    return false;

                Width = (int)_PresentedFrameSourceSize.Width;
                Height = (int)_PresentedFrameSourceSize.Height;
                CameraOverride = _PresentedFrameCameraSnapshot;
            }
            else
            {
                (Width, Height) = GetRenderDimensions();
            }

            if ((Width <= 0) || (Height <= 0))
                return false;

            SKRect Viewport = SKRect.Create(0, 0, Width, Height);
            ResolveCameraState(
                Viewport,
                out double HeadingDeg,
                out double PitchDeg,
                out double DistanceM,
                out double OrbitLat,
                out double OrbitLon,
                CameraOverride);

            FieldCamera3D Camera = new FieldCamera3D(
                _Surface,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                _Display.VerticalExaggeration,
                OrbitLat,
                OrbitLon,
                ResolveContentView());

            double MetresPerPixel = Camera.EstimateGroundMetresPerScreenPixel(Viewport);
            MapViewport MapViewport = new MapViewport { Width = Viewport.Width, Height = Viewport.Height };
            return MapProjection.ScreenToLatLon(
                PickX,
                PickY,
                OrbitLat,
                OrbitLon,
                MetresPerPixel,
                MapViewport,
                out Lat,
                out Lon);
        }

        /// <summary>True when <see cref="_RoadPickMetresPerPixel"/> matches the current view.</summary>
        private bool _HasRoadPickScale;

        /// <summary>Viewport width used for <see cref="_RoadPickMetresPerPixel"/>.</summary>
        private int _RoadPickWidth;

        /// <summary>Viewport height used for <see cref="_RoadPickMetresPerPixel"/>.</summary>
        private int _RoadPickHeight;

        /// <summary>Orbit heading used for <see cref="_RoadPickMetresPerPixel"/>, in degrees.</summary>
        private double _RoadPickHeadingDeg;

        /// <summary>Orbit pitch used for <see cref="_RoadPickMetresPerPixel"/>, in degrees.</summary>
        private double _RoadPickPitchDeg;

        /// <summary>Camera distance used for <see cref="_RoadPickMetresPerPixel"/>, in metres.</summary>
        private double _RoadPickDistanceM;

        /// <summary>Orbit latitude used for <see cref="_RoadPickMetresPerPixel"/>.</summary>
        private double _RoadPickOrbitLat;

        /// <summary>Orbit longitude used for <see cref="_RoadPickMetresPerPixel"/>.</summary>
        private double _RoadPickOrbitLon;

        /// <summary>Vertical exaggeration used for <see cref="_RoadPickMetresPerPixel"/>.</summary>
        private double _RoadPickExaggeration;

        /// <summary>Content view used for <see cref="_RoadPickMetresPerPixel"/>.</summary>
        private FieldContentView _RoadPickContent;

        /// <summary>Cached ground metres per pixel for the road pointer preview.</summary>
        private double _RoadPickMetresPerPixel;

        /// <summary>
        /// Picks a latitude and longitude for the road preview. The camera scale is reused until the view changes,
        /// so moving the pointer does not scan the elevation grid on every sample.
        /// </summary>
        /// <param name="ScreenX">Horizontal screen coordinate in pixels.</param>
        /// <param name="ScreenY">Vertical screen coordinate in pixels.</param>
        /// <param name="Lat">Output latitude in decimal degrees.</param>
        /// <param name="Lon">Output longitude in decimal degrees.</param>
        /// <returns>True when the pick succeeds.</returns>
        private bool TryPickRoadPreviewLatLon
            (
            float ScreenX,
            float ScreenY,
            out double Lat,
            out double Lon
            )
        {
            Lat = 0.0;
            Lon = 0.0;
            bool Fitted = IsPresentedFrameFittedForCurrentViewport();
            float PickX = ScreenX;
            float PickY = ScreenY;
            int Width;
            int Height;
            RenderSnapshot? CameraOverride = null;
            if (Fitted)
            {
                if (!TryMapViewportPointToPresentedFrame(ScreenX, ScreenY, out PickX, out PickY))
                    return false;

                Width = (int)_PresentedFrameSourceSize.Width;
                Height = (int)_PresentedFrameSourceSize.Height;
                CameraOverride = _PresentedFrameCameraSnapshot;
            }
            else
            {
                (Width, Height) = GetRenderDimensions();
            }

            if ((Width <= 0) || (Height <= 0))
                return false;

            if (!TryGetRoadPreviewMetresPerPixel(Width, Height, CameraOverride, out double MetresPerPixel, out double OrbitLat, out double OrbitLon))
                return false;

            MapViewport MapViewport = new MapViewport { Width = Width, Height = Height };
            return MapProjection.ScreenToLatLon(
                PickX,
                PickY,
                OrbitLat,
                OrbitLon,
                MetresPerPixel,
                MapViewport,
                out Lat,
                out Lon);
        }

        /// <summary>
        /// Returns the ground scale for the road preview, building the camera only when the view has changed.
        /// </summary>
        /// <param name="Width">Pick viewport width in pixels.</param>
        /// <param name="Height">Pick viewport height in pixels.</param>
        /// <param name="CameraOverride">Presented-frame camera when the picture is letterboxed.</param>
        /// <param name="MetresPerPixel">Ground metres per screen pixel.</param>
        /// <param name="OrbitLat">Orbit pivot latitude in decimal degrees.</param>
        /// <param name="OrbitLon">Orbit pivot longitude in decimal degrees.</param>
        /// <returns>True when a scale is available.</returns>
        private bool TryGetRoadPreviewMetresPerPixel
            (
            int Width,
            int Height,
            RenderSnapshot? CameraOverride,
            out double MetresPerPixel,
            out double OrbitLat,
            out double OrbitLon
            )
        {
            SKRect Viewport = SKRect.Create(0, 0, Width, Height);
            ResolveCameraState(
                Viewport,
                out double HeadingDeg,
                out double PitchDeg,
                out double DistanceM,
                out OrbitLat,
                out OrbitLon,
                CameraOverride);
            double Exaggeration = _Display.VerticalExaggeration;
            FieldContentView Content = ResolveContentView();

            // Same view as the last sample: skip the elevation scan inside the camera.
            if (_HasRoadPickScale &&
                (_RoadPickWidth == Width) &&
                (_RoadPickHeight == Height) &&
                (_RoadPickHeadingDeg == HeadingDeg) &&
                (_RoadPickPitchDeg == PitchDeg) &&
                (_RoadPickDistanceM == DistanceM) &&
                (_RoadPickOrbitLat == OrbitLat) &&
                (_RoadPickOrbitLon == OrbitLon) &&
                (_RoadPickExaggeration == Exaggeration) &&
                (_RoadPickContent == Content))
            {
                MetresPerPixel = _RoadPickMetresPerPixel;
                return true;
            }

            FieldCamera3D Camera = new FieldCamera3D(
                _Surface,
                HeadingDeg,
                PitchDeg,
                DistanceM,
                Exaggeration,
                OrbitLat,
                OrbitLon,
                Content);
            MetresPerPixel = Camera.EstimateGroundMetresPerScreenPixel(Viewport);
            _HasRoadPickScale = true;
            _RoadPickWidth = Width;
            _RoadPickHeight = Height;
            _RoadPickHeadingDeg = HeadingDeg;
            _RoadPickPitchDeg = PitchDeg;
            _RoadPickDistanceM = DistanceM;
            _RoadPickOrbitLat = OrbitLat;
            _RoadPickOrbitLon = OrbitLon;
            _RoadPickExaggeration = Exaggeration;
            _RoadPickContent = Content;
            _RoadPickMetresPerPixel = MetresPerPixel;
            return true;
        }

        /// <summary>
        /// Resolves orbit camera parameters for the active view mode.
        /// </summary>
        /// <param name="Viewport">Viewport rectangle in pixels.</param>
        /// <param name="HeadingDeg">Output orbit heading in degrees.</param>
        /// <param name="PitchDeg">Output orbit pitch in degrees.</param>
        /// <param name="DistanceM">Output camera distance in metres.</param>
        /// <param name="OrbitLat">Output orbit pivot latitude in degrees.</param>
        /// <param name="OrbitLon">Output orbit pivot longitude in degrees.</param>
        /// <param name="CameraOverride">When set, uses the presented-frame camera instead of live orbit state.</param>
        private void ResolveCameraState
            (
            SKRect Viewport,
            out double HeadingDeg,
            out double PitchDeg,
            out double DistanceM,
            out double OrbitLat,
            out double OrbitLon,
            RenderSnapshot? CameraOverride = null
            )
        {
            if (CameraOverride != null)
            {
                if (CameraOverride.ViewMode == ViewMode.TwoD)
                {
                    PlanViewCamera.ResolveOrbitParameters(
                        CameraOverride.CentreLatitude,
                        CameraOverride.CentreLongitude,
                        CameraOverride.ZoomFactor,
                        Viewport.Height,
                        out HeadingDeg,
                        out PitchDeg,
                        out DistanceM,
                        out OrbitLat,
                        out OrbitLon);
                    return;
                }

                HeadingDeg = CameraOverride.OrbitHeadingDeg;
                PitchDeg = CameraOverride.OrbitPitchDeg;
                DistanceM = CameraOverride.OrbitDistanceM;
                OrbitLat = CameraOverride.OrbitCentreLatitude;
                OrbitLon = CameraOverride.OrbitCentreLongitude;
                return;
            }

            if (ViewMode == ViewMode.TwoD)
            {
                PlanViewCamera.ResolveOrbitParameters(
                    CentreLatitude,
                    CentreLongitude,
                    ZoomFactor,
                    Viewport.Height,
                    out HeadingDeg,
                    out PitchDeg,
                    out DistanceM,
                    out OrbitLat,
                    out OrbitLon);
                return;
            }

            HeadingDeg = OrbitHeadingDeg;
            PitchDeg = OrbitPitchDeg;
            DistanceM = OrbitDistanceM;
            OrbitLat = ResolveOrbitCentreLatitude();
            OrbitLon = ResolveOrbitCentreLongitude();
        }

        /// <summary>
        /// Returns the presented-frame camera when the cached surface is stale relative to live orbit input.
        /// </summary>
        /// <returns>Presented camera snapshot, or null when overlay should track live camera.</returns>
        private RenderSnapshot? TryResolveMapToolOverlayCameraSource
            (
            )
        {
            (int Width, int Height) = GetRenderDimensions();
            if ((Width <= 0) || (Height <= 0))
                return null;

            RenderSnapshot LiveSnapshot = CaptureRenderSnapshot(ViewMode);
            string CurrentCacheKey = BuildFrameCacheKey(Width, Height, ViewMode, LiveSnapshot);
            string LastSuccessfulCacheKey = ViewMode == ViewMode.ThreeD
                ? _LastSuccessful3DCacheKey
                : _LastSuccessful2DCacheKey;
            if (string.IsNullOrEmpty(LastSuccessfulCacheKey) ||
                string.Equals(CurrentCacheKey, LastSuccessfulCacheKey, StringComparison.Ordinal))
            {
                return null;
            }

            return _PresentedFrameCameraSnapshot;
        }
    }
}
