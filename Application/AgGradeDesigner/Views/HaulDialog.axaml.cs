using AgGradeDesigner.ViewModels;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgGradeDesigner.Views
{
    /// <summary>
    /// Haul configuration dialog code-behind; closes when the view model signals completion.
    /// </summary>
    public partial class HaulDialog : Window
    {
        /// <summary>
        /// Initializes dialog XAML.
        /// </summary>
        public HaulDialog
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
            if (DataContext is HaulDialogViewModel Vm)
            {
                Vm.PropertyChanged += (_, Args) =>
                {
                    if (Args.PropertyName == nameof(HaulDialogViewModel.CloseToken))
                        Close();
                };
            }
        }
    }
}
