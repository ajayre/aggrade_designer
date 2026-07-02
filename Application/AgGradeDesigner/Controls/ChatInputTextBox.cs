using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace AgGradeDesigner.Controls
{
    /// <summary>
    /// Multi-line chat compose box: Enter submits, Shift+Enter inserts a newline.
    /// </summary>
    public class ChatInputTextBox : TextBox
    {
        /// <summary>
        /// Defines the <see cref="SubmitCommand"/> property.
        /// </summary>
        public static readonly StyledProperty<ICommand?> SubmitCommandProperty =
            AvaloniaProperty.Register<ChatInputTextBox, ICommand?>(nameof(SubmitCommand));

        /// <summary>
        /// Command executed when the user presses Enter without Shift.
        /// </summary>
        public ICommand? SubmitCommand
        {
            get => GetValue(SubmitCommandProperty);
            set => SetValue(SubmitCommandProperty, value);
        }

        /// <summary>
        /// Intercepts Enter before the default TextBox newline handling runs.
        /// </summary>
        /// <param name="EventArgs">Key event arguments.</param>
        protected override void OnKeyDown
            (
            KeyEventArgs EventArgs
            )
        {
            if (IsSubmitKey(EventArgs.Key) &&
                (EventArgs.KeyModifiers & KeyModifiers.Shift) != KeyModifiers.Shift)
            {
                EventArgs.Handled = true;
                if (SubmitCommand?.CanExecute(null) == true)
                    SubmitCommand.Execute(null);

                return;
            }

            base.OnKeyDown(EventArgs);
        }

        /// <summary>
        /// Returns true when the key is the main or numpad Enter key.
        /// </summary>
        /// <param name="Key">Key reported by the input event.</param>
        /// <returns>True for Enter-style keys.</returns>
        static private bool IsSubmitKey
            (
            Key Key
            )
        {
            return Key == Key.Enter || Key == Key.Return;
        }
    }
}
