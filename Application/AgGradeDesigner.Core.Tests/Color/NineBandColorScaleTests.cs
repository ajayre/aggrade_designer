using AgGradeDesigner.Core.Color;

namespace AgGradeDesigner.Core.Tests.Color
{
    /// <summary>
    /// Unit tests for <see cref="NineBandColorScale"/> band indexing and formatting helpers.
    /// </summary>
    public sealed class NineBandColorScaleTests
    {
        /// <summary>
        /// Mid value used by this component.
        /// </summary>

        private const double MID_VALUE = 50.0;
        /// <summary>
        /// Min value used by this component.
        /// </summary>

        private const double MIN_VALUE = 0.0;
        /// <summary>
        /// Max value used by this component.
        /// </summary>

        private const double MAX_VALUE = 100.0;
        /// <summary>
        /// Range min used by this component.
        /// </summary>

        private const double RANGE_MIN = 0.0;
        /// <summary>
        /// Range max used by this component.
        /// </summary>

        private const double RANGE_MAX = 10.0;
        /// <summary>
        /// Format metres input used by this component.
        /// </summary>

        private const double FORMAT_METRES_INPUT = 123.456;
        /// <summary>
        /// Format feet input m used by this component.
        /// </summary>

        private const double FORMAT_FEET_INPUT_M = 100.0;
        /// <summary>
        /// Green band index used by this component.
        /// </summary>

        private const int GREEN_BAND_INDEX = 4;
        /// <summary>
        /// Dark red band index used by this component.
        /// </summary>

        private const int DARK_RED_BAND_INDEX = 0;
        /// <summary>
        /// Indigo band index used by this component.
        /// </summary>

        private const int INDIGO_BAND_INDEX = 8;
        /// <summary>
        /// Near green band min used by this component.
        /// </summary>

        private const int NEAR_GREEN_BAND_MIN = 3;
        /// <summary>
        /// Near green band max used by this component.
        /// </summary>

        private const int NEAR_GREEN_BAND_MAX = 5;
        /// <summary>
        /// Interior band count plus one, used to space contour levels.
        /// </summary>

        private const int BAND_COUNT = 9;
        /// <summary>
        /// Boundary count used by this component.
        /// </summary>

        private const int BOUNDARY_COUNT = 10;
        /// <summary>
        /// Boundary precision used by this component.
        /// </summary>

        private const int BOUNDARY_PRECISION = 6;

        /// <summary>
        /// Verifies a zero value range maps to the green centre band.
        /// </summary>
        [Fact]
        public void GetBandIndex_WhenRangeZero_ReturnsGreenBand
            (
            )
        {
            // Degenerate range should fall back to the neutral green band index.
            Assert.Equal(GREEN_BAND_INDEX, NineBandColorScale.GetBandIndex(MID_VALUE, MAX_VALUE, MAX_VALUE));
        }

        /// <summary>
        /// Verifies the maximum value maps to the dark-red band.
        /// </summary>
        [Fact]
        public void GetBandIndex_AtMaximum_ReturnsDarkRed
            (
            )
        {
            // High cut/fill values use the warm end of the nine-band palette.
            Assert.Equal(DARK_RED_BAND_INDEX, NineBandColorScale.GetBandIndex(MAX_VALUE, MIN_VALUE, MAX_VALUE));
        }

        /// <summary>
        /// Verifies the minimum value maps to the indigo band.
        /// </summary>
        [Fact]
        public void GetBandIndex_AtMinimum_ReturnsIndigo
            (
            )
        {
            // Low cut/fill values use the cool end of the nine-band palette.
            Assert.Equal(INDIGO_BAND_INDEX, NineBandColorScale.GetBandIndex(MIN_VALUE, MIN_VALUE, MAX_VALUE));
        }

        /// <summary>
        /// Verifies a mid-range value lands near the green centre band.
        /// </summary>
        [Fact]
        public void GetBandIndex_MiddleValue_ReturnsNearGreen
            (
            )
        {
            int Band = NineBandColorScale.GetBandIndex(MID_VALUE, MIN_VALUE, MAX_VALUE);
            // Midpoint values should stay within one band of centre green.
            Assert.InRange(Band, NEAR_GREEN_BAND_MIN, NEAR_GREEN_BAND_MAX);
        }

        /// <summary>
        /// Verifies colour tables expose nine bands for UI legend rendering.
        /// </summary>
        [Fact]
        public void BandColors_HasNineEntries
            (
            )
        {
            // Legend tables must stay aligned with the nine-band scale contract.
            Assert.Equal(BAND_COUNT, NineBandColorScale.BandColorsArgb.Length);
            Assert.Equal(BAND_COUNT, NineBandColorScale.BandNames.Length);
        }

        /// <summary>
        /// Verifies band boundary arrays include endpoints for positive ranges.
        /// </summary>
        [Fact]
        public void GetBandBoundaries_ReturnsTenValuesWhenRangePositive
            (
            )
        {
            double[] Boundaries = NineBandColorScale.GetBandBoundaries(RANGE_MIN, RANGE_MAX);
            // Ten boundary values bracket nine interior bands between min and max.
            Assert.Equal(BOUNDARY_COUNT, Boundaries.Length);
            Assert.Equal(RANGE_MAX, Boundaries[0], BOUNDARY_PRECISION);
            Assert.Equal(RANGE_MIN, Boundaries[BAND_COUNT], BOUNDARY_PRECISION);
        }

        /// <summary>
        /// Verifies flat scalar ranges still return ten identical boundary values.
        /// </summary>
        [Fact]
        public void GetBandBoundaries_WhenRangeZero_ReturnsTenEqualValues
            (
            )
        {
            double[] Boundaries = NineBandColorScale.GetBandBoundaries(RANGE_MAX, RANGE_MAX);

            Assert.Equal(BOUNDARY_COUNT, Boundaries.Length);
            foreach (double Boundary in Boundaries)
                Assert.Equal(RANGE_MAX, Boundary, BOUNDARY_PRECISION);
        }

        /// <summary>
        /// Verifies metre formatting uses invariant culture for stable test output.
        /// </summary>
        [Fact]
        public void FormatMetres_UsesInvariantCulture
            (
            )
        {
            // UI labels must not depend on the host locale decimal separator.
            Assert.Equal("123.456", NineBandColorScale.FormatMetres(FORMAT_METRES_INPUT));
        }

        /// <summary>
        /// Verifies feet formatting converts from metres with invariant culture.
        /// </summary>
        [Fact]
        public void FormatFeet_ConvertsMetresToFeet
            (
            )
        {
            Assert.Equal("328.08", NineBandColorScale.FormatFeet(FORMAT_FEET_INPUT_M));
        }
    }
}
