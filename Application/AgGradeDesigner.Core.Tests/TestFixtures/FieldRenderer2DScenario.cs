using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Pan/zoom and display parameters for one headless 2D plan-view render scenario.
    /// </summary>
    internal readonly struct FieldRenderer2DScenario
    {
        /// <summary>
        /// Map centre latitude in decimal degrees.
        /// </summary>
        internal double CentreLat { get; init; }

        /// <summary>
        /// Map centre longitude in decimal degrees.
        /// </summary>
        internal double CentreLon { get; init; }

        /// <summary>
        /// 2D zoom factor (metres-per-pixel scale).
        /// </summary>
        internal double ZoomFactor { get; init; }

        /// <summary>
        /// Display settings for the frame.
        /// </summary>
        internal DisplaySettings Display { get; init; }

        /// <summary>
        /// Optional seeded satellite service when <see cref="DisplaySettings.Satellite"/> is true.
        /// </summary>
        internal SatelliteTileService? Satellite { get; init; }

        /// <summary>
        /// Builds a fitted default 2D view centred on the field bounds.
        /// </summary>
        /// <param name="Surface">Built field surface.</param>
        /// <param name="Project">Project document.</param>
        /// <param name="Display">Display settings.</param>
        /// <param name="Satellite">Optional seeded satellite tiles.</param>
        /// <returns>Scenario with camera fit to the field.</returns>
        static internal FieldRenderer2DScenario FitToField
            (
            FieldSurfaceSampler Surface,
            ProjectDocument Project,
            DisplaySettings Display,
            SatelliteTileService? Satellite = null
            )
        {
            double CentreLat = (Surface.MinLat + Surface.MaxLat) * 0.5;
            double CentreLon = (Surface.MinLon + Surface.MaxLon) * 0.5;
            double ZoomFactor = 1.0;
            if (FieldBounds.TryFromProject(Project, out FieldBounds Bounds))
            {
                MapViewFitResult Fit = MapViewFitter.Fit(
                    Bounds,
                    FieldRenderer2DBenchmark.ViewportWidth,
                    FieldRenderer2DBenchmark.ViewportHeight);
                CentreLat = Fit.CentreLat;
                CentreLon = Fit.CentreLon;
                ZoomFactor = Fit.ZoomFactor;
            }

            return new FieldRenderer2DScenario
            {
                CentreLat = CentreLat,
                CentreLon = CentreLon,
                ZoomFactor = ZoomFactor,
                Display = Display,
                Satellite = Satellite
            };
        }

        /// <summary>
        /// Zoomed-in plan view (higher zoom factor).
        /// </summary>
        static internal FieldRenderer2DScenario ZoomedIn
            (
            FieldRenderer2DScenario Base
            )
        {
            return Base with { ZoomFactor = Base.ZoomFactor * FieldRenderer2DBenchmark.ZOOM_IN_MULTIPLIER };
        }

        /// <summary>
        /// Zoomed-out plan view (lower zoom factor).
        /// </summary>
        static internal FieldRenderer2DScenario ZoomedOut
            (
            FieldRenderer2DScenario Base
            )
        {
            return Base with { ZoomFactor = Base.ZoomFactor * FieldRenderer2DBenchmark.ZOOM_OUT_MULTIPLIER };
        }

        /// <summary>
        /// Low topo opacity scenario (layered compositing path).
        /// </summary>
        static internal FieldRenderer2DScenario LowOpacity
            (
            FieldRenderer2DScenario Base
            )
        {
            DisplaySettings LowOpacityDisplay = new DisplaySettings
            {
                Satellite = Base.Display.Satellite,
                Contours = Base.Display.Contours,
                Hillshade = Base.Display.Hillshade,
                SurveyPoints = Base.Display.SurveyPoints,
                Benchmarks = Base.Display.Benchmarks,
                VerticalExaggeration = Base.Display.VerticalExaggeration,
                TopoCutFillOpacityPercent = 50.0
            };

            return Base with { Display = LowOpacityDisplay };
        }
    }
}
