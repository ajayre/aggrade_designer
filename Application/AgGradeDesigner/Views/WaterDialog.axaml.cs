using AgGradeDesigner.ViewModels;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgGradeDesigner.Views
{
    /// <summary>
    /// Water settings dialog code-behind; closes when the view model signals completion.
    /// </summary>
    public partial class WaterDialog : Window
    {
        /// <summary>
        /// Initializes dialog XAML.
        /// </summary>
        public WaterDialog
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
            if (DataContext is WaterDialogViewModel Vm)
            {
                // Close the modal when the user confirms or cancels.
                Vm.PropertyChanged += (_, Args) =>
                {
                    if (Args.PropertyName == nameof(WaterDialogViewModel.CloseToken))
                        Close();
                };
            }
        }
    }
}
