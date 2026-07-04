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
        /// True when the active tool creates new tool points or polyline vertices.
        /// </summary>
        /// <returns>True for Add Point and Add Polyline.</returns>
        private bool IsAddMapToolActive
            (
            )
        {
            return (ActiveMapTool == MapEditingToolKind.AddPoint) ||
                (ActiveMapTool == MapEditingToolKind.AddPolyline);
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

            InvalidateVisual();
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
            if (!HasToolData && !_IsEraseDragging && (_ErasePreview == null))
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
            if (HasToolData)
            {
                ToolDataRenderer.Draw3D(
                    OverlayCanvas,
                    Viewport,
                    ToolDataSession,
                    _Surface,
                    ContentView,
                    Camera,
                    UseDisplaySnapping,
                    _ErasePreview);
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
            (int Width, int Height) = GetRenderDimensions();
            if ((Width <= 0) || (Height <= 0))
                return false;

            EnsureSurfaceBuiltForFit();

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
            (int Width, int Height) = GetRenderDimensions();
            if ((Width <= 0) || (Height <= 0))
                return false;

            SKRect Viewport = SKRect.Create(0, 0, Width, Height);
            ResolveCameraState(
                Viewport,
                out double HeadingDeg,
                out double PitchDeg,
                out double DistanceM,
                out double OrbitLat,
                out double OrbitLon);

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
                ScreenX,
                ScreenY,
                OrbitLat,
                OrbitLon,
                MetresPerPixel,
                MapViewport,
                out Lat,
                out Lon);
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
