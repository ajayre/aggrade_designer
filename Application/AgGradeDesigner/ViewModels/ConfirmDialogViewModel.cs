using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Confirmation or message dialog (yes/no questions or OK-only alerts).
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
        /// When true, shows Yes/No buttons; otherwise shows OK only.
        /// </summary>
        [ObservableProperty]
        private bool _IsQuestion = true;

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
            CloseToken++;
        }

        /// <summary>
        /// Dismisses an OK-only message dialog.
        /// </summary>
        [RelayCommand]
        private void Ok
            (
            )
        {
            Confirmed = true;
            CloseToken++;
        }
    }
}
