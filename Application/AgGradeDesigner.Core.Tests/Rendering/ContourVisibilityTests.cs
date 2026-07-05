using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Rendering;

namespace AgGradeDesigner.Core.Tests.Rendering
{
    /// <summary>
    /// Unit tests for <see cref="ContourVisibility"/>.
    /// </summary>
    public sealed class ContourVisibilityTests
    {
        /// <summary>
        /// Composite grade just below the contour threshold.
        /// </summary>
        private const double BelowThresholdGradePercent = 0.029;

        /// <summary>
        /// Composite grade at the contour threshold.
        /// </summary>
        private const double AtThresholdGradePercent = 0.03;

        /// <summary>
        /// Composite grade above the contour threshold.
        /// </summary>
        private const double AboveThresholdGradePercent = 0.5;

        /// <summary>
        /// Verifies near-flat proposed designs suppress contour drawing even when toggled on.
        /// </summary>
        [Fact]
        public void ShouldDrawContours_ProposedBelowThreshold_ReturnsFalse
            (
            )
        {
            DisplaySettings Display = new DisplaySettings { Contours = true };
            ProjectDocument Project = CreateProject(BelowThresholdGradePercent);

            Assert.False(ContourVisibility.ShouldDrawContours(Display, Project, FieldContentView.Proposed));
        }

        /// <summary>
        /// Verifies proposed contours are shown at the minimum design grade threshold.
        /// </summary>
        [Fact]
        public void ShouldDrawContours_ProposedAtThreshold_ReturnsTrue
            (
            )
        {
            DisplaySettings Display = new DisplaySettings { Contours = true };
            ProjectDocument Project = CreateProject(AtThresholdGradePercent);

            Assert.True(ContourVisibility.ShouldDrawContours(Display, Project, FieldContentView.Proposed));
        }

        /// <summary>
        /// Verifies existing-view contours are unaffected by flat proposed designs.
        /// </summary>
        [Fact]
        public void ShouldDrawContours_ExistingWithFlatDesign_ReturnsTrue
            (
            )
        {
            DisplaySettings Display = new DisplaySettings { Contours = true };
            ProjectDocument Project = CreateProject(BelowThresholdGradePercent);

            Assert.True(ContourVisibility.ShouldDrawContours(Display, Project, FieldContentView.Existing));
        }

        /// <summary>
        /// Verifies the contours toggle still disables drawing for all views.
        /// </summary>
        [Fact]
        public void ShouldDrawContours_ContoursToggleOff_ReturnsFalse
            (
            )
        {
            DisplaySettings Display = new DisplaySettings { Contours = false };
            ProjectDocument Project = CreateProject(AboveThresholdGradePercent);

            Assert.False(ContourVisibility.ShouldDrawContours(Display, Project, FieldContentView.Proposed));
            Assert.False(ContourVisibility.ShouldDrawContours(Display, Project, FieldContentView.Existing));
        }

        /// <summary>
        /// Builds a minimal project carrying the supplied composite design grade.
        /// </summary>
        /// <param name="CompositeSlopePercent">Design composite grade percent.</param>
        /// <returns>Project with a populated design grid.</returns>
        static private ProjectDocument CreateProject
            (
            double CompositeSlopePercent
            )
        {
            return new ProjectDocument
            {
                DesignGrid = new DesignGrid
                {
                    CompositeSlopePercent = CompositeSlopePercent
                }
            };
        }
    }
}
