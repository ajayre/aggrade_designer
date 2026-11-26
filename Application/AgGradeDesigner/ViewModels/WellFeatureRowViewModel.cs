using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// One well row in the Features palette.
    /// </summary>
    public sealed partial class WellFeatureRowViewModel : ViewModelBase
    {
        /// <summary>Called when the user edits the pad diameter text.</summary>
        private readonly Action<string> _DiameterEdited;

        /// <summary>Called when the user clicks Edit for this well.</summary>
        private readonly Action<string> _Edit;

        /// <summary>Called when the user confirms Delete for this well.</summary>
        private readonly Func<string, Task> _Delete;

        /// <summary>True while the view model is assigning diameter text, so the write-back is skipped.</summary>
        private bool _SuppressDiameterWrite;

        /// <summary>
        /// Creates a well row.
        /// </summary>
        /// <param name="Id">Committed well id, or a draft token.</param>
        /// <param name="CoordinateText">Latitude and longitude label.</param>
        /// <param name="PadDiameterText">Pad diameter in display units.</param>
        /// <param name="LengthUnit">Unit suffix, ft or m.</param>
        /// <param name="ShowEditButton">True when Edit should be shown.</param>
        /// <param name="IsLiveEdit">True when this row follows a dragged pad centre.</param>
        /// <param name="IsAlternate">True for the second, fourth, and later stripe.</param>
        /// <param name="CanEditSize">True only while this well itself is being edited.</param>
        /// <param name="ShowDiameter">True when the diameter is listed on the row.</param>
        /// <param name="Edit">Edit command handler.</param>
        /// <param name="Delete">Delete command handler. Runs after the user confirms.</param>
        /// <param name="DiameterEdited">Diameter text handler.</param>
        public WellFeatureRowViewModel
            (
            string Id,
            string CoordinateText,
            string PadDiameterText,
            string LengthUnit,
            bool ShowEditButton,
            bool IsLiveEdit,
            bool IsAlternate,
            bool CanEditSize,
            bool ShowDiameter,
            Action<string> Edit,
            Func<string, Task> Delete,
            Action<string> DiameterEdited
            )
        {
            this.Id = Id;
            this.LengthUnit = LengthUnit;
            this.ShowEditButton = ShowEditButton;
            this.IsLiveEdit = IsLiveEdit;
            this.IsAlternate = IsAlternate;
            this.CanEditSize = CanEditSize;
            this.ShowDiameter = ShowDiameter;
            _CoordinateText = CoordinateText;
            _Edit = Edit;
            _Delete = Delete;
            _DiameterEdited = DiameterEdited;
            _SuppressDiameterWrite = true;
            this.PadDiameterText = PadDiameterText;
            _SuppressDiameterWrite = false;
        }

        /// <summary>Committed well id, or a draft token.</summary>
        public string Id { get; }

        /// <summary>Unit suffix shown beside the diameter.</summary>
        public string LengthUnit { get; }

        /// <summary>True when the Edit button is shown.</summary>
        public bool ShowEditButton { get; }

        /// <summary>True when map drags should refresh this row's coordinates.</summary>
        public bool IsLiveEdit { get; }

        /// <summary>True when this row uses the alternate list stripe.</summary>
        public bool IsAlternate { get; }

        /// <summary>True when the diameter box accepts typing. Other wells stay read-only.</summary>
        public bool CanEditSize { get; }

        /// <summary>True when the diameter is listed on the row. Wells being added use the header editor instead.</summary>
        public bool ShowDiameter { get; }

        /// <summary>Latitude and longitude label.</summary>
        [ObservableProperty]
        private string _CoordinateText;

        /// <summary>Pad diameter in display units.</summary>
        [ObservableProperty]
        private string _PadDiameterText;

        /// <summary>
        /// Writes a user-edited diameter back to the draft or the committed well.
        /// </summary>
        /// <param name="Value">New text box contents.</param>
        partial void OnPadDiameterTextChanged
            (
            string Value
            )
        {
            if (_SuppressDiameterWrite)
                return;

            _DiameterEdited(Value);
        }

        /// <summary>
        /// Starts edit mode for this well.
        /// </summary>
        [RelayCommand]
        private void Edit
            (
            )
        {
            _Edit(Id);
        }

        /// <summary>
        /// Deletes this well after the palette asks for confirmation.
        /// </summary>
        [RelayCommand]
        private Task Delete
            (
            )
        {
            return _Delete(Id);
        }
    }
}
