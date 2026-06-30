using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Tests.Models
{
    /// <summary>
    /// Unit tests for <see cref="ViewerState"/> and related display defaults.
    /// </summary>
    public sealed class ViewerStateTests
    {
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
            Assert.Equal(36.0, State.Camera2D.CentreLat, 5);
            Assert.Equal(-90.0, State.Camera2D.CentreLon, 5);
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
            Assert.True(Display.DesignOverlay);
        }
    }
}
