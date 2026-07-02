using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Tests.Models
{
    /// <summary>
    /// Unit tests for <see cref="ViewerState"/> and related display defaults.
    /// </summary>
    public sealed class ViewerStateTests
    {
        /// <summary>
        /// Default centre lat used by this component.
        /// </summary>

        private const double DEFAULT_CENTRE_LAT = 36.0;
        /// <summary>
        /// Default centre lon used by this component.
        /// </summary>

        private const double DEFAULT_CENTRE_LON = -90.0;
        /// <summary>
        /// Coord precision used by this component.
        /// </summary>

        private const int COORD_PRECISION = 5;

        /// <summary>
        /// Verifies viewer defaults match the product startup specification.
        /// </summary>
        [Fact]
        public void Defaults_MatchSpecStartup
            (
            )
        {
            ViewerState State = new ViewerState();
            // Startup camera and overlay toggles must match the documented baseline.
            Assert.Equal(ViewMode.TwoD, State.ViewMode);
            Assert.Equal(FieldContentView.None, State.FieldContentView);
            Assert.Equal(DEFAULT_CENTRE_LAT, State.Camera2D.CentreLat, COORD_PRECISION);
            Assert.Equal(DEFAULT_CENTRE_LON, State.Camera2D.CentreLon, COORD_PRECISION);
            Assert.True(State.Display.Benchmarks);
            Assert.False(State.Display.Satellite);
        }

        /// <summary>
        /// Verifies display decoration toggles default to visible for design review.
        /// </summary>
        [Fact]
        public void DisplaySettings_DefaultDecorationsOn
            (
            )
        {
            DisplaySettings Display = new DisplaySettings();
            // Core map decorations ship enabled so first open shows full context.
            Assert.True(Display.SurveyPoints);
            Assert.True(Display.Contours);
            Assert.True(Display.Hillshade);
            Assert.True(Display.Benchmarks);
            Assert.False(Display.DesignOverlay);
        }
    }
}
