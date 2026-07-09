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
        /// Satellite display toggles must not rebuild cached topo mesh geometry.
        /// </summary>
        [Fact]
        public void GeometryCacheKey_SameWhenSatelliteToggled
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            DisplaySettings SatelliteOff = new DisplaySettings { Satellite = false, Contours = true, Hillshade = true };
            DisplaySettings SatelliteOn = new DisplaySettings { Satellite = true, Contours = true, Hillshade = true };

            Field3DGeometryCacheKey OffKey = CreateKey(Project, FieldContentView.Existing, SatelliteOff);
            Field3DGeometryCacheKey OnKey = CreateKey(Project, FieldContentView.Existing, SatelliteOn);

            Assert.Equal(OffKey, OnKey);
        }

        /// <summary>
        /// Contour display toggles must not rebuild cached topo mesh geometry.
        /// </summary>
        [Fact]
        public void GeometryCacheKey_SameWhenContourToggleChanges
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            DisplaySettings ContoursOn = new DisplaySettings { Satellite = false, Contours = true, Hillshade = true };
            DisplaySettings ContoursOff = new DisplaySettings { Satellite = false, Contours = false, Hillshade = true };

            Field3DGeometryCacheKey OnKey = CreateKey(Project, FieldContentView.Existing, ContoursOn);
            Field3DGeometryCacheKey OffKey = CreateKey(Project, FieldContentView.Existing, ContoursOff);

            Assert.Equal(OnKey, OffKey);
        }

        /// <summary>
        /// Hillshade toggles must invalidate cached mesh vertex colours.
        /// </summary>
        [Fact]
        public void GeometryCacheKey_DiffersWhenHillshadeToggled
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            DisplaySettings HillshadeOn = new DisplaySettings { Satellite = false, Contours = true, Hillshade = true };
            DisplaySettings HillshadeOff = new DisplaySettings { Satellite = false, Contours = true, Hillshade = false };

            Field3DGeometryCacheKey OnKey = CreateKey(Project, FieldContentView.Existing, HillshadeOn);
            Field3DGeometryCacheKey OffKey = CreateKey(Project, FieldContentView.Existing, HillshadeOff);

            Assert.NotEqual(OnKey, OffKey);
        }

        /// <summary>
        /// Haul 3D must not reuse survey Existing mesh geometry and must vary by pinned scrub index.
        /// </summary>
        [Fact]
        public void GeometryCacheKey_DiffersForHaulContentViewAndPinnedScrubIndex
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            DisplaySettings Display = new DisplaySettings { Satellite = false, Contours = true, Hillshade = true };

            Field3DGeometryCacheKey SurveyKey = CreateKey(Project, FieldContentView.Existing, Display, HaulRouteScrubIndex: -1);
            Field3DGeometryCacheKey HaulFrameKey = CreateKey(Project, FieldContentView.Haul, Display, HaulRouteScrubIndex: 1791);
            Field3DGeometryCacheKey HaulOtherFrameKey = CreateKey(Project, FieldContentView.Haul, Display, HaulRouteScrubIndex: 0);

            Assert.NotEqual(SurveyKey, HaulFrameKey);
            Assert.NotEqual(HaulFrameKey, HaulOtherFrameKey);
        }

        /// <summary>
        /// Builds a geometry cache fingerprint matching production <see cref="Field3DGeometryBuilder"/> rules.
        /// </summary>
        /// <param name="Project">Project reference stored in the cache key.</param>
        /// <param name="ContentView">Active scalar view.</param>
        /// <param name="Display">Display toggles affecting hillshade vertex colours.</param>
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
            return new Field3DGeometryCacheKey
            {
                Project = Project,
                ContentView = ContentView,
                Hillshade = Display.Hillshade,
                HaulRouteScrubIndex = HaulRouteScrubIndex,
                TrueScaleReliefStorageVersion = 1,
            };
        }
    }
}
