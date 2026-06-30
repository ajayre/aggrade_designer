using AgGradeDesigner.Core.Color;

namespace AgGradeDesigner.Core.Tests.Color
{
    /// <summary>
    /// Unit tests for <see cref="NineBandColorScale"/> band indexing and formatting helpers.
    /// </summary>
    public sealed class NineBandColorScaleTests
    {
        /// <summary>
        /// Verifies a zero value range maps to the green centre band.
        /// </summary>
        [Fact]
        public void GetBandIndex_WhenRangeZero_ReturnsGreenBand
            (
            )
        {
            // Degenerate range should fall back to the neutral green band index.
            Assert.Equal(4, NineBandColorScale.GetBandIndex(50.0, 100.0, 100.0));
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
            Assert.Equal(0, NineBandColorScale.GetBandIndex(100.0, 0.0, 100.0));
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
            Assert.Equal(8, NineBandColorScale.GetBandIndex(0.0, 0.0, 100.0));
        }

        /// <summary>
        /// Verifies a mid-range value lands near the green centre band.
        /// </summary>
        [Fact]
        public void GetBandIndex_MiddleValue_ReturnsNearGreen
            (
            )
        {
            int Band = NineBandColorScale.GetBandIndex(50.0, 0.0, 100.0);
            // Midpoint values should stay within one band of centre green.
            Assert.InRange(Band, 3, 5);
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
            Assert.Equal(9, NineBandColorScale.BandColorsArgb.Length);
            Assert.Equal(9, NineBandColorScale.BandNames.Length);
        }

        /// <summary>
        /// Verifies band boundary arrays include endpoints for positive ranges.
        /// </summary>
        [Fact]
        public void GetBandBoundaries_ReturnsTenValuesWhenRangePositive
            (
            )
        {
            double[] Boundaries = NineBandColorScale.GetBandBoundaries(0.0, 10.0);
            // Ten boundary values bracket nine interior bands between min and max.
            Assert.Equal(10, Boundaries.Length);
            Assert.Equal(10.0, Boundaries[0], 6);
            Assert.Equal(0.0, Boundaries[9], 6);
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
            Assert.Equal("123.456", NineBandColorScale.FormatMetres(123.456));
        }

        /// <summary>
        /// Verifies feet formatting converts from metres with invariant culture.
        /// </summary>
        [Fact]
        public void FormatFeet_ConvertsMetresToFeet
            (
            )
        {
            Assert.Equal("328.08", NineBandColorScale.FormatFeet(100.0));
        }
    }
}
