namespace AgGradeDesigner.Rendering
{
    /// <summary>
    /// Maps north-up 2D pan/zoom state to a locked 3D orbit camera (nadir plan view).
    /// </summary>
    internal static class PlanViewCamera
    {
        /// <summary>North-up plan view (east maps to screen right).</summary>
        public const double LockedHeadingDeg = 180.0;

        /// <summary>Near top-down pitch matching interactive 3D plan view.</summary>
        public const double LockedPitchDeg = 89.0;

        /// <summary>
        /// Converts 2D map centre and zoom into locked orbit camera parameters.
        /// </summary>
        static public void ResolveOrbitParameters
            (
            double CentreLat,
            double CentreLon,
            double ZoomFactor,
            float ViewportHeight,
            out double HeadingDeg,
            out double PitchDeg,
            out double DistanceM,
            out double OrbitCentreLat,
            out double OrbitCentreLon
            )
        {
            HeadingDeg = LockedHeadingDeg;
            PitchDeg = LockedPitchDeg;
            OrbitCentreLat = CentreLat;
            OrbitCentreLon = CentreLon;
            double MetresPerPixel = FieldRenderer.EstimateMetresPerPixel(CentreLat, ZoomFactor);
            DistanceM = Math.Max(
                RenderingConstants.MIN_ORBIT_DISTANCE_M,
                MetresPerPixel * ViewportHeight);
        }
    }
}
