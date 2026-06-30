using AgGradeDesigner.ViewModels;

using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AgGradeDesigner.Views
{
    /// <summary>
    /// Import choice dialog code-behind; closes when the view model signals completion.
    /// </summary>
    public partial class ImportChoiceDialog : Window
    {
        /// <summary>
        /// Initializes dialog XAML.
        /// </summary>
        public ImportChoiceDialog
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
            if (DataContext is ImportChoiceViewModel Vm)
            {
                // Close the modal when the user picks an import kind or cancels.
                Vm.PropertyChanged += (_, Args) =>
                {
                    if (Args.PropertyName == nameof(ImportChoiceViewModel.CloseToken))
                        Close();
                };
            }
        }
    }
}
