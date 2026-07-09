using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Rendering;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Verifies 3D mesh geometry cache keys match production fingerprint rules.
    /// </summary>
    [Trait("Category", "RenderingRegression")]
    public sealed class Field3DGeometryCacheTests
    {
        /// <summary>
        /// Satellite display toggles must invalidate cached static mesh geometry.
        /// </summary>
        [Fact]
        public void GeometryCacheKey_DiffersWhenSatelliteToggled
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            DisplaySettings SatelliteOff = new DisplaySettings { Satellite = false, Contours = true, Hillshade = true };
            DisplaySettings SatelliteOn = new DisplaySettings { Satellite = true, Contours = true, Hillshade = true };

            Field3DGeometryCacheKey OffKey = CreateKey(Project, FieldContentView.Existing, SatelliteOff);
            Field3DGeometryCacheKey OnKey = CreateKey(Project, FieldContentView.Existing, SatelliteOn);

            Assert.NotEqual(OffKey, OnKey);
        }

        /// <summary>
        /// Contour and hillshade toggles must invalidate cached static mesh geometry.
        /// </summary>
        [Fact]
        public void GeometryCacheKey_DiffersWhenContourToggleChanges
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            DisplaySettings ContoursOn = new DisplaySettings { Satellite = false, Contours = true, Hillshade = true };
            DisplaySettings ContoursOff = new DisplaySettings { Satellite = false, Contours = false, Hillshade = true };

            Field3DGeometryCacheKey OnKey = CreateKey(Project, FieldContentView.Existing, ContoursOn);
            Field3DGeometryCacheKey OffKey = CreateKey(Project, FieldContentView.Existing, ContoursOff);

            Assert.NotEqual(OnKey, OffKey);
        }

        /// <summary>
        /// Haul 3D pinned scrub indices must not reuse survey Existing mesh geometry.
        /// </summary>
        [Fact]
        public void GeometryCacheKey_DiffersWhenHaulRouteScrubIndexChanges
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            DisplaySettings Display = new DisplaySettings { Satellite = false, Contours = true, Hillshade = true };

            Field3DGeometryCacheKey SurveyKey = CreateKey(Project, FieldContentView.Existing, Display, HaulRouteScrubIndex: -1);
            Field3DGeometryCacheKey HaulFrameKey = CreateKey(Project, FieldContentView.Existing, Display, HaulRouteScrubIndex: 1791);

            Assert.NotEqual(SurveyKey, HaulFrameKey);
        }

        /// <summary>
        /// Builds a geometry cache fingerprint matching production <see cref="Field3DGeometryBuilder"/> rules.
        /// </summary>
        /// <param name="Project">Project reference stored in the cache key.</param>
        /// <param name="ContentView">Active scalar view.</param>
        /// <param name="Display">Display toggles affecting static mesh storage.</param>
        /// <param name="HaulRouteScrubIndex">Pinned haul route scrub index for haul 3D, or -1 when not applicable.</param>
        /// <returns>Cache key under test.</returns>
        private static Field3DGeometryCacheKey CreateKey
            (
            ProjectDocument Project,
            FieldContentView ContentView,
            DisplaySettings Display,
            int HaulRouteScrubIndex = -1
            )
        {
            // intent: tests assume satellite tiles are available when Display.Satellite is true
            bool ShowSatellite = Display.Satellite;
            return new Field3DGeometryCacheKey
            {
                Project = Project,
                ContentView = ContentView,
                ShowSatellite = ShowSatellite,
                Contours = Display.Contours,
                Hillshade = Display.Hillshade,
                HaulRouteScrubIndex = HaulRouteScrubIndex,
                TrueScaleReliefStorageVersion = 1,
                SatelliteStaticLayoutVersion = ShowSatellite ? 1 : 0
            };
        }
    }
}
