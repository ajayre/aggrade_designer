using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.Chat;
using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Core.Tests.Ai
{
    /// <summary>
    /// Unit tests for <see cref="SystemPromptBuilder"/>.
    /// </summary>
    public sealed class SystemPromptBuilderTests
    {
        /// <summary>
        /// Verifies the normative template is present with substituted placeholders.
        /// </summary>
        [Fact]
        public void Build_IncludesTemplateAndPlaceholders
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            string Prompt = SystemPromptBuilder.Build(Project, AiDeliveryPhase.Phase1);

            Assert.Contains("You are the AgGrade Designer assistant", Prompt, StringComparison.Ordinal);
            Assert.Contains("Project: Untitled", Prompt, StringComparison.Ordinal);
            Assert.Contains("Survey loaded: no", Prompt, StringComparison.Ordinal);
            Assert.Contains("Design loaded: no", Prompt, StringComparison.Ordinal);
            Assert.Contains("Delivery phase: 1", Prompt, StringComparison.Ordinal);
            Assert.Contains("TOOLS ARE MANDATORY", Prompt, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies unsaved projects use a non-empty project placeholder in the prompt.
        /// </summary>
        [Fact]
        public void Build_UnsavedProject_UsesUntitledDisplayName
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            string Prompt = SystemPromptBuilder.Build(Project, AiDeliveryPhase.Phase1);

            Assert.Contains("Project: Untitled", Prompt, StringComparison.Ordinal);
            Assert.DoesNotContain("Project: \r\n", Prompt, StringComparison.Ordinal);
        }
    }
}
