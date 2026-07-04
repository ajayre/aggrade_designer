using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.Chat;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Units;

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
            Assert.Contains("Display units: imperial", Prompt, StringComparison.Ordinal);
            Assert.Contains("ALWAYS express as percent", Prompt, StringComparison.Ordinal);
            Assert.Contains("slopes to the user as m/m, ratio, or ft/ft", Prompt, StringComparison.Ordinal);
            Assert.Contains("mean local slope", Prompt, StringComparison.Ordinal);
            Assert.Contains("TOOLS ARE MANDATORY", Prompt, StringComparison.Ordinal);
            Assert.Contains("Users rarely say \"tool data\"", Prompt, StringComparison.Ordinal);
            Assert.Contains("get_tool_data before answering", Prompt, StringComparison.Ordinal);
        }

        /// <summary>
        /// Verifies metric display units are injected into the system prompt.
        /// </summary>
        [Fact]
        public void Build_MetricDisplayUnits_UsesMetricToken
            (
            )
        {
            ProjectDocument Project = new ProjectDocument();
            string Prompt = SystemPromptBuilder.Build(Project, AiDeliveryPhase.Phase1, DisplayUnitSystem.Metric);

            Assert.Contains("Display units: metric", Prompt, StringComparison.Ordinal);
            Assert.Contains("Default to metric for lengths", Prompt, StringComparison.Ordinal);
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
