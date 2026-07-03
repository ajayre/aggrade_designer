using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Tests.Units
{
    /// <summary>
    /// Unit tests for <see cref="DisplayUnitFormatter"/>.
    /// </summary>
    public sealed class DisplayUnitFormatterTests
    {
        /// <summary>Sample elevation in metres used by formatter tests.</summary>
        private const double SAMPLE_ELEVATION_M = 100.0;

        /// <summary>Sample area in acres used by formatter tests.</summary>
        private const double SAMPLE_AREA_ACRES = 10.0;

        /// <summary>Sample volume in cubic yards used by formatter tests.</summary>
        private const double SAMPLE_VOLUME_CY = 100.0;

        /// <summary>
        /// Verifies imperial elevation formatting uses feet with two decimal places.
        /// </summary>
        [Fact]
        public void FormatElevation_Imperial_UsesFeet
            (
            )
        {
            Assert.Equal("328.08 ft", DisplayUnitFormatter.FormatElevation(SAMPLE_ELEVATION_M, DisplayUnitSystem.Imperial));
        }

        /// <summary>
        /// Verifies metric elevation formatting uses metres with three decimal places.
        /// </summary>
        [Fact]
        public void FormatElevation_Metric_UsesMetres
            (
            )
        {
            Assert.Equal("100.000 m", DisplayUnitFormatter.FormatElevation(SAMPLE_ELEVATION_M, DisplayUnitSystem.Metric));
        }

        /// <summary>
        /// Verifies imperial area formatting uses acres.
        /// </summary>
        [Fact]
        public void FormatAreaFromAcres_Imperial_UsesAcres
            (
            )
        {
            Assert.Equal("Area: 10.00 acres", DisplayUnitFormatter.FormatAreaFromAcres(SAMPLE_AREA_ACRES, DisplayUnitSystem.Imperial));
        }

        /// <summary>
        /// Verifies metric area formatting uses hectares.
        /// </summary>
        [Fact]
        public void FormatAreaFromAcres_Metric_UsesHectares
            (
            )
        {
            Assert.Equal("Area: 4.05 ha", DisplayUnitFormatter.FormatAreaFromAcres(SAMPLE_AREA_ACRES, DisplayUnitSystem.Metric));
        }

        /// <summary>
        /// Verifies cubic yard to display volume conversion is identity for imperial.
        /// </summary>
        [Fact]
        public void CubicYardsToDisplayVolume_Imperial_IsIdentity
            (
            )
        {
            Assert.Equal(SAMPLE_VOLUME_CY, DisplayUnitFormatter.CubicYardsToDisplayVolume(SAMPLE_VOLUME_CY, DisplayUnitSystem.Imperial));
        }

        /// <summary>
        /// Verifies cubic yard to cubic metre conversion for metric display.
        /// </summary>
        [Fact]
        public void CubicYardsToDisplayVolume_Metric_ConvertsToCubicMetres
            (
            )
        {
            double Display = DisplayUnitFormatter.CubicYardsToDisplayVolume(SAMPLE_VOLUME_CY, DisplayUnitSystem.Metric);
            Assert.Equal(76.46, Display, 2);
        }

        /// <summary>
        /// Verifies display volume round-trips back to cubic yards for metric.
        /// </summary>
        [Fact]
        public void DisplayVolumeToCubicYards_Metric_RoundTrips
            (
            )
        {
            double Display = DisplayUnitFormatter.CubicYardsToDisplayVolume(SAMPLE_VOLUME_CY, DisplayUnitSystem.Metric);
            double Back = DisplayUnitFormatter.DisplayVolumeToCubicYards(Display, DisplayUnitSystem.Metric);
            Assert.Equal(SAMPLE_VOLUME_CY, Back, 6);
        }
    }
}
