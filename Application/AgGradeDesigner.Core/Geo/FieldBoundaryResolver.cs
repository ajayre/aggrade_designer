using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Geo
{
    /// <summary>
    /// Resolves the canonical field boundary ring used to clip viewer surface rasters.
    /// </summary>
    public static class FieldBoundaryResolver
    {
        /// <summary>Minimum boundary vertices required for clipping.</summary>
        private const int MIN_BOUNDARY_VERTEX_COUNT = GeoConstants.MIN_POLYGON_VERTICES;

        /// <summary>
        /// Resolves the field boundary ring from explicit survey 2PER points or inferred observed samples.
        /// </summary>
        /// <param name="Survey">Survey model with optional 2PER boundary points.</param>
        /// <param name="Grid">Optional design grid whose valid bins supplement inference.</param>
        /// <returns>Ordered lat/lon boundary ring, or null when no boundary can be resolved.</returns>
        public static List<(double Lat, double Lon)>? Resolve
            (
            SurveyModel? Survey,
            DesignGrid? Grid
            )
        {
            List<(double Lat, double Lon)>? SurveyBoundary = TryResolveSurveyBoundary(Survey);
            if (SurveyBoundary != null)
                return SurveyBoundary;

            return ObservedFieldBoundaryInferrer.TryInferFromProject(Survey, Grid);
        }

        /// <summary>
        /// Returns the ordered 2PER survey boundary when enough boundary points are present.
        /// </summary>
        /// <param name="Survey">Survey model with optional boundary points.</param>
        /// <returns>Ordered lat/lon ring, or null when 2PER is absent.</returns>
        private static List<(double Lat, double Lon)>? TryResolveSurveyBoundary
            (
            SurveyModel? Survey
            )
        {
            if (Survey == null || Survey.BoundaryPoints.Count < MIN_BOUNDARY_VERTEX_COUNT)
                return null;

            List<(double Lat, double Lon)> Ring = new List<(double Lat, double Lon)>(Survey.BoundaryPoints.Count);
            foreach (TopologyPoint Point in Survey.BoundaryPoints)
                Ring.Add((Point.Latitude, Point.Longitude));

            return BoundaryMask.OrderBoundaryLatLon(Ring);
        }
    }
}
