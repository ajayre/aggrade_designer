using Avalonia;
using Avalonia.Media;

using AgGradeDesigner.Core.Features;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Controls
{
    /// <summary>
    /// Well and road placement on the field viewer. Clicks add points and polyline vertices
    /// the same way as the sidebar tools, but this code does not call those tools.
    /// </summary>
    public partial class FieldViewerControl
    {
        /// <summary>Pointer movement at or below this distance is a click, not a pan.</summary>
        private const double FEATURE_CLICK_DRAG_THRESHOLD_PX = 5.0;

        /// <summary>Screen distance that selects a vertex for dragging.</summary>
        private const double FEATURE_VERTEX_HIT_RADIUS_PX = 12.0;

        /// <summary>Radius of a committed or newly placed well marker.</summary>
        private const double FEATURE_WELL_RADIUS_PX = 6.0;

        /// <summary>The pad circle's radius is half its diameter.</summary>
        private const double PAD_RADIUS_OF_DIAMETER = 0.5;

        /// <summary>Radius of a centreline vertex that can be dragged.</summary>
        private const double FEATURE_HANDLE_RADIUS_PX = 7.0;

        /// <summary>Fill for a well or road vertex that has been saved.</summary>
        private static readonly IBrush FeatureFillBrush = new SolidColorBrush(Color.FromRgb(61, 181, 74));

        /// <summary>Fill for a well or road vertex while it is being created or edited.</summary>
        private static readonly IBrush FeatureEditFillBrush = new SolidColorBrush(Color.FromRgb(242, 148, 36));

        /// <summary>Stroke around wells and vertices.</summary>
        private static readonly IPen FeatureOutlinePen = new Pen(Brushes.White, 1.5);

        /// <summary>Centreline of a saved road.</summary>
        private static readonly IPen FeatureRoadPen = new Pen(FeatureFillBrush, 2.5);

        /// <summary>Centreline of a road that is being created or edited.</summary>
        private static readonly IPen FeatureEditRoadPen = new Pen(FeatureEditFillBrush, 2.5);

        /// <summary>Darker grey for the toe, the outer dashed pair.</summary>
        private const byte FEATURE_TOE_GREY = 96;

        /// <summary>Lighter grey for the road-bed edges, drawn inside the toes.</summary>
        private const byte FEATURE_BED_GREY = 176;

        /// <summary>Stroke width of the dashed guide lines.</summary>
        private const double FEATURE_GUIDE_THICKNESS = 1.5;

        /// <summary>Dash length of a guide line, in pixels.</summary>
        private const double FEATURE_GUIDE_DASH_ON = 6.0;

        /// <summary>Gap length of a guide line, in pixels.</summary>
        private const double FEATURE_GUIDE_DASH_OFF = 4.0;

        /// <summary>Dash pattern shared by the toe and the 2× blade-width lines.</summary>
        private static readonly double[] FeatureGuideDash = new double[] { FEATURE_GUIDE_DASH_ON, FEATURE_GUIDE_DASH_OFF };

        /// <summary>Dashed toe of a road, both while it is being drawn and after it is saved.</summary>
        private static readonly IPen FeatureToePen = new Pen(
            new SolidColorBrush(Color.FromRgb(FEATURE_TOE_GREY, FEATURE_TOE_GREY, FEATURE_TOE_GREY)),
            FEATURE_GUIDE_THICKNESS,
            new DashStyle(FeatureGuideDash, 0));

        /// <summary>Dashed edges of the road bed. Both sides move when the bed width changes.</summary>
        private static readonly IPen FeatureBedPen = new Pen(
            new SolidColorBrush(Color.FromRgb(FEATURE_BED_GREY, FEATURE_BED_GREY, FEATURE_BED_GREY)),
            FEATURE_GUIDE_THICKNESS,
            new DashStyle(FeatureGuideDash, 0));

        /// <summary>True while an add click is waiting to see whether the pointer pans instead.</summary>
        private bool _FeatureAddArmed;

        /// <summary>True when the add gesture moved far enough to pan instead of placing a point.</summary>
        private bool _FeatureAddExceeded;

        /// <summary>Pointer position where an add gesture started.</summary>
        private Point _FeaturePressPoint;

        /// <summary>True while a vertex of the feature being edited is being dragged.</summary>
        private bool _IsFeatureVertexDragging;

        /// <summary>Index of the vertex being dragged.</summary>
        private int _FeatureDragVertexIndex = -1;

        /// <summary>True when <see cref="_FeatureDragVertexIndex"/> refers to an unsaved well, not a road vertex.</summary>
        private bool _FeatureDragIsDraftWell;

        /// <summary>True while a proposed road segment is following the pointer.</summary>
        private bool _HasRoadPreview;

        /// <summary>Latitude of the proposed next road vertex, in decimal degrees.</summary>
        private double _RoadPreviewLatitude;

        /// <summary>Longitude of the proposed next road vertex, in decimal degrees.</summary>
        private double _RoadPreviewLongitude;

        /// <summary>Pointer position in control pixels while the first road point is being chosen.</summary>
        private Point _RoadPreviewScreen;

        /// <summary>True while a proposed well pad is following the pointer.</summary>
        private bool _HasWellPreview;

        /// <summary>Pointer position in control pixels while the next well pad is being chosen.</summary>
        private Point _WellPreviewScreen;

        /// <summary>Shared well and road edit session. Null until the main window assigns it.</summary>
        public FeatureEditSession? FeatureEditSession { get; set; }

        /// <summary>
        /// True while a feature edit should receive clicks instead of the sidebar map tools.
        /// </summary>
        /// <returns>True when an add or edit session is open.</returns>
        private bool IsFieldFeatureEditActive
            (
            )
        {
            return (FeatureEditSession != null) && FeatureEditSession.IsActive;
        }

        /// <summary>
        /// Arms a placement click, or starts a vertex drag when the press hits a point already placed.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        /// <returns>True when the press is a vertex drag and must not pan the map.</returns>
        private bool TryBeginFieldFeaturePress
            (
            Point Position
            )
        {
            _FeatureAddArmed = false;
            _FeatureAddExceeded = false;
            _IsFeatureVertexDragging = false;
            _FeatureDragVertexIndex = -1;
            _FeatureDragIsDraftWell = false;

            if (!CanUseFieldFeaturePointer())
                return false;

            FeatureEditSession Session = FeatureEditSession!;
            if ((Session.Mode == FeatureEditMode.EditWell) || (Session.Mode == FeatureEditMode.EditRoad))
            {
                if (!TryHitEditableVertex(Position, out int Index))
                    return false;

                // Dragging a vertex replaces pan for this gesture.
                BeginFeaturePointDrag(Index, DraftWell: false);
                return true;
            }

            if (Session.Mode == FeatureEditMode.AddWells)
            {
                // A press on a well already clicked moves that well. Empty ground still places or pans.
                if (TryHitDraftWell(Position, out int WellIndex))
                {
                    BeginFeaturePointDrag(WellIndex, DraftWell: true);
                    return true;
                }
            }
            else if (Session.Mode == FeatureEditMode.AddRoad)
            {
                if (TryHitEditableVertex(Position, out int VertexIndex))
                {
                    BeginFeaturePointDrag(VertexIndex, DraftWell: false);
                    return true;
                }
            }

            if ((Session.Mode == FeatureEditMode.AddWells) || (Session.Mode == FeatureEditMode.AddRoad))
            {
                _FeatureAddArmed = true;
                _FeaturePressPoint = Position;
            }

            return false;
        }

        /// <summary>
        /// Starts a drag that moves one placed point and does not pan the map.
        /// </summary>
        /// <param name="Index">Index of the well or road vertex.</param>
        /// <param name="DraftWell">True when <paramref name="Index"/> is an unsaved well.</param>
        private void BeginFeaturePointDrag
            (
            int Index,
            bool DraftWell
            )
        {
            _IsFeatureVertexDragging = true;
            _FeatureDragVertexIndex = Index;
            _FeatureDragIsDraftWell = DraftWell;
        }

        /// <summary>
        /// Keeps a short add press from panning, then lets a longer drag pan the map.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        /// <param name="BlockNavigation">True when the gesture is still a click and must not pan.</param>
        /// <returns>True when the caller should stop processing the move.</returns>
        private bool TryAdvanceFieldFeatureAddGesture
            (
            Point Position,
            out bool BlockNavigation
            )
        {
            BlockNavigation = false;
            if (!_FeatureAddArmed)
                return false;

            double Dx = Position.X - _FeaturePressPoint.X;
            double Dy = Position.Y - _FeaturePressPoint.Y;
            if ((Math.Abs(Dx) <= FEATURE_CLICK_DRAG_THRESHOLD_PX) &&
                (Math.Abs(Dy) <= FEATURE_CLICK_DRAG_THRESHOLD_PX))
            {
                // Same click-versus-pan rule as the point and polyline tools.
                BlockNavigation = true;
                return false;
            }

            _FeatureAddExceeded = true;
            return false;
        }

        /// <summary>
        /// Places a point on click, or finishes a vertex drag.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        /// <returns>True when the feature editor consumed the release.</returns>
        private bool TryCompleteFieldFeaturePointer
            (
            Point Position
            )
        {
            if (_IsFeatureVertexDragging)
            {
                MoveFieldFeatureVertex(Position);
                _IsFeatureVertexDragging = false;
                _FeatureDragVertexIndex = -1;
                _FeatureDragIsDraftWell = false;
                return true;
            }

            if (_FeatureAddArmed && !_FeatureAddExceeded)
            {
                PlaceFieldFeature(Position);
                _FeatureAddArmed = false;
                return true;
            }

            _FeatureAddArmed = false;
            return false;
        }

        /// <summary>
        /// Moves the dragged vertex to the pointer when it is inside the field.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        private void MoveFieldFeatureVertex
            (
            Point Position
            )
        {
            if ((FeatureEditSession == null) || (_FeatureDragVertexIndex < 0))
                return;

            if (!TryPickFeatureLatLon(Position, out double Latitude, out double Longitude))
                return;

            if (_FeatureDragIsDraftWell)
                FeatureEditSession.TryMoveDraftWell(_FeatureDragVertexIndex, Latitude, Longitude);
            else
                FeatureEditSession.TryMoveEditableVertex(_FeatureDragVertexIndex, Latitude, Longitude);

            InvalidateVisual();
        }

        /// <summary>
        /// Adds a well centre or a road vertex at the click.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        private void PlaceFieldFeature
            (
            Point Position
            )
        {
            if (FeatureEditSession == null)
                return;

            if (!TryPickFeatureLatLon(Position, out double Latitude, out double Longitude))
                return;

            if (FeatureEditSession.Mode == FeatureEditMode.AddWells)
                FeatureEditSession.AddWellAt(Latitude, Longitude);
            else if (FeatureEditSession.Mode == FeatureEditMode.AddRoad)
                FeatureEditSession.AppendRoadVertex(Latitude, Longitude);

            InvalidateVisual();
        }

        /// <summary>
        /// Moves the road preview to the pointer. Before the first click that is two circles.
        /// After the first click it is the next straight segment. Overlay only: the field picture is not rebuilt.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        private void UpdateRoadPointerPreview
            (
            Point Position
            )
        {
            FeatureEditSession? Session = FeatureEditSession;
            bool WantPreview = (Session != null) &&
                (Session.Mode == FeatureEditMode.AddRoad) &&
                !_IsFeatureVertexDragging &&
                CanUseFieldFeaturePointer();
            if (!WantPreview)
            {
                ClearRoadPreview();
                return;
            }

            EnsureSurfaceBuiltForFit();
            if (!TryPickRoadPreviewLatLon((float)Position.X, (float)Position.Y, out double Latitude, out double Longitude) ||
                !_Surface.IsInsideValidRegion(Latitude, Longitude))
            {
                ClearRoadPreview();
                return;
            }

            if (_HasRoadPreview &&
                (Latitude == _RoadPreviewLatitude) &&
                (Longitude == _RoadPreviewLongitude) &&
                (_RoadPreviewScreen == Position))
                return;

            _HasRoadPreview = true;
            _RoadPreviewLatitude = Latitude;
            _RoadPreviewLongitude = Longitude;
            _RoadPreviewScreen = Position;
            InvalidateVisual();
        }

        /// <summary>
        /// Drops the proposed segment when the pointer leaves the field or road placement stops.
        /// </summary>
        private void ClearRoadPreview
            (
            )
        {
            if (!_HasRoadPreview)
                return;

            _HasRoadPreview = false;
            InvalidateVisual();
        }

        /// <summary>
        /// Moves the next well pad circle to the pointer. Overlay only: the field picture is not rebuilt.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        private void UpdateWellPointerPreview
            (
            Point Position
            )
        {
            FeatureEditSession? Session = FeatureEditSession;
            bool WantPreview = (Session != null) &&
                (Session.Mode == FeatureEditMode.AddWells) &&
                !_IsFeatureVertexDragging &&
                CanUseFieldFeaturePointer();
            if (!WantPreview)
            {
                ClearWellPreview();
                return;
            }

            EnsureSurfaceBuiltForFit();
            if (!TryPickRoadPreviewLatLon((float)Position.X, (float)Position.Y, out double Latitude, out double Longitude) ||
                !_Surface.IsInsideValidRegion(Latitude, Longitude))
            {
                ClearWellPreview();
                return;
            }

            if (_HasWellPreview && (_WellPreviewScreen == Position))
                return;

            _HasWellPreview = true;
            _WellPreviewScreen = Position;
            InvalidateVisual();
        }

        /// <summary>
        /// Drops the proposed pad when the pointer leaves the field or well placement stops.
        /// </summary>
        private void ClearWellPreview
            (
            )
        {
            if (!_HasWellPreview)
                return;

            _HasWellPreview = false;
            InvalidateVisual();
        }

        /// <summary>
        /// Converts a screen point to a field location inside the rendered survey.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <returns>True when the point is inside the field in 2D.</returns>
        private bool TryPickFeatureLatLon
            (
            Point Position,
            out double Latitude,
            out double Longitude
            )
        {
            Latitude = 0.0;
            Longitude = 0.0;
            if (!CanUseFieldFeaturePointer() || (Project == null))
                return false;

            EnsureSurfaceBuiltForFit();
            if (!TryPickLatLon((float)Position.X, (float)Position.Y, out Latitude, out Longitude))
                return false;

            // Keep new and dragged points on the surveyed field.
            return _Surface.IsInsideValidRegion(Latitude, Longitude);
        }

        /// <summary>
        /// True when feature clicks are accepted: 2D, a survey is loaded, and an edit is open.
        /// </summary>
        /// <returns>True when the pointer may place or drag a feature.</returns>
        private bool CanUseFieldFeaturePointer
            (
            )
        {
            return IsFieldFeatureEditActive() &&
                (ViewMode == ViewMode.TwoD) &&
                (Project != null) &&
                Project.HasSurvey;
        }

        /// <summary>
        /// Finds the editable vertex closest to the pointer.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        /// <param name="Index">Index into the session's editable vertices.</param>
        /// <returns>True when a vertex is within the hit radius.</returns>
        private bool TryHitEditableVertex
            (
            Point Position,
            out int Index
            )
        {
            Index = -1;
            if ((FeatureEditSession == null) || (FeatureEditSession.EditableVertices.Count == 0))
                return false;

            double BestDistanceSq = FEATURE_VERTEX_HIT_RADIUS_PX * FEATURE_VERTEX_HIT_RADIUS_PX;
            IReadOnlyList<DraftVertex> Vertices = FeatureEditSession.EditableVertices;
            for (int VertexIndex = 0; VertexIndex < Vertices.Count; VertexIndex++)
            {
                if (!ProjectToolPointToScreen(Vertices[VertexIndex].Latitude, Vertices[VertexIndex].Longitude, out float ScreenX, out float ScreenY))
                    continue;

                double Dx = Position.X - ScreenX;
                double Dy = Position.Y - ScreenY;
                double DistanceSq = (Dx * Dx) + (Dy * Dy);
                if (DistanceSq > BestDistanceSq)
                    continue;

                BestDistanceSq = DistanceSq;
                Index = VertexIndex;
            }

            return Index >= 0;
        }

        /// <summary>
        /// Finds the unsaved well closest to the pointer.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        /// <param name="Index">Index into the session's draft wells.</param>
        /// <returns>True when a well is within the hit radius.</returns>
        private bool TryHitDraftWell
            (
            Point Position,
            out int Index
            )
        {
            Index = -1;
            if ((FeatureEditSession == null) || (FeatureEditSession.DraftWells.Count == 0))
                return false;

            double BestDistanceSq = FEATURE_VERTEX_HIT_RADIUS_PX * FEATURE_VERTEX_HIT_RADIUS_PX;
            IReadOnlyList<WellDraft> Wells = FeatureEditSession.DraftWells;
            for (int WellIndex = 0; WellIndex < Wells.Count; WellIndex++)
            {
                if (!ProjectToolPointToScreen(Wells[WellIndex].Latitude, Wells[WellIndex].Longitude, out float ScreenX, out float ScreenY))
                    continue;

                double Dx = Position.X - ScreenX;
                double Dy = Position.Y - ScreenY;
                double DistanceSq = (Dx * Dx) + (Dy * Dy);
                if (DistanceSq > BestDistanceSq)
                    continue;

                BestDistanceSq = DistanceSq;
                Index = WellIndex;
            }

            return Index >= 0;
        }

        /// <summary>
        /// Draws committed wells and roads, plus the unsaved draft, above the field.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Width">Viewport width in pixels.</param>
        /// <param name="Height">Viewport height in pixels.</param>
        private void DrawFieldFeatureOverlay
            (
            DrawingContext Context,
            int Width,
            int Height
            )
        {
            if ((Width <= 0) || (Height <= 0) || (Project == null) || !Project.HasSurvey)
                return;

            FieldFeatureSet Features = Project.Features;
            FeatureEditSession? Session = FeatureEditSession;
            bool HasDraft = (Session != null) && Session.HasDraftGeometry;
            bool PreviewingRoadStart = (Session != null) &&
                (Session.Mode == FeatureEditMode.AddRoad) &&
                (Session.EditableVertices.Count == 0) &&
                _HasRoadPreview;
            bool PreviewingWellPad = (Session != null) &&
                (Session.Mode == FeatureEditMode.AddWells) &&
                _HasWellPreview;
            if ((Features.Wells.Count == 0) && (Features.Roads.Count == 0) && !HasDraft && !PreviewingRoadStart && !PreviewingWellPad)
                return;

            string? EditingId = Session?.EditingFeatureId;
            FeatureEditMode Mode = Session?.Mode ?? FeatureEditMode.None;

            for (int Index = 0; Index < Features.Roads.Count; Index++)
            {
                RoadFeature Road = Features.Roads[Index];
                if ((Mode == FeatureEditMode.EditRoad) && string.Equals(Road.Id, EditingId, StringComparison.Ordinal))
                    continue;

                DrawFeaturePolyline(
                    Context,
                    Road.Vertices,
                    Road.BedWidthM,
                    Road.HeightM,
                    Road.SideSlope,
                    DrawHandles: false,
                    FeatureRoadPen,
                    FeatureFillBrush);
            }

            if ((Session != null) &&
                ((Mode == FeatureEditMode.AddRoad) || (Mode == FeatureEditMode.EditRoad)) &&
                (Session.EditableVertices.Count > 0))
            {
                // The pointer is not a placed vertex. It extends the corridor so the next segment can be seen.
                IReadOnlyList<DraftVertex> DrawnVertices = Session.EditableVertices;
                int MarkedVertexCount = Session.EditableVertices.Count;
                if ((Mode == FeatureEditMode.AddRoad) && _HasRoadPreview)
                {
                    List<DraftVertex> WithPreview = new List<DraftVertex>(MarkedVertexCount + 1);
                    WithPreview.AddRange(Session.EditableVertices);
                    WithPreview.Add(new DraftVertex(_RoadPreviewLatitude, _RoadPreviewLongitude));
                    DrawnVertices = WithPreview;
                }

                // Unsaved roads stay orange until Save writes them.
                DrawDraftPolyline(
                    Context,
                    DrawnVertices,
                    MarkedVertexCount,
                    Session.RoadBedWidthM,
                    Session.RoadHeightM,
                    Session.RoadSideSlope,
                    DrawHandles: Mode == FeatureEditMode.EditRoad,
                    FeatureEditRoadPen,
                    FeatureEditFillBrush);
            }

            for (int Index = 0; Index < Features.Wells.Count; Index++)
            {
                WellFeature Well = Features.Wells[Index];
                if ((Mode == FeatureEditMode.EditWell) && string.Equals(Well.Id, EditingId, StringComparison.Ordinal))
                    continue;

                DrawFeatureWell(Context, Well.Latitude, Well.Longitude, Well.PadDiameterM, FEATURE_WELL_RADIUS_PX, FeatureFillBrush);
            }

            if ((Session != null) && (Mode == FeatureEditMode.AddWells))
            {
                for (int Index = 0; Index < Session.DraftWells.Count; Index++)
                {
                    WellDraft Draft = Session.DraftWells[Index];
                    DrawFeatureWell(Context, Draft.Latitude, Draft.Longitude, Draft.PadDiameterM, FEATURE_WELL_RADIUS_PX, FeatureEditFillBrush);
                }
            }

            if ((Session != null) && (Mode == FeatureEditMode.EditWell) && (Session.EditableVertices.Count == 1))
            {
                DraftVertex Vertex = Session.EditableVertices[0];
                DrawFeatureWell(
                    Context,
                    Vertex.Latitude,
                    Vertex.Longitude,
                    Session.EditingWellPadDiameterM,
                    FEATURE_HANDLE_RADIUS_PX,
                    FeatureEditFillBrush);
            }

            // Circles only until the first click. The straight corridor takes over after that.
            if (PreviewingRoadStart && (Session != null))
                DrawRoadStartCircles(Context, Session);

            // The next pad follows the pointer until the click places the well.
            if (PreviewingWellPad && (Session != null))
                DrawDashedGroundCircle(
                    Context,
                    _WellPreviewScreen,
                    Session.EditingWellPadDiameterM * PAD_RADIUS_OF_DIAMETER,
                    FeatureBedPen);
        }

        /// <summary>
        /// Draws the 2× blade-width circle and the toe circle around the pointer before the first road point.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Session">Road being added. Its bed, height, and slope set the toe circle.</param>
        private void DrawRoadStartCircles
            (
            DrawingContext Context,
            FeatureEditSession Session
            )
        {
            if (Project == null)
                return;

            double BladeRadiusM = RoadCrossSection.BladePairOffsetM(Project.HaulSettings.BladeWidthMm);
            double ToeRadiusM = RoadCrossSection.ToeOffsetM(Session.RoadBedWidthM, Session.RoadHeightM, Session.RoadSideSlope);
            DrawDashedGroundCircle(Context, _RoadPreviewScreen, BladeRadiusM, FeatureBedPen);
            DrawDashedGroundCircle(Context, _RoadPreviewScreen, ToeRadiusM, FeatureToePen);
        }

        /// <summary>
        /// Strokes a circle whose radius is a ground distance, centred on a control point.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Centre">Circle centre in control pixels.</param>
        /// <param name="RadiusM">Radius in metres.</param>
        /// <param name="Pen">Dashed stroke.</param>
        private void DrawDashedGroundCircle
            (
            DrawingContext Context,
            Point Centre,
            double RadiusM,
            IPen Pen
            )
        {
            if (!TryGroundRadiusToScreen(RadiusM, out double RadiusPx))
                return;

            Context.DrawEllipse(null, Pen, Centre, RadiusPx, RadiusPx);
        }

        /// <summary>
        /// Converts a ground radius into control pixels at the current view scale, including letterboxing.
        /// </summary>
        /// <param name="RadiusM">Radius in metres.</param>
        /// <param name="RadiusPx">Radius in control pixels.</param>
        /// <returns>True when the radius is large enough to draw.</returns>
        private bool TryGroundRadiusToScreen
            (
            double RadiusM,
            out double RadiusPx
            )
        {
            RadiusPx = 0.0;
            if (RadiusM <= 0.0)
                return false;

            bool Fitted = IsPresentedFrameFittedForCurrentViewport();
            int Width;
            int Height;
            RenderSnapshot? CameraOverride = null;
            double LetterboxScale = 1.0;
            if (Fitted)
            {
                Width = (int)_PresentedFrameSourceSize.Width;
                Height = (int)_PresentedFrameSourceSize.Height;
                CameraOverride = _PresentedFrameCameraSnapshot;
                if (_PresentedFrameSourceSize.Width > 0.0)
                    LetterboxScale = _PresentedFrameDestRect.Width / _PresentedFrameSourceSize.Width;
            }
            else
            {
                (Width, Height) = GetRenderDimensions();
            }

            if ((Width <= 0) || (Height <= 0) || (LetterboxScale <= 0.0))
                return false;

            if (!TryGetRoadPreviewMetresPerPixel(Width, Height, CameraOverride, out double MetresPerPixel, out _, out _))
                return false;

            if (MetresPerPixel <= 0.0)
                return false;

            // The scale is in frame pixels. Letterboxing shows that frame smaller or larger on the control.
            RadiusPx = (RadiusM / MetresPerPixel) * LetterboxScale;
            return RadiusPx > 0.0;
        }

        /// <summary>
        /// Draws a committed centreline, with the bed edges and the toes beside it.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Vertices">Centreline vertices.</param>
        /// <param name="BedWidthM">Full road bed width in metres.</param>
        /// <param name="HeightM">Embankment height in metres.</param>
        /// <param name="SideSlope">Horizontal run per one vertical.</param>
        /// <param name="DrawHandles">True when each vertex gets a drag handle.</param>
        /// <param name="RoadPen">Stroke for the centreline.</param>
        /// <param name="PointBrush">Fill for vertex markers.</param>
        private void DrawFeaturePolyline
            (
            DrawingContext Context,
            IReadOnlyList<FeatureVertex> Vertices,
            double BedWidthM,
            double HeightM,
            double SideSlope,
            bool DrawHandles,
            IPen RoadPen,
            IBrush PointBrush
            )
        {
            DrawRoadGuides(Context, Vertices, BedWidthM, HeightM, SideSlope);
            Point? Previous = null;
            for (int Index = 0; Index < Vertices.Count; Index++)
            {
                if (!TryProjectFeature(Vertices[Index].Latitude, Vertices[Index].Longitude, out Point Screen))
                {
                    Previous = null;
                    continue;
                }

                if (Previous.HasValue)
                    Context.DrawLine(RoadPen, Previous.Value, Screen);

                if (DrawHandles)
                    DrawFeatureHandle(Context, Screen, PointBrush);

                Previous = Screen;
            }
        }

        /// <summary>
        /// Draws an unsaved or in-progress centreline, with the bed edges and the toes beside it.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Vertices">Draft vertices, including the pointer when a segment is proposed.</param>
        /// <param name="MarkedVertexCount">How many leading vertices are placed points. The pointer is not marked.</param>
        /// <param name="BedWidthM">Full road bed width in metres.</param>
        /// <param name="HeightM">Embankment height in metres.</param>
        /// <param name="SideSlope">Horizontal run per one vertical.</param>
        /// <param name="DrawHandles">True when each vertex gets a drag handle.</param>
        /// <param name="RoadPen">Stroke for the centreline.</param>
        /// <param name="PointBrush">Fill for vertex markers.</param>
        private void DrawDraftPolyline
            (
            DrawingContext Context,
            IReadOnlyList<DraftVertex> Vertices,
            int MarkedVertexCount,
            double BedWidthM,
            double HeightM,
            double SideSlope,
            bool DrawHandles,
            IPen RoadPen,
            IBrush PointBrush
            )
        {
            DrawRoadGuides(Context, DraftCentreline(Vertices), BedWidthM, HeightM, SideSlope);
            Point? Previous = null;
            for (int Index = 0; Index < Vertices.Count; Index++)
            {
                if (!TryProjectFeature(Vertices[Index].Latitude, Vertices[Index].Longitude, out Point Screen))
                {
                    Previous = null;
                    continue;
                }

                if (Previous.HasValue)
                    Context.DrawLine(RoadPen, Previous.Value, Screen);

                // The pointer extends the lines but is not a vertex until the user clicks.
                if (Index < MarkedVertexCount)
                {
                    if (DrawHandles)
                        DrawFeatureHandle(Context, Screen, PointBrush);
                    else
                        DrawFeatureMarker(Context, Screen, FEATURE_WELL_RADIUS_PX, PointBrush);
                }

                Previous = Screen;
            }
        }

        /// <summary>
        /// Draws the two dashed pairs. The lighter pair is the road bed and the darker pair is the toe.
        /// Both include half the bed, so changing the bed width moves both. The centreline is drawn on top.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Vertices">Centreline vertices.</param>
        /// <param name="BedWidthM">Full road bed width in metres.</param>
        /// <param name="HeightM">Embankment height in metres.</param>
        /// <param name="SideSlope">Horizontal run per one vertical.</param>
        private void DrawRoadGuides
            (
            DrawingContext Context,
            IReadOnlyList<FeatureVertex> Vertices,
            double BedWidthM,
            double HeightM,
            double SideSlope
            )
        {
            if (Vertices.Count < FieldFeatureSet.MIN_ROAD_VERTICES)
                return;

            DrawOffsetPair(Context, Vertices, RoadCrossSection.BedEdgeOffsetM(BedWidthM), FeatureBedPen);
            DrawOffsetPair(Context, Vertices, RoadCrossSection.ToeOffsetM(BedWidthM, HeightM, SideSlope), FeatureToePen);
        }

        /// <summary>
        /// Draws one dashed pair, the same ground distance to the left and right of the centreline.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Vertices">Centreline vertices.</param>
        /// <param name="OffsetM">Distance from the centreline in metres.</param>
        /// <param name="Pen">Dashed stroke for this pair.</param>
        private void DrawOffsetPair
            (
            DrawingContext Context,
            IReadOnlyList<FeatureVertex> Vertices,
            double OffsetM,
            IPen Pen
            )
        {
            if (OffsetM <= 0.0)
                return;

            DrawGuidePolyline(Context, RoadPlanOffset.Offset(Vertices, OffsetM), Pen);
            DrawGuidePolyline(Context, RoadPlanOffset.Offset(Vertices, -OffsetM), Pen);
        }

        /// <summary>
        /// Strokes a geographic polyline in screen space. A vertex that cannot be projected breaks the stroke.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Vertices">Geographic vertices.</param>
        /// <param name="Pen">Stroke for the polyline.</param>
        private void DrawGuidePolyline
            (
            DrawingContext Context,
            IReadOnlyList<FeatureVertex> Vertices,
            IPen Pen
            )
        {
            Point? Previous = null;
            for (int Index = 0; Index < Vertices.Count; Index++)
            {
                if (!TryProjectFeature(Vertices[Index].Latitude, Vertices[Index].Longitude, out Point Screen))
                {
                    Previous = null;
                    continue;
                }

                if (Previous.HasValue)
                    Context.DrawLine(Pen, Previous.Value, Screen);

                Previous = Screen;
            }
        }

        /// <summary>
        /// Copies draft vertices into centreline positions so the same offset can be used while drawing.
        /// </summary>
        /// <param name="Vertices">Draft vertices.</param>
        /// <returns>Centreline positions in the same order.</returns>
        private static List<FeatureVertex> DraftCentreline
            (
            IReadOnlyList<DraftVertex> Vertices
            )
        {
            List<FeatureVertex> Centre = new List<FeatureVertex>(Vertices.Count);
            for (int Index = 0; Index < Vertices.Count; Index++)
                Centre.Add(new FeatureVertex(Vertices[Index].Latitude, Vertices[Index].Longitude));

            return Centre;
        }

        /// <summary>
        /// Draws one well marker and the dashed pad circle at that well's diameter.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Latitude">Pad centre latitude in decimal degrees.</param>
        /// <param name="Longitude">Pad centre longitude in decimal degrees.</param>
        /// <param name="PadDiameterM">Pad diameter in metres.</param>
        /// <param name="Radius">Marker radius in pixels.</param>
        /// <param name="Fill">Fill for the marker. Orange while editing, green once saved.</param>
        private void DrawFeatureWell
            (
            DrawingContext Context,
            double Latitude,
            double Longitude,
            double PadDiameterM,
            double Radius,
            IBrush Fill
            )
        {
            if (!TryProjectFeature(Latitude, Longitude, out Point Screen))
                return;

            DrawDashedGroundCircle(Context, Screen, PadDiameterM * PAD_RADIUS_OF_DIAMETER, FeatureBedPen);
            DrawFeatureMarker(Context, Screen, Radius, Fill);
        }

        /// <summary>
        /// Draws a filled circle with a white outline.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Screen">Screen position in pixels.</param>
        /// <param name="Radius">Radius in pixels.</param>
        /// <param name="Fill">Fill for the marker.</param>
        private static void DrawFeatureMarker
            (
            DrawingContext Context,
            Point Screen,
            double Radius,
            IBrush Fill
            )
        {
            Context.DrawEllipse(Fill, FeatureOutlinePen, Screen, Radius, Radius);
        }

        /// <summary>
        /// Draws a larger handle so a vertex that can be dragged is visible.
        /// </summary>
        /// <param name="Context">Avalonia drawing context.</param>
        /// <param name="Screen">Screen position in pixels.</param>
        /// <param name="Fill">Fill for the handle. Orange while the vertex is being edited.</param>
        private static void DrawFeatureHandle
            (
            DrawingContext Context,
            Point Screen,
            IBrush Fill
            )
        {
            Context.DrawEllipse(Fill, FeatureOutlinePen, Screen, FEATURE_HANDLE_RADIUS_PX, FEATURE_HANDLE_RADIUS_PX);
        }

        /// <summary>
        /// Projects a geographic position into the current view.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <param name="Screen">Screen position in pixels.</param>
        /// <returns>True when the projection succeeds.</returns>
        private bool TryProjectFeature
            (
            double Latitude,
            double Longitude,
            out Point Screen
            )
        {
            if (!ProjectToolPointToScreen(Latitude, Longitude, out float ScreenX, out float ScreenY))
            {
                Screen = default;
                return false;
            }

            Screen = new Point(ScreenX, ScreenY);
            return true;
        }
    }
}
