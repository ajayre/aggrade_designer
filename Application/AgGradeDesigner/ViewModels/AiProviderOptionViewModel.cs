using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// One selectable AI provider entry for the settings dialog combo box.
    /// </summary>
    public sealed class AiProviderOptionViewModel
    {
        /// <summary>
        /// Creates a provider option row.
        /// </summary>
        /// <param name="Kind">Provider kind value.</param>
        /// <param name="DisplayName">User-visible label.</param>
        public AiProviderOptionViewModel
            (
            AgGradeDesigner.Core.Ai.AiProviderKind Kind,
            string DisplayName
            )
        {
            this.Kind = Kind;
            this.DisplayName = DisplayName;
        }

        /// <summary>Provider kind stored in settings.</summary>
        public AgGradeDesigner.Core.Ai.AiProviderKind Kind { get; }

        /// <summary>Display label shown in the combo box.</summary>
        public string DisplayName { get; }
    }

    /// <summary>
    /// One line displayed in the AI chat output pane.
    /// </summary>
    public sealed partial class ChatLineViewModel : ObservableObject
    {
        /// <summary>
        /// Creates a chat transcript line.
        /// </summary>
        /// <param name="Speaker">Speaker label such as You or AI.</param>
        /// <param name="Text">Message body.</param>
        /// <param name="IsError">True when the line represents an error status.</param>
        public ChatLineViewModel
            (
            string Speaker,
            string Text,
            bool IsError = false
            )
        {
            this.Speaker = Speaker;
            this.Text = Text;
            this.IsError = IsError;
        }

        /// <summary>Speaker label shown above the message text.</summary>
        public string Speaker { get; }

        /// <summary>Message body text.</summary>
        [ObservableProperty]
        private string _Text;

        /// <summary>True when styled as an error line.</summary>
        public bool IsError { get; }
    }
}
