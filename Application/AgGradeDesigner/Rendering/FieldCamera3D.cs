using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// 3D orbit camera projection for the field viewer (§2.2, §2.3, §2.5.5).
    /// </summary>
    internal readonly struct FieldCamera3D
    {
        /// <summary>Fraction of lat/lon span used when defaulting the orbit pivot to field centre.</summary>
        private const double FIELD_CENTRE_FRACTION = 0.5;
        /// <summary>Fraction of horizontal extent mapped to the active elevation range for relief scale.</summary>
        private const double RELIEF_SCALE_HORIZONTAL_FRACTION = 0.25;
        /// <summary>Minimum elevation span in metres to avoid division by zero in relief scaling.</summary>
        private const double MIN_ELEVATION_RANGE_M = 1e-6;
        /// <summary>Floor on |sin(pitch)| when converting screen drag to geographic pan.</summary>
        private const double MIN_PITCH_SINE_FOR_PAN = 0.15;
        /// <summary>Model-space Z for flat ground-plane geometry (satellite tiles, cut/fill overlay).</summary>
        private const double FLAT_PLANE_MODEL_Z = 0.0;
        /// <summary>Lower bound clamp for satellite ground-plane clearance in model units.</summary>
        private const double MIN_GROUND_PLANE_CLEARANCE_M = 0.0;
        /// <summary>Vertical exaggeration at or below this value uses true 1:1 metre relief.</summary>
        private const double MIN_VERTICAL_EXAGGERATION = 0.0;
        /// <summary>Minimum projected pixel span when estimating ground metres per pixel.</summary>
        private const double MIN_PIXEL_SPAN = 1.0;
        /// <summary>Degrees in half a circle (radians-to-degrees helper).</summary>
        private const double DEGREES_PER_RADIAN = 180.0;

        /// <summary>
        /// Builds camera state from field bounds, orbit angles, and vertical exaggeration.
        /// </summary>
        /// <param name="Surface">Sampled field surface defining extent and relief.</param>
        /// <param name="HeadingDeg">Orbit heading in degrees.</param>
        /// <param name="PitchDeg">Orbit pitch in degrees.</param>
        /// <param name="DistanceM">Camera distance in metres.</param>
        /// <param name="VerticalExaggeration">Vertical scale applied to elevation delta.</param>
        /// <param name="OrbitCentreLat">Orbit pivot latitude; null uses field geometric centre.</param>
        /// <param name="OrbitCentreLon">Orbit pivot longitude; null uses field geometric centre.</param>
        /// <param name="ReliefView">Active scalar view; relief is anchored to its minimum elevation.</param>
        /// <param name="GroundPlaneClearanceM">
        /// Model-space lift applied above the satellite ground plane (Z=0) for all projected geometry.
        /// </param>
        public FieldCamera3D
            (
            FieldSurfaceSampler Surface,
            double HeadingDeg,
            double PitchDeg,
            double DistanceM,
            double VerticalExaggeration,
            double? OrbitCentreLat = null,
            double? OrbitCentreLon = null,
            FieldContentView ReliefView = FieldContentView.Existing,
            double GroundPlaneClearanceM = 0.0
            )
        {
            CentreLat = OrbitCentreLat ?? ((Surface.MinLat + Surface.MaxLat) * FIELD_CENTRE_FRACTION);
            CentreLon = OrbitCentreLon ?? ((Surface.MinLon + Surface.MaxLon) * FIELD_CENTRE_FRACTION);
            CentreUtm = UTM.FromLatLon(CentreLat, CentreLon);

            (MinElevation, double ElevationRangeM) = ResolveReliefExtents(Surface, ReliefView);

            UTM.UTMCoordinate SouthWest = UTM.FromLatLon(Surface.MinLat, Surface.MinLon);
            UTM.UTMCoordinate NorthEast = UTM.FromLatLon(Surface.MaxLat, Surface.MaxLon);
            HorizontalExtentM = Math.Max(
                Math.Abs(NorthEast.Easting - SouthWest.Easting),
                Math.Abs(NorthEast.Northing - SouthWest.Northing));

            // Scale relief so typical field elevation range maps to a visible fraction of horizontal extent.
            ReliefScale = (HorizontalExtentM * RELIEF_SCALE_HORIZONTAL_FRACTION) / ElevationRangeM;

            HeadingRad = HeadingDeg * Math.PI / DEGREES_PER_RADIAN;
            PitchRad = PitchDeg * Math.PI / DEGREES_PER_RADIAN;
            this.DistanceM = Math.Max(RenderingConstants.MIN_ORBIT_DISTANCE_M, DistanceM);
            this.VerticalExaggeration = VerticalExaggeration;
            this.GroundPlaneClearanceM = Math.Max(MIN_GROUND_PLANE_CLEARANCE_M, GroundPlaneClearanceM);
        }

        /// <summary>
        /// Field centre latitude in degrees.
        /// </summary>
        public double CentreLat { get; }

        /// <summary>
        /// Field centre longitude in degrees.
        /// </summary>
        public double CentreLon { get; }

        /// <summary>
        /// UTM coordinates of the field centre.
        /// </summary>
        public UTM.UTMCoordinate CentreUtm { get; }

        /// <summary>
        /// Vertical datum for relief: the lowest elevation of the active topo view.
        /// </summary>
        public double MinElevation { get; }

        /// <summary>
        /// Model-space clearance above the satellite ground plane for relief topo (existing/proposed).
        /// Flat overlays (cut/fill) and the satellite tiles themselves stay at Z=0 plus their own lift.
        /// </summary>
        public double GroundPlaneClearanceM { get; init; }

        /// <summary>
        /// Resolves the relief anchor and span for the active field content view.
        /// </summary>
        static private (double VerticalDatum, double ElevationRangeM) ResolveReliefExtents
            (
            FieldSurfaceSampler Surface,
            FieldContentView ReliefView
            )
        {
            if (ReliefView == FieldContentView.CutFill)
            {
                double ExistingRange = Math.Max(Surface.MaxElevation - Surface.MinElevation, MIN_ELEVATION_RANGE_M);
                return (Surface.MinElevation, ExistingRange);
            }

            Surface.ComputeScalarRange(ReliefView, out double VMin, out double VMax);
            return (VMin, Math.Max(VMax - VMin, MIN_ELEVATION_RANGE_M));
        }

        /// <summary>
        /// Larger horizontal span of the field in metres.
        /// </summary>
        public double HorizontalExtentM { get; }

        /// <summary>
        /// Multiplier mapping elevation delta to model units for 3D display.
        /// </summary>
        public double ReliefScale { get; }

        /// <summary>
        /// Converts an elevation delta above the relief anchor into model-space Z before ground clearance.
        /// At 0× exaggeration this uses true 1:1 metre scale; above 0× the auto relief scale applies.
        /// </summary>
        static private double ComputeReliefModelZ
            (
            double ElevationM,
            double MinElevation,
            double VerticalExaggeration,
            double ReliefScale
            )
        {
            double Delta = ElevationM - MinElevation;
            if (double.IsNaN(Delta) || double.IsInfinity(Delta))
                return FLAT_PLANE_MODEL_Z;

            if (VerticalExaggeration <= MIN_VERTICAL_EXAGGERATION)
                return Delta;

            return Delta * VerticalExaggeration * ReliefScale;
        }

        /// <summary>
        /// Orbit heading in radians.
        /// </summary>
        public double HeadingRad { get; }

        /// <summary>
        /// Orbit pitch in radians.
        /// </summary>
        public double PitchRad { get; }

        /// <summary>
        /// Camera distance from the field centre in metres.
        /// </summary>
        public double DistanceM { get; init; }

        /// <summary>
        /// Vertical exaggeration factor applied during projection.
        /// </summary>
        public double VerticalExaggeration { get; init; }

        /// <summary>
        /// Lowest pitch (degrees): horizontal view with eye height at minimum elevation.
        /// </summary>
        static public double MinimumOrbitPitchDeg => FLAT_PLANE_MODEL_Z;

        /// <summary>
        /// Converts a screen-space drag delta to a geographic offset for orbit-centre panning.
        /// </summary>
        /// <param name="DxPx">Horizontal pointer delta in pixels (positive = right).</param>
        /// <param name="DyPx">Vertical pointer delta in pixels (positive = down).</param>
        /// <param name="Viewport">Current viewport rectangle.</param>
        /// <returns>Latitude and longitude delta in degrees to apply to the orbit pivot.</returns>
        public (double DeltaLat, double DeltaLon) ComputeScreenPanLatLonDelta
            (
            double DxPx,
            double DyPx,
            SKRect Viewport
            )
        {
            double Scale = Viewport.Height / DistanceM;
            if (Scale <= 0)
                return (0, 0);

            // Pivot moves opposite to drag so the scene follows the pointer (camera-aligned axes).
            double DRx = -DxPx / Scale;
            double AbsSinPitch = Math.Max(Math.Abs(Math.Sin(PitchRad)), MIN_PITCH_SINE_FOR_PAN);
            double DFy = -DyPx / (Scale * AbsSinPitch);

            double CosH = Math.Cos(HeadingRad);
            double SinH = Math.Sin(HeadingRad);
            double DEx = (DRx * CosH) + (DFy * SinH);
            double DEy = (-DRx * SinH) + (DFy * CosH);

            UTM.ToLatLon(
                CentreUtm.Zone,
                CentreUtm.IsNorthernHemisphere,
                CentreUtm.Easting + DEx,
                CentreUtm.Northing + DEy,
                out double NewLat,
                out double NewLon);

            return (NewLat - CentreLat, NewLon - CentreLon);
        }

        /// <summary>
        /// Projects a geographic point to screen coordinates and depth for painter ordering.
        /// </summary>
        /// <param name="Lat">Point latitude in degrees.</param>
        /// <param name="Lon">Point longitude in degrees.</param>
        /// <param name="ElevationM">Elevation in metres above datum.</param>
        /// <param name="Viewport">Target viewport rectangle.</param>
        /// <param name="FlatPlane">When true, suppress vertical relief for ground-plane tiles.</param>
        /// <param name="ScreenX">Projected X in viewport pixels.</param>
        /// <param name="ScreenY">Projected Y in viewport pixels.</param>
        /// <param name="Depth">Depth value for back-to-front sorting.</param>
        public void Project
            (
            double Lat,
            double Lon,
            double ElevationM,
            SKRect Viewport,
            bool FlatPlane,
            out float ScreenX,
            out float ScreenY,
            out double Depth
            )
        {
            UTM.UTMCoordinate Point = UTM.FromLatLon(Lat, Lon);
            double Ex = Point.Easting - CentreUtm.Easting;
            double Ey = Point.Northing - CentreUtm.Northing;
            double Ez = FlatPlane
                ? FLAT_PLANE_MODEL_Z
                : ComputeReliefModelZ(ElevationM, MinElevation, VerticalExaggeration, ReliefScale);
            if (!FlatPlane)
                Ez += GroundPlaneClearanceM;

            double CosH = Math.Cos(HeadingRad);
            double SinH = Math.Sin(HeadingRad);
            double CosP = Math.Cos(PitchRad);
            double SinP = Math.Sin(PitchRad);

            // Rotate into camera space, then apply pitch to derive screen depth and vertical offset.
            double Rx = Ex * CosH - Ey * SinH;
            double Fy = Ex * SinH + Ey * CosH;

            Depth = Fy * CosP + Ez * SinP;
            double Up = Ez * CosP - Fy * SinP;

            double Scale = Viewport.Height / DistanceM;
            // Negate Rx so that east (positive Ex) maps to the right of the screen.
            // With HeadingDeg=180 (north-up), cos(π)=-1 gives Rx=-Ex; negating restores east→right.
            ScreenX = (float)(Viewport.MidX - Rx * Scale);
            ScreenY = (float)(Viewport.MidY - Up * Scale);
        }

        /// <summary>
        /// Computes camera-space depth for a model-space east/north/Z point (used for GPU depth mapping).
        /// </summary>
        public double ComputeCameraDepth
            (
            double OffsetEastM,
            double OffsetNorthM,
            double ModelZ
            )
        {
            double CosH = Math.Cos(HeadingRad);
            double SinH = Math.Sin(HeadingRad);
            double CosP = Math.Cos(PitchRad);
            double SinP = Math.Sin(PitchRad);

            double Rx = (OffsetEastM * CosH) - (OffsetNorthM * SinH);
            double Fy = (OffsetEastM * SinH) + (OffsetNorthM * CosH);
            return (Fy * CosP) + (ModelZ * SinP);
        }

        /// <summary>
        /// Converts a geographic point to model-space east/north/Z coordinates relative to the orbit pivot.
        /// </summary>
        public (double EastM, double NorthM, double ModelZ) ToModelSpace
            (
            double Lat,
            double Lon,
            double ElevationM,
            bool FlatPlane
            )
        {
            UTM.UTMCoordinate Point = UTM.FromLatLon(Lat, Lon);
            double EastM = Point.Easting - CentreUtm.Easting;
            double NorthM = Point.Northing - CentreUtm.Northing;
            double ModelZ = FlatPlane
                ? FLAT_PLANE_MODEL_Z
                : ComputeReliefModelZ(ElevationM, MinElevation, VerticalExaggeration, ReliefScale);
            if (!FlatPlane)
                ModelZ += GroundPlaneClearanceM;
            return (EastM, NorthM, ModelZ);
        }

        /// <summary>
        /// Builds GPU camera uniforms for the active viewport.
        /// </summary>
        public Core.Rendering.Vulkan.CameraGpuUniforms ToGpuUniforms
            (
            SKRect Viewport,
            double DepthMin,
            double DepthMax
            )
        {
            double Scale = Viewport.Height / DistanceM;
            return new Core.Rendering.Vulkan.CameraGpuUniforms
            {
                ViewportWidth = Viewport.Width,
                ViewportHeight = Viewport.Height,
                ViewportMidX = Viewport.MidX,
                ViewportMidY = Viewport.MidY,
                Scale = (float)Scale,
                CosH = (float)Math.Cos(HeadingRad),
                SinH = (float)Math.Sin(HeadingRad),
                CosP = (float)Math.Cos(PitchRad),
                SinP = (float)Math.Sin(PitchRad),
                DepthMin = (float)DepthMin,
                DepthMax = (float)DepthMax
            };
        }

        /// <summary>
        /// Projects a model-space offset from the orbit pivot using explicit east/north/Z model coordinates.
        /// </summary>
        public void ProjectModelOffset
            (
            double OffsetEastM,
            double OffsetNorthM,
            double ModelZ,
            SKRect Viewport,
            out float ScreenX,
            out float ScreenY,
            out double Depth
            )
        {
            double Scale = Viewport.Height / DistanceM;
            FieldCameraProjection.ProjectModelOffset(
                OffsetEastM,
                OffsetNorthM,
                ModelZ,
                Viewport.Width,
                Viewport.Height,
                Viewport.MidX,
                Viewport.MidY,
                Scale,
                Math.Cos(HeadingRad),
                Math.Sin(HeadingRad),
                Math.Cos(PitchRad),
                Math.Sin(PitchRad),
                out ScreenX,
                out ScreenY,
                out Depth);
        }

        /// <summary>
        /// Returns the camera-space Z component of a world east/north/up direction.
        /// </summary>
        public double ModelNormalViewZ
            (
            double East,
            double North,
            double Up
            )
        {
            double CosH = Math.Cos(HeadingRad);
            double SinH = Math.Sin(HeadingRad);
            double CosP = Math.Cos(PitchRad);
            double SinP = Math.Sin(PitchRad);

            double Rx = (East * CosH) - (North * SinH);
            double Fy = (East * SinH) + (North * CosH);
            return (Fy * CosP) + (Up * SinP);
        }

        /// <summary>
        /// Estimates ground metres per screen pixel at the field centre for tile zoom selection.
        /// </summary>
        /// <param name="Viewport">Current viewport rectangle.</param>
        /// <returns>Metres per pixel at the field centre.</returns>
        public double EstimateGroundMetresPerScreenPixel
            (
            SKRect Viewport
            )
        {
            Project(CentreLat, CentreLon, MinElevation, Viewport, FlatPlane: true, out float X0, out float Y0, out _);

            UTM.ToLatLon(
                CentreUtm.Zone,
                CentreUtm.IsNorthernHemisphere,
                CentreUtm.Easting + RenderingConstants.GROUND_SAMPLE_SPAN_M,
                CentreUtm.Northing,
                out double EastLat,
                out double EastLon);

            Project(EastLat, EastLon, MinElevation, Viewport, FlatPlane: true, out float X1, out float Y1, out _);
            double PixelSpan = Math.Sqrt((X1 - X0) * (X1 - X0) + (Y1 - Y0) * (Y1 - Y0));
            return RenderingConstants.GROUND_SAMPLE_SPAN_M / Math.Max(MIN_PIXEL_SPAN, PixelSpan);
        }
    }
}
