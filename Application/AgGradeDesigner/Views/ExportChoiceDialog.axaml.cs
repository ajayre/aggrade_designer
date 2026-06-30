using AgGradeDesigner.ViewModels;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgGradeDesigner.Views
{
    /// <summary>
    /// Export choice dialog code-behind; closes when the view model signals completion.
    /// </summary>
    public partial class ExportChoiceDialog : Window
    {
        /// <summary>
        /// Initializes dialog XAML.
        /// </summary>
        public ExportChoiceDialog
            (
            )
        {
            InitializeComponent();
        }

        /// <summary>
        /// Subscribes to view model close token after load.
        /// </summary>
        /// <param name="e">Load event arguments.</param>
        protected override void OnLoaded
            (
            RoutedEventArgs e
            )
        {
            base.OnLoaded(e);
            if (DataContext is ExportChoiceViewModel Vm)
            {
                // Close the modal when the user picks an export kind or cancels.
                Vm.PropertyChanged += (_, Args) =>
                {
                    if (Args.PropertyName == nameof(ExportChoiceViewModel.CloseToken))
                        Close();
                };
            }
        }
    }
}
