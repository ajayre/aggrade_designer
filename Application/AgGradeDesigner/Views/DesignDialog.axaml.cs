using AgGradeDesigner.ViewModels;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgGradeDesigner.Views
{
    /// <summary>
    /// Design parameters dialog code-behind; closes when the view model signals completion.
    /// </summary>
    public partial class DesignDialog : Window
    {
        /// <summary>
        /// Initializes dialog XAML.
        /// </summary>
        public DesignDialog
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
            if (DataContext is DesignDialogViewModel Vm)
            {
                // Close the modal when the user confirms or cancels.
                Vm.PropertyChanged += (_, Args) =>
                {
                    if (Args.PropertyName == nameof(DesignDialogViewModel.CloseToken))
                        Close();
                };
            }
        }
    }
}
