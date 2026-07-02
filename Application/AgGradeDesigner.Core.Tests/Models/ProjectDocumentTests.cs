using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Survey;

namespace AgGradeDesigner.Core.Tests.Models
{
    /// <summary>
    /// Unit tests for <see cref="ProjectDocument"/> display names and reset behaviour.
    /// </summary>
    public sealed class ProjectDocumentTests
    {
        /// <summary>
        /// Sample latitude used by this component.
        /// </summary>

        private const double SAMPLE_LATITUDE = 1;
        /// <summary>
        /// Sample longitude used by this component.
        /// </summary>

        private const double SAMPLE_LONGITUDE = 2;
        /// <summary>
        /// Sample existing elevation used by this component.
        /// </summary>

        private const double SAMPLE_EXISTING_ELEVATION = 3;

        /// <summary>
        /// Verifies an unsaved project shows the untitled display name.
        /// </summary>
        [Fact]
        public void DisplayName_UntitledWhenNoPath
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            // New documents without a file path use the product untitled label.
            Assert.Equal("Untitled", Project.DisplayName);
        }

        /// <summary>
        /// Verifies modified projects append the dirty marker to the title bar name.
        /// </summary>
        [Fact]
        public void TitleBarName_ShowsDirtyMarker
            (
            )
        {
            ProjectDocument Project = new ProjectDocument { FilePath = @"C:\data\Field.agproject" };
            Project.MarkModified();
            // Dirty state must be visible in the chrome title suffix.
            Assert.Equal("Field*", Project.TitleBarName);
        }

        /// <summary>
        /// Verifies reset clears survey, design, and modification flags.
        /// </summary>
        [Fact]
        public void ResetToEmpty_ClearsSurveyAndDesign
            (
            )
        {
            ProjectDocument Project = new ProjectDocument
            {
                Survey = new SurveyModel(),
                DesignParameters = new DesignParameters(),
                DesignGrid = new DesignGrid()
            };
            Project.Survey.InteriorPoints.Add(new TopologyPoint
            {
                Latitude = SAMPLE_LATITUDE,
                Longitude = SAMPLE_LONGITUDE,
                ExistingElevation = SAMPLE_EXISTING_ELEVATION
            });
            Project.MarkModified();

            Project.ResetToEmpty();

            // Reset must return the document to a pristine empty shell.
            Assert.False(Project.HasSurvey);
            Assert.False(Project.HasDesign);
            Assert.False(Project.IsModified);
            Assert.Equal("Untitled", Project.DisplayName);
        }
    }
}
