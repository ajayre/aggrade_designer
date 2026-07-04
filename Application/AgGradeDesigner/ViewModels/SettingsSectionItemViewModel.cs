using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// One navigable section in the settings dialog sidebar.
    /// </summary>
    public sealed partial class SettingsSectionItemViewModel : ViewModelBase
    {
        /// <summary>Parent settings dialog that owns section selection state.</summary>
        readonly SettingsDialogViewModel _Owner;

        /// <summary>
        /// Creates a sidebar entry for the given settings section.
        /// </summary>
        /// <param name="Owner">Parent settings dialog view model.</param>
        /// <param name="Kind">Section identifier.</param>
        /// <param name="Title">Display label shown in the sidebar.</param>
        public SettingsSectionItemViewModel
            (
            SettingsDialogViewModel Owner,
            SettingsDialogViewModel.SettingsSectionKind Kind,
            string Title
            )
        {
            _Owner = Owner;
            this.Kind = Kind;
            this.Title = Title;
        }

        /// <summary>
        /// Section identifier for this sidebar entry.
        /// </summary>
        public SettingsDialogViewModel.SettingsSectionKind Kind { get; }

        /// <summary>
        /// Display label shown in the sidebar.
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// Whether this section is currently selected.
        /// </summary>
        [ObservableProperty]
        private bool _IsSelected;

        /// <summary>
        /// Selects this section in the parent settings dialog.
        /// </summary>
        [RelayCommand]
        private void Select
            (
            )
        {
            _Owner.SelectSection(Kind);
        }
    }
}
