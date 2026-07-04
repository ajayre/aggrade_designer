using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

using SkiaSharp;

namespace AgGradeDesigner.Core.Tests.TestFixtures
{
    /// <summary>
    /// Camera and display parameters for one headless 3D render scenario.
    /// </summary>
    internal readonly struct FieldRenderer3DScenario
    {
        /// <summary>
        /// Orbit heading in degrees.
        /// </summary>
        internal double HeadingDeg { get; init; }

        /// <summary>
        /// Orbit pitch in degrees.
        /// </summary>
        internal double PitchDeg { get; init; }

        /// <summary>
        /// Camera distance from orbit centre in metres.
        /// </summary>
        internal double DistanceM { get; init; }

        /// <summary>
        /// Orbit centre latitude; NaN uses field midpoint.
        /// </summary>
        internal double OrbitCentreLat { get; init; }

        /// <summary>
        /// Orbit centre longitude; NaN uses field midpoint.
        /// </summary>
        internal double OrbitCentreLon { get; init; }

        /// <summary>
        /// Display settings for the frame.
        /// </summary>
        internal DisplaySettings Display { get; init; }

        /// <summary>
        /// Optional seeded satellite service when <see cref="DisplaySettings.Satellite"/> is true.
        /// </summary>
        internal SatelliteTileService? Satellite { get; init; }

        /// <summary>
        /// Default orbit view used by most regression scenarios.
        /// </summary>
        static internal FieldRenderer3DScenario DefaultOrbit
            (
            DisplaySettings Display,
            SatelliteTileService? Satellite = null
            )
        {
            return new FieldRenderer3DScenario
            {
                HeadingDeg = 45.0,
                PitchDeg = 30.0,
                DistanceM = 800.0,
                OrbitCentreLat = double.NaN,
                OrbitCentreLon = double.NaN,
                Display = Display,
                Satellite = Satellite
            };
        }

        /// <summary>
        /// Zoomed-in orbit (closer camera).
        /// </summary>
        static internal FieldRenderer3DScenario ZoomedIn
            (
            DisplaySettings Display,
            SatelliteTileService? Satellite = null
            )
        {
            FieldRenderer3DScenario Base = DefaultOrbit(Display, Satellite);
            return Base with { DistanceM = 350.0 };
        }

        /// <summary>
        /// Zoomed-out orbit (farther camera).
        /// </summary>
        static internal FieldRenderer3DScenario ZoomedOut
            (
            DisplaySettings Display,
            SatelliteTileService? Satellite = null
            )
        {
            FieldRenderer3DScenario Base = DefaultOrbit(Display, Satellite);
            return Base with { DistanceM = 1600.0 };
        }

        /// <summary>
        /// Low topo opacity scenario (layered compositing path).
        /// </summary>
        static internal FieldRenderer3DScenario LowOpacity
            (
            DisplaySettings Display,
            SatelliteTileService? Satellite = null
            )
        {
            DisplaySettings LowOpacityDisplay = new DisplaySettings
            {
                Satellite = Display.Satellite,
                Contours = Display.Contours,
                Hillshade = Display.Hillshade,
                SurveyPoints = Display.SurveyPoints,
                Benchmarks = Display.Benchmarks,
                VerticalExaggeration = Display.VerticalExaggeration,
                TopoCutFillOpacityPercent = 50.0
            };

            return DefaultOrbit(LowOpacityDisplay, Satellite);
        }
    }
}
