using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgGradeDesigner.Views
{
    /// <summary>
    /// Read-only survey statistics dialog code-behind.
    /// </summary>
    public partial class SurveyStatisticsDialog : Window
    {
        /// <summary>
        /// Initializes dialog XAML.
        /// </summary>
        public SurveyStatisticsDialog
            (
            )
        {
            InitializeComponent();
        }

        /// <summary>
        /// Closes the dialog when the user clicks the close button.
        /// </summary>
        /// <param name="Sender">Close button source.</param>
        /// <param name="Args">Click event arguments.</param>
        private void OnCloseClick
            (
            object? Sender,
            RoutedEventArgs Args
            )
        {
            Close();
        }
    }
}
