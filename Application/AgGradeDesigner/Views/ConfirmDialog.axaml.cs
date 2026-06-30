using AgGradeDesigner.ViewModels;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgGradeDesigner.Views
{
    /// <summary>
    /// Modal confirmation dialog code-behind; closes when the view model signals completion.
    /// </summary>
    public partial class ConfirmDialog : Window
    {
        /// <summary>
        /// Initializes dialog XAML.
        /// </summary>
        public ConfirmDialog
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
            if (DataContext is ConfirmDialogViewModel Vm)
            {
                // Close the modal when the user picks yes or no.
                Vm.PropertyChanged += (_, Args) =>
                {
                    if (Args.PropertyName == nameof(ConfirmDialogViewModel.CloseToken))
                        Close();
                };
            }
        }
    }
}
