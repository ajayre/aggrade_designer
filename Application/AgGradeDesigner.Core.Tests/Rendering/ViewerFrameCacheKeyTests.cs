using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Unit tests for viewer frame cache key helpers.
    /// </summary>
    public sealed class ViewerFrameCacheKeyTests
    {
        /// <summary>
        /// Builds a sample 3D cache key for tests.
        /// </summary>
        /// <param name="HeadingDeg">Orbit heading in degrees.</param>
        /// <param name="PitchDeg">Orbit pitch in degrees.</param>
        /// <param name="VerticalExaggeration">Vertical exaggeration multiplier.</param>
        /// <returns>Cache fingerprint string.</returns>
        private static string BuildSampleThreeDKey
            (
            double HeadingDeg,
            double PitchDeg,
            double VerticalExaggeration
            )
        {
            DisplaySettings Display = new DisplaySettings
            {
                VerticalExaggeration = VerticalExaggeration,
                TopoCutFillOpacityPercent = 100.0
            };

            return ViewerFrameCacheKey.Build(new ViewerFrameState
            {
                ViewMode = ViewMode.ThreeD,
                Width = 1546,
                Height = 902,
                ContentView = FieldContentView.Existing,
                Display = Display,
                ProjectKey = "project",
                OrbitHeadingDeg = HeadingDeg,
                OrbitPitchDeg = PitchDeg,
                OrbitDistanceM = 500.0,
                OrbitCentreLat = 36.447712,
                OrbitCentreLon = -90.723636,
                HaulRouteScrubIndex = 1993
            });
        }

        /// <summary>
        /// Orbit-only changes must produce distinct cache keys.
        /// </summary>
        [Fact]
        public void Build_ThreeDKey_DiffersWhenOrbitChanges
            (
            )
        {
            string KeyA = BuildSampleThreeDKey(HeadingDeg: 87.8, PitchDeg: 53.7, VerticalExaggeration: 0.9712);
            string KeyB = BuildSampleThreeDKey(HeadingDeg: 97.8, PitchDeg: 58.2, VerticalExaggeration: 0.9712);

            Assert.NotEqual(KeyA, KeyB);
        }

        /// <summary>
        /// Vertical exaggeration changes must produce distinct cache keys.
        /// </summary>
        [Fact]
        public void Build_ThreeDKey_DiffersWhenVerticalExaggerationChanges
            (
            )
        {
            string KeyA = BuildSampleThreeDKey(HeadingDeg: 218.2, PitchDeg: 33.9, VerticalExaggeration: 0.9712);
            string KeyB = BuildSampleThreeDKey(HeadingDeg: 218.2, PitchDeg: 33.9, VerticalExaggeration: 1.051);

            Assert.NotEqual(KeyA, KeyB);
        }

        /// <summary>
        /// Identical viewer state must produce the same cache key.
        /// </summary>
        [Fact]
        public void Build_ThreeDKey_IsStableForIdenticalState
            (
            )
        {
            string KeyA = BuildSampleThreeDKey(HeadingDeg: 120.0, PitchDeg: 45.0, VerticalExaggeration: 1.0);
            string KeyB = BuildSampleThreeDKey(HeadingDeg: 120.0, PitchDeg: 45.0, VerticalExaggeration: 1.0);

            Assert.Equal(KeyA, KeyB);
        }
    }
}
