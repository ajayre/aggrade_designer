using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Simple confirmation dialog (overwrite, add/replace, etc.).
    /// </summary>
    public sealed partial class ConfirmDialogViewModel : ViewModelBase
    {
        /// <summary>
        /// Dialog title text shown in the window chrome.
        /// </summary>
        [ObservableProperty]
        private string _Title = "Confirm";

        /// <summary>
        /// Body message presented to the user.
        /// </summary>
        [ObservableProperty]
        private string _Message = "";

        /// <summary>
        /// Whether the user confirmed (true) or declined (false).
        /// </summary>
        [ObservableProperty]
        private bool _Confirmed;

        /// <summary>
        /// Incremented to signal the view to close the dialog.
        /// </summary>
        [ObservableProperty]
        private int _CloseToken;

        /// <summary>
        /// Records affirmative confirmation and signals the view to close.
        /// </summary>
        [RelayCommand]
        private void Yes
            (
            )
        {
            Confirmed = true;
            // Bump close token so the dialog code-behind closes the window.
            CloseToken++;
        }

        /// <summary>
        /// Records negative confirmation and signals the view to close.
        /// </summary>
        [RelayCommand]
        private void No
            (
            )
        {
            Confirmed = false;
            // Bump close token so the dialog code-behind closes the window.
            CloseToken++;
        }
    }
}
