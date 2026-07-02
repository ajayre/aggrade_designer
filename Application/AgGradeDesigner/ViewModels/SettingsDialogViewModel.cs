using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// View model for the settings dialog.
    /// </summary>
    public sealed partial class SettingsDialogViewModel : ViewModelBase
    {
        /// <summary>
        /// Whether the user accepted the dialog.
        /// </summary>
        [ObservableProperty]
        private bool _Confirmed;

        /// <summary>
        /// Incremented to signal the view to close the dialog.
        /// </summary>
        [ObservableProperty]
        private int _CloseToken;

        /// <summary>
        /// Accepts and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void Ok
            (
            )
        {
            Confirmed = true;
            CloseToken++;
        }

        /// <summary>
        /// Cancels and closes the dialog.
        /// </summary>
        [RelayCommand]
        private void Cancel
            (
            )
        {
            Confirmed = false;
            CloseToken++;
        }
    }
}
