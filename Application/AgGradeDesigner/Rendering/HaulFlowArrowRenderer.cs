using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Haul;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Draws haul transport flow arrows over 2D maps and 3D terrain using HaulBuilder flow-field conventions.
    /// </summary>
    public static class HaulFlowArrowRenderer
    {
        /// <summary>Projects WGS-84 coordinates to screen pixels.</summary>
        private delegate bool LatLonProjector(double Lat, double Lon, out float ScreenX, out float ScreenY);

        /// <summary>Maximum screen-space edge length for one arrow segment.</summary>
        private const float MAX_ARROW_SEGMENT_SCREEN_PX = 80f;

        /// <summary>Minimum screen-space stroke width for arrow outlines.</summary>
        private const float MIN_ARROW_STROKE_WIDTH = 1f;

        /// <summary>Hollow flow-arrow outline colour.</summary>
        private static readonly SKColor ArrowStrokeColor = SKColors.Black;

        /// <summary>
        /// Draws subsampled flow arrows in geographic 2D map space.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Viewport">Viewport rectangle in pixels.</param>
        /// <param name="Resolved">Resolved haul flow data.</param>
        /// <param name="CentreLat">Map centre latitude.</param>
        /// <param name="CentreLon">Map centre longitude.</param>
        /// <param name="MetresPerPixel">Ground metres per screen pixel.</param>
        /// <param name="ClipBounds">Optional geographic clip bounds; arrows outside are skipped.</param>
        public static void Draw2D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            HaulFlowFieldResolver.ResolvedHaulFlow Resolved,
            double CentreLat,
            double CentreLon,
            double MetresPerPixel,
            FieldBounds? ClipBounds = null
            )
        {
            if (!Resolved.HasVisibleArrows || MetresPerPixel <= 0.0)
                return;

            using SKPaint Stroke = new SKPaint
            {
                Color = ArrowStrokeColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(
                    MIN_ARROW_STROKE_WIDTH,
                    (float)(Resolved.Grid.GridSizeM / MetresPerPixel * 0.04))
            };

            MapViewport MapViewport = new MapViewport { Width = Viewport.Width, Height = Viewport.Height };
            LatLonProjector Project = (double Lat, double Lon, out float ScreenX, out float ScreenY) =>
                MapProjection.LatLonToScreen(
                    Lat,
                    Lon,
                    CentreLat,
                    CentreLon,
                    MetresPerPixel,
                    MapViewport,
                    out ScreenX,
                    out ScreenY);

            DrawArrows2D(Project, Canvas, Stroke, Resolved, ClipBounds);
        }

        /// <summary>
        /// Draws subsampled flow arrows draped on the active 3D surface.
        /// </summary>
        /// <param name="Canvas">Target canvas.</param>
        /// <param name="Viewport">Viewport rectangle in pixels.</param>
        /// <param name="Resolved">Resolved haul flow data.</param>
        /// <param name="Surface">Scalar surface sampler for elevation draping.</param>
        /// <param name="ContentView">Active field content view.</param>
        /// <param name="Camera">Orbit camera.</param>
        /// <param name="ClipBounds">Optional geographic clip bounds; arrows outside are skipped.</param>
        /// <param name="TopoDepth">Topo mesh depth buffer for camera-view occlusion testing.</param>
        internal static void Draw3D
            (
            SKCanvas Canvas,
            SKRect Viewport,
            HaulFlowFieldResolver.ResolvedHaulFlow Resolved,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            FieldBounds? ClipBounds,
            MeshDepthBuffer TopoDepth
            )
        {
            if (!Resolved.HasVisibleArrows)
                return;

            bool DrapeOnFlatOverlay = ContentView == FieldContentView.CutFill;

            using SKPaint Stroke = new SKPaint
            {
                Color = ArrowStrokeColor,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = MIN_ARROW_STROKE_WIDTH
            };

            HaulBuilder.Grid Grid = Resolved.Grid;
            HaulBuilder.FlowField Flow = Resolved.Flow;
            int ArrowStride = Resolved.ArrowStride;

            for (int By = 0; By < Grid.Height; By += ArrowStride)
            {
                for (int Bx = 0; Bx < Grid.Width; Bx += ArrowStride)
                {
                    if (!HaulFlowArrowGeometry.TryBuildDrawableTriangle(Grid, Flow, Bx, By, out HaulFlowArrowGeometry.ArrowTriangleUtm Triangle))
                        continue;

                    if (!HaulFlowArrowGeometry.TryGetCellCenterLatLon(Grid, Bx, By, out double CenterLat, out double CenterLon))
                        continue;

                    if (!AreAllTriangleVerticesInsideClip(ClipBounds, Grid, Triangle))
                        continue;

                    if (!TryProjectTriangle3D(
                            Triangle,
                            CenterLat,
                            CenterLon,
                            Grid,
                            Surface,
                            ContentView,
                            Camera,
                            Viewport,
                            DrapeOnFlatOverlay,
                            out float TipX,
                            out float TipY,
                            out double TipD,
                            out float LeftX,
                            out float LeftY,
                            out double LeftD,
                            out float RightX,
                            out float RightY,
                            out double RightD))
                    {
                        continue;
                    }

                    if (!AreReasonableSegments(TipX, TipY, LeftX, LeftY, RightX, RightY))
                        continue;

                    TopoDepthOverlayDrawer.DrawDepthTestedLine3D(Canvas, TipX, TipY, TipD, LeftX, LeftY, LeftD, TopoDepth, Stroke);
                    TopoDepthOverlayDrawer.DrawDepthTestedLine3D(Canvas, TipX, TipY, TipD, RightX, RightY, RightD, TopoDepth, Stroke);
                    TopoDepthOverlayDrawer.DrawDepthTestedLine3D(Canvas, LeftX, LeftY, LeftD, RightX, RightY, RightD, TopoDepth, Stroke);
                }
            }
        }

        /// <summary>
        /// Iterates flow-field cells and draws hollow triangular arrow glyphs in 2D.
        /// </summary>
        private static void DrawArrows2D
            (
            LatLonProjector ProjectLatLon,
            SKCanvas Canvas,
            SKPaint Stroke,
            HaulFlowFieldResolver.ResolvedHaulFlow Resolved,
            FieldBounds? ClipBounds
            )
        {
            HaulBuilder.Grid Grid = Resolved.Grid;
            HaulBuilder.FlowField Flow = Resolved.Flow;
            int ArrowStride = Resolved.ArrowStride;

            for (int By = 0; By < Grid.Height; By += ArrowStride)
            {
                for (int Bx = 0; Bx < Grid.Width; Bx += ArrowStride)
                {
                    if (!HaulFlowArrowGeometry.TryBuildDrawableTriangle(Grid, Flow, Bx, By, out HaulFlowArrowGeometry.ArrowTriangleUtm Triangle))
                        continue;

                    if (!AreAllTriangleVerticesInsideClip(ClipBounds, Grid, Triangle))
                        continue;

                    if (!TryProjectTriangleLatLon(ProjectLatLon, Grid, Triangle, out float TipX, out float TipY, out float LeftX, out float LeftY, out float RightX, out float RightY))
                        continue;

                    if (!AreReasonableSegments(TipX, TipY, LeftX, LeftY, RightX, RightY))
                        continue;

                    Canvas.DrawLine(TipX, TipY, LeftX, LeftY, Stroke);
                    Canvas.DrawLine(TipX, TipY, RightX, RightY, Stroke);
                    Canvas.DrawLine(LeftX, LeftY, RightX, RightY, Stroke);
                }
            }
        }

        /// <summary>
        /// Projects one arrow triangle from haul-grid UTM through WGS-84 into screen space.
        /// </summary>
        private static bool TryProjectTriangleLatLon
            (
            LatLonProjector ProjectLatLon,
            HaulBuilder.Grid Grid,
            HaulFlowArrowGeometry.ArrowTriangleUtm Triangle,
            out float TipX,
            out float TipY,
            out float LeftX,
            out float LeftY,
            out float RightX,
            out float RightY
            )
        {
            TipX = 0;
            TipY = 0;
            LeftX = 0;
            LeftY = 0;
            RightX = 0;
            RightY = 0;

            if (!TryProjectGridUtm(ProjectLatLon, Grid, Triangle.TipE, Triangle.TipN, out TipX, out TipY))
                return false;

            if (!TryProjectGridUtm(ProjectLatLon, Grid, Triangle.LeftE, Triangle.LeftN, out LeftX, out LeftY))
                return false;

            return TryProjectGridUtm(ProjectLatLon, Grid, Triangle.RightE, Triangle.RightN, out RightX, out RightY);
        }

        /// <summary>
        /// Projects one 3D arrow triangle using a single centre elevation so glyphs stay on the local tangent plane.
        /// </summary>
        private static bool TryProjectTriangle3D
            (
            HaulFlowArrowGeometry.ArrowTriangleUtm Triangle,
            double CenterLat,
            double CenterLon,
            HaulBuilder.Grid Grid,
            FieldSurfaceSampler Surface,
            FieldContentView ContentView,
            FieldCamera3D Camera,
            SKRect Viewport,
            bool DrapeOnFlatOverlay,
            out float TipX,
            out float TipY,
            out double TipD,
            out float LeftX,
            out float LeftY,
            out double LeftD,
            out float RightX,
            out float RightY,
            out double RightD
            )
        {
            TipX = 0;
            TipY = 0;
            TipD = 0;
            LeftX = 0;
            LeftY = 0;
            LeftD = 0;
            RightX = 0;
            RightY = 0;
            RightD = 0;

            if (DrapeOnFlatOverlay)
            {
                double OverlayZ = Camera.GroundPlaneClearanceM > 0
                    ? Camera.GroundPlaneClearanceM
                    : Field3DGeometryBuilder.ComputeFlatOverlayZLift(Camera);

                return TryProjectVertexFlat(Triangle.TipE, Triangle.TipN, Grid, Camera, Viewport, OverlayZ, out TipX, out TipY, out TipD) &&
                    TryProjectVertexFlat(Triangle.LeftE, Triangle.LeftN, Grid, Camera, Viewport, OverlayZ, out LeftX, out LeftY, out LeftD) &&
                    TryProjectVertexFlat(Triangle.RightE, Triangle.RightN, Grid, Camera, Viewport, OverlayZ, out RightX, out RightY, out RightD);
            }

            double CenterElev = Surface.SampleScalar(ContentView, CenterLat, CenterLon);
            if (double.IsNaN(CenterElev))
                return false;

            return TryProjectVertexTerrain(Triangle.TipE, Triangle.TipN, Grid, CenterElev, Camera, Viewport, out TipX, out TipY, out TipD) &&
                TryProjectVertexTerrain(Triangle.LeftE, Triangle.LeftN, Grid, CenterElev, Camera, Viewport, out LeftX, out LeftY, out LeftD) &&
                TryProjectVertexTerrain(Triangle.RightE, Triangle.RightN, Grid, CenterElev, Camera, Viewport, out RightX, out RightY, out RightD);
        }

        /// <summary>
        /// Projects one haul-grid UTM vertex onto the flat cut/fill overlay plane.
        /// </summary>
        private static bool TryProjectVertexFlat
            (
            double Easting,
            double Northing,
            HaulBuilder.Grid Grid,
            FieldCamera3D Camera,
            SKRect Viewport,
            double OverlayZ,
            out float ScreenX,
            out float ScreenY,
            out double Depth
            )
        {
            if (!HaulFlowArrowGeometry.TryUtmToLatLon(Grid, Easting, Northing, out double Lat, out double Lon))
            {
                ScreenX = 0;
                ScreenY = 0;
                Depth = 0;
                return false;
            }

            UTM.UTMCoordinate Point = UTM.FromLatLon(Lat, Lon);
            double OffsetEastM = Point.Easting - Camera.CentreUtm.Easting;
            double OffsetNorthM = Point.Northing - Camera.CentreUtm.Northing;
            Camera.ProjectModelOffset(OffsetEastM, OffsetNorthM, OverlayZ, Viewport, out ScreenX, out ScreenY, out Depth);
            return true;
        }

        /// <summary>
        /// Projects one haul-grid UTM vertex onto the terrain using a shared centre elevation.
        /// </summary>
        private static bool TryProjectVertexTerrain
            (
            double Easting,
            double Northing,
            HaulBuilder.Grid Grid,
            double CenterElev,
            FieldCamera3D Camera,
            SKRect Viewport,
            out float ScreenX,
            out float ScreenY,
            out double Depth
            )
        {
            if (!HaulFlowArrowGeometry.TryUtmToLatLon(Grid, Easting, Northing, out double Lat, out double Lon))
            {
                ScreenX = 0;
                ScreenY = 0;
                Depth = 0;
                return false;
            }

            Camera.Project(Lat, Lon, CenterElev, Viewport, FlatPlane: false, out ScreenX, out ScreenY, out Depth);
            return true;
        }

        /// <summary>
        /// Projects one haul-grid UTM coordinate through WGS-84 into screen pixels.
        /// </summary>
        private static bool TryProjectGridUtm
            (
            LatLonProjector ProjectLatLon,
            HaulBuilder.Grid Grid,
            double Easting,
            double Northing,
            out float ScreenX,
            out float ScreenY
            )
        {
            if (!HaulFlowArrowGeometry.TryUtmToLatLon(Grid, Easting, Northing, out double Lat, out double Lon))
            {
                ScreenX = 0;
                ScreenY = 0;
                return false;
            }

            return ProjectLatLon(Lat, Lon, out ScreenX, out ScreenY);
        }

        /// <summary>
        /// Returns true when every triangle vertex lies inside optional clip bounds.
        /// </summary>
        private static bool AreAllTriangleVerticesInsideClip
            (
            FieldBounds? ClipBounds,
            HaulBuilder.Grid Grid,
            HaulFlowArrowGeometry.ArrowTriangleUtm Triangle
            )
        {
            if (ClipBounds == null)
                return true;

            return IsInsideClip(ClipBounds, Grid, Triangle.TipE, Triangle.TipN) &&
                IsInsideClip(ClipBounds, Grid, Triangle.LeftE, Triangle.LeftN) &&
                IsInsideClip(ClipBounds, Grid, Triangle.RightE, Triangle.RightN);
        }

        /// <summary>
        /// Returns true when a haul-grid vertex lies inside optional clip bounds.
        /// </summary>
        private static bool IsInsideClip
            (
            FieldBounds? ClipBounds,
            HaulBuilder.Grid Grid,
            double Easting,
            double Northing
            )
        {
            if (ClipBounds == null)
                return true;

            if (!HaulFlowArrowGeometry.TryUtmToLatLon(Grid, Easting, Northing, out double Lat, out double Lon))
                return false;

            FieldBounds Bounds = ClipBounds.Value;
            return Lat >= Bounds.MinLat &&
                Lat <= Bounds.MaxLat &&
                Lon >= Bounds.MinLon &&
                Lon <= Bounds.MaxLon;
        }

        /// <summary>
        /// Rejects arrow segments that would span implausibly long screen distances.
        /// </summary>
        private static bool AreReasonableSegments
            (
            float TipX,
            float TipY,
            float LeftX,
            float LeftY,
            float RightX,
            float RightY
            )
        {
            return IsReasonableSegment(TipX, TipY, LeftX, LeftY) &&
                IsReasonableSegment(TipX, TipY, RightX, RightY) &&
                IsReasonableSegment(LeftX, LeftY, RightX, RightY);
        }

        /// <summary>
        /// Returns true when a screen segment length stays within the arrow glyph budget.
        /// </summary>
        private static bool IsReasonableSegment
            (
            float X0,
            float Y0,
            float X1,
            float Y1
            )
        {
            float Dx = X1 - X0;
            float Dy = Y1 - Y0;
            float LengthSq = (Dx * Dx) + (Dy * Dy);
            float MaxLengthSq = MAX_ARROW_SEGMENT_SCREEN_PX * MAX_ARROW_SEGMENT_SCREEN_PX;
            return LengthSq <= MaxLengthSq;
        }
    }
}
