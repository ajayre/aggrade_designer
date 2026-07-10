using System.Globalization;

using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Haul
{
    /// <summary>
    /// Resolves center-pivot overlay geometry and scrub bearings for the haul 2D view.
    /// </summary>
    public static class HaulPivotOverlay
    {
        /// <summary>Decimal places used when formatting pivot angle labels.</summary>
        private const int ANGLE_DISPLAY_DECIMALS = 1;

        /// <summary>Full circle in degrees used when normalizing bearings.</summary>
        private const double DEGREES_PER_TURN = 360.0;

        /// <summary>
        /// Validates that a pivot-enabled haul run has a centre inside the field boundary.
        /// </summary>
        /// <param name="Settings">Haul settings from the dialog session.</param>
        /// <param name="Project">Active project supplying survey/design boundary.</param>
        /// <param name="ErrorMessage">User-facing error when validation fails.</param>
        /// <returns>True when pivot is off or the centre lies inside the field.</returns>
        public static bool TryValidatePivotCenterForRun
            (
            HaulSettings Settings,
            ProjectDocument? Project,
            out string ErrorMessage
            )
        {
            ErrorMessage = string.Empty;
            if (!Settings.Pivot)
                return true;

            if (Project?.Survey == null)
            {
                ErrorMessage = "Pivot analysis requires a field survey so the pivot centre can be checked.";
                return false;
            }

            List<(double Lat, double Lon)>? Boundary = FieldBoundaryResolver.Resolve(
                Project.Survey,
                Project.DesignGrid);
            if (Boundary == null)
            {
                ErrorMessage = "Pivot analysis requires a field boundary so the pivot centre can be checked.";
                return false;
            }

            if (!BoundaryMask.IsInsideBoundaryLatLon(
                    Settings.PivotLatitudeDeg,
                    Settings.PivotLongitudeDeg,
                    Boundary))
            {
                ErrorMessage = "The pivot centre must be inside the field when pivot analysis is enabled.";
                return false;
            }

            return true;
        }

        /// <summary>
        /// Returns whether the haul 2D pivot overlay should be drawn for the active settings and data.
        /// </summary>
        /// <param name="Settings">Project haul settings.</param>
        /// <param name="Data">Haul session data with optional pivot overlay payload.</param>
        /// <returns>True when pivot mode is on, the centre is not 0,0, and overlay data is available.</returns>
        public static bool ShouldDraw
            (
            HaulSettings Settings,
            HaulData? Data
            )
        {
            if (!Settings.Pivot)
                return false;

            // intent: 0,0 is the unset pivot location sentinel — never draw an overlay there
            if (IsUnsetPivotLocation(Settings.PivotLatitudeDeg, Settings.PivotLongitudeDeg))
                return false;

            HaulPivotOverlayDto? Overlay = Data?.ResolvePivotOverlay();
            return Overlay != null && Overlay.RadiusM > 0.0;
        }

        /// <summary>
        /// Returns true when the pivot centre is the unset 0,0 sentinel.
        /// </summary>
        /// <param name="LatitudeDeg">Pivot latitude in decimal degrees.</param>
        /// <param name="LongitudeDeg">Pivot longitude in decimal degrees.</param>
        /// <returns>True when both coordinates are exactly zero.</returns>
        public static bool IsUnsetPivotLocation
            (
            double LatitudeDeg,
            double LongitudeDeg
            )
        {
            return LatitudeDeg == 0.0 && LongitudeDeg == 0.0;
        }

        /// <summary>
        /// Resolves the held pivot bearing for a zero-based route scrub index.
        /// </summary>
        /// <param name="Overlay">Persisted or live pivot overlay data.</param>
        /// <param name="RouteScrubIndex">Zero-based haul route scrub index.</param>
        /// <param name="BearingDeg">Output compass bearing (0 = north).</param>
        /// <returns>True when a bearing is available for the scrub index.</returns>
        public static bool TryResolveBearingDeg
            (
            HaulPivotOverlayDto? Overlay,
            int RouteScrubIndex,
            out double BearingDeg
            )
        {
            BearingDeg = 0.0;
            if (Overlay == null || Overlay.BearingByPassDeg.Length == 0)
                return false;

            int Index = Math.Clamp(RouteScrubIndex, 0, Overlay.BearingByPassDeg.Length - 1);
            BearingDeg = NormalizeBearingDeg(Overlay.BearingByPassDeg[Index]);
            return true;
        }

        /// <summary>
        /// Formats a compass bearing for the haul plan statistics panel.
        /// </summary>
        /// <param name="BearingDeg">Compass bearing in degrees (0 = north).</param>
        /// <returns>Formatted angle label with degree symbol.</returns>
        public static string FormatBearingLabel
            (
            double BearingDeg
            )
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:N" + ANGLE_DISPLAY_DECIMALS + "}°",
                NormalizeBearingDeg(BearingDeg));
        }

        /// <summary>
        /// Computes the tip latitude/longitude of the pivot arm at a compass bearing.
        /// </summary>
        /// <param name="CenterLat">Pivot centre latitude in decimal degrees.</param>
        /// <param name="CenterLon">Pivot centre longitude in decimal degrees.</param>
        /// <param name="RadiusM">Arm radius in metres.</param>
        /// <param name="BearingDeg">Compass bearing in degrees (0 = north, 90 = east).</param>
        /// <param name="TipLat">Output tip latitude.</param>
        /// <param name="TipLon">Output tip longitude.</param>
        public static void TipLatLon
            (
            double CenterLat,
            double CenterLon,
            double RadiusM,
            double BearingDeg,
            out double TipLat,
            out double TipLon
            )
        {
            UTM.UTMCoordinate Center = UTM.FromLatLon(CenterLat, CenterLon);
            double BearingRad = BearingDeg * (Math.PI / 180.0);
            // intent: compass bearing — 0° is north, so east uses sin and north uses cos
            double TipEasting = Center.Easting + (RadiusM * Math.Sin(BearingRad));
            double TipNorthing = Center.Northing + (RadiusM * Math.Cos(BearingRad));
            UTM.ToLatLon(
                Center.Zone,
                Center.IsNorthernHemisphere,
                TipEasting,
                TipNorthing,
                out TipLat,
                out TipLon);
        }

        /// <summary>
        /// Computes the largest radius that keeps a circle centred at the pivot entirely inside the field.
        /// </summary>
        /// <param name="CenterLat">Pivot centre latitude in decimal degrees.</param>
        /// <param name="CenterLon">Pivot centre longitude in decimal degrees.</param>
        /// <param name="BoundaryLatLon">Ordered field boundary ring in lat/lon.</param>
        /// <returns>Inscribed radius in metres, or zero when the centre is outside or the ring is invalid.</returns>
        public static double ComputeInscribedRadiusM
            (
            double CenterLat,
            double CenterLon,
            IReadOnlyList<(double Lat, double Lon)> BoundaryLatLon
            )
        {
            if (BoundaryLatLon.Count < GeoConstants.MIN_POLYGON_VERTICES)
                return 0.0;

            if (!BoundaryMask.IsInsideBoundaryLatLon(CenterLat, CenterLon, BoundaryLatLon))
                return 0.0;

            List<(double X, double Y)> Ring = BoundaryMask.BuildUtmRing(BoundaryLatLon);
            UTM.UTMCoordinate Center = UTM.FromLatLon(CenterLat, CenterLon);
            double MinDistanceM = double.PositiveInfinity;

            for (int Index = 0; Index < Ring.Count; Index++)
            {
                (double X0, double Y0) = Ring[Index];
                (double X1, double Y1) = Ring[(Index + 1) % Ring.Count];
                double DistanceM = DistancePointToSegmentM(Center.Easting, Center.Northing, X0, Y0, X1, Y1);
                if (DistanceM < MinDistanceM)
                    MinDistanceM = DistanceM;
            }

            return double.IsFinite(MinDistanceM) ? Math.Max(0.0, MinDistanceM) : 0.0;
        }

        /// <summary>
        /// Normalizes a bearing into the half-open range [0, 360).
        /// </summary>
        /// <param name="BearingDeg">Raw compass bearing in degrees.</param>
        /// <returns>Normalized bearing in degrees.</returns>
        public static double NormalizeBearingDeg
            (
            double BearingDeg
            )
        {
            double Normalized = BearingDeg % DEGREES_PER_TURN;
            if (Normalized < 0.0)
                Normalized += DEGREES_PER_TURN;
            return Normalized;
        }

        /// <summary>
        /// Returns the shortest distance from a point to a line segment in metres.
        /// </summary>
        /// <param name="Px">Point easting.</param>
        /// <param name="Py">Point northing.</param>
        /// <param name="X0">Segment start easting.</param>
        /// <param name="Y0">Segment start northing.</param>
        /// <param name="X1">Segment end easting.</param>
        /// <param name="Y1">Segment end northing.</param>
        /// <returns>Distance in metres.</returns>
        private static double DistancePointToSegmentM
            (
            double Px,
            double Py,
            double X0,
            double Y0,
            double X1,
            double Y1
            )
        {
            double Dx = X1 - X0;
            double Dy = Y1 - Y0;
            double LengthSq = (Dx * Dx) + (Dy * Dy);
            if (LengthSq <= FieldConstants.EPSILON_FINE)
            {
                double Ex = Px - X0;
                double Ey = Py - Y0;
                return Math.Sqrt((Ex * Ex) + (Ey * Ey));
            }

            // intent: clamp the projection onto the segment so corners are measured correctly
            double T = Math.Clamp((((Px - X0) * Dx) + ((Py - Y0) * Dy)) / LengthSq, 0.0, 1.0);
            double ClosestX = X0 + (T * Dx);
            double ClosestY = Y0 + (T * Dy);
            double OffsetX = Px - ClosestX;
            double OffsetY = Py - ClosestY;
            return Math.Sqrt((OffsetX * OffsetX) + (OffsetY * OffsetY));
        }
    }
}
