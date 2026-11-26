using AgGradeDesigner.ViewModels;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgGradeDesigner.Views
{
    /// <summary>
    /// Well coordinate dialog code-behind; closes when the view model signals completion.
    /// </summary>
    public partial class WellCoordinateDialog : Window
    {
        /// <summary>
        /// Initializes dialog XAML.
        /// </summary>
        public WellCoordinateDialog
            (
            )
        {
            InitializeComponent();
        }

        /// <summary>
        /// Subscribes to the view model close token after load.
        /// </summary>
        /// <param name="e">Load event arguments.</param>
        protected override void OnLoaded
            (
            RoutedEventArgs e
            )
        {
            base.OnLoaded(e);
            if (DataContext is WellCoordinateDialogViewModel Vm)
            {
                // Close the modal when Add accepts a location or Cancel drops the request.
                Vm.PropertyChanged += (_, Args) =>
                {
                    if (Args.PropertyName == nameof(WellCoordinateDialogViewModel.CloseToken))
                        Close();
                };
            }
        }
    }
}
