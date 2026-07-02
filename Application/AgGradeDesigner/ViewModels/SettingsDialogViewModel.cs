using System.Collections.ObjectModel;

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
        /// Identifies a settings dialog content section.
        /// </summary>
        public enum SettingsSectionKind
        {
            Ai
        }

        /// <summary>
        /// Initializes sidebar sections and selects the first entry.
        /// </summary>
        public SettingsDialogViewModel
            (
            )
        {
            SettingsSectionItemViewModel AiSection = new SettingsSectionItemViewModel(this, SettingsSectionKind.Ai, "AI");
            Sections = new ReadOnlyCollection<SettingsSectionItemViewModel>([AiSection]);
            SelectedSection = SettingsSectionKind.Ai;
            AiSection.IsSelected = true;
        }

        /// <summary>
        /// Sidebar entries for choosing a settings section.
        /// </summary>
        public ReadOnlyCollection<SettingsSectionItemViewModel> Sections { get; }

        /// <summary>
        /// Currently displayed settings section.
        /// </summary>
        [ObservableProperty]
        private SettingsSectionKind _SelectedSection;

        /// <summary>
        /// Whether the AI section is currently displayed.
        /// </summary>
        public bool IsAiSectionSelected => SelectedSection == SettingsSectionKind.Ai;

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
        /// Switches the visible settings section and updates sidebar selection.
        /// </summary>
        /// <param name="Kind">Section to display.</param>
        internal void SelectSection
            (
            SettingsSectionKind Kind
            )
        {
            SelectedSection = Kind;
            foreach (SettingsSectionItemViewModel Section in Sections)
                Section.IsSelected = Section.Kind == Kind;
            OnPropertyChanged(nameof(IsAiSectionSelected));
        }

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
