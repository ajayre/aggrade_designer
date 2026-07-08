using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// One navigable section in the haul configuration dialog sidebar.
    /// </summary>
    public sealed partial class HaulSectionItemViewModel : ViewModelBase
    {
        /// <summary>Parent haul dialog that owns section selection state.</summary>
        readonly HaulDialogViewModel _Owner;

        /// <summary>
        /// Creates a sidebar entry for the given haul settings section.
        /// </summary>
        /// <param name="Owner">Parent haul dialog view model.</param>
        /// <param name="Kind">Section identifier.</param>
        /// <param name="Title">Display label shown in the sidebar.</param>
        public HaulSectionItemViewModel
            (
            HaulDialogViewModel Owner,
            HaulDialogViewModel.HaulSectionKind Kind,
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
        public HaulDialogViewModel.HaulSectionKind Kind { get; }

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
        /// Selects this section in the parent haul dialog.
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
