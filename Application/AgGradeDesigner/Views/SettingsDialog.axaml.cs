using AgGradeDesigner.ViewModels;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgGradeDesigner.Views
{
    /// <summary>
    /// Settings dialog code-behind; closes when the view model signals completion.
    /// </summary>
    public partial class SettingsDialog : Window
    {
        /// <summary>
        /// Initializes dialog XAML.
        /// </summary>
        public SettingsDialog
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
            if (DataContext is SettingsDialogViewModel Vm)
            {
                Vm.PropertyChanged += (_, Args) =>
                {
                    if (Args.PropertyName == nameof(SettingsDialogViewModel.CloseToken))
                        Close();
                };
            }
        }
    }
}
