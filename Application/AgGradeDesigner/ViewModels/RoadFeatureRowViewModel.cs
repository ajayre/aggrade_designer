using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// One road row in the Features palette.
    /// </summary>
    public sealed partial class RoadFeatureRowViewModel : ViewModelBase
    {
        /// <summary>Called when the user edits the crown width text.</summary>
        private readonly Action<string> _WidthEdited;

        /// <summary>Called when the user clicks Edit for this road.</summary>
        private readonly Action<string> _Edit;

        /// <summary>Called when the user confirms Delete for this road.</summary>
        private readonly Func<string, Task> _Delete;

        /// <summary>True while the view model is assigning width text, so the write-back is skipped.</summary>
        private bool _SuppressWidthWrite;

        /// <summary>
        /// Creates a road row.
        /// </summary>
        /// <param name="Id">Committed road id, or a draft token.</param>
        /// <param name="StartText">Start latitude and longitude label.</param>
        /// <param name="EndText">End latitude and longitude label.</param>
        /// <param name="CrownWidthText">Crown width in display units.</param>
        /// <param name="LengthUnit">Unit suffix, ft or m.</param>
        /// <param name="ShowEditButton">True when Edit should be shown.</param>
        /// <param name="IsLiveEdit">True when this row follows dragged centreline ends.</param>
        /// <param name="IsAlternate">True for the second, fourth, and later stripe.</param>
        /// <param name="CanEditSize">True only while this road itself is being edited.</param>
        /// <param name="Edit">Edit command handler.</param>
        /// <param name="Delete">Delete command handler. Runs after the user confirms.</param>
        /// <param name="WidthEdited">Crown width text handler.</param>
        public RoadFeatureRowViewModel
            (
            string Id,
            string StartText,
            string EndText,
            string CrownWidthText,
            string LengthUnit,
            bool ShowEditButton,
            bool IsLiveEdit,
            bool IsAlternate,
            bool CanEditSize,
            Action<string> Edit,
            Func<string, Task> Delete,
            Action<string> WidthEdited
            )
        {
            this.Id = Id;
            this.LengthUnit = LengthUnit;
            this.ShowEditButton = ShowEditButton;
            this.IsLiveEdit = IsLiveEdit;
            this.IsAlternate = IsAlternate;
            this.CanEditSize = CanEditSize;
            _StartText = StartText;
            _EndText = EndText;
            _Edit = Edit;
            _Delete = Delete;
            _WidthEdited = WidthEdited;
            _SuppressWidthWrite = true;
            this.CrownWidthText = CrownWidthText;
            _SuppressWidthWrite = false;
        }

        /// <summary>Committed road id, or a draft token.</summary>
        public string Id { get; }

        /// <summary>Unit suffix shown beside the crown width.</summary>
        public string LengthUnit { get; }

        /// <summary>True when the Edit button is shown.</summary>
        public bool ShowEditButton { get; }

        /// <summary>True when map drags should refresh this row's end coordinates.</summary>
        public bool IsLiveEdit { get; }

        /// <summary>True when this row uses the alternate list stripe.</summary>
        public bool IsAlternate { get; }

        /// <summary>True when the crown-width box accepts typing. Other roads stay read-only.</summary>
        public bool CanEditSize { get; }

        /// <summary>Start latitude and longitude label.</summary>
        [ObservableProperty]
        private string _StartText;

        /// <summary>End latitude and longitude label.</summary>
        [ObservableProperty]
        private string _EndText;

        /// <summary>Crown width in display units.</summary>
        [ObservableProperty]
        private string _CrownWidthText;

        /// <summary>
        /// Writes a user-edited crown width back to the draft or the committed road.
        /// </summary>
        /// <param name="Value">New text box contents.</param>
        partial void OnCrownWidthTextChanged
            (
            string Value
            )
        {
            if (_SuppressWidthWrite)
                return;

            _WidthEdited(Value);
        }

        /// <summary>
        /// Starts edit mode for this road.
        /// </summary>
        [RelayCommand]
        private void Edit
            (
            )
        {
            _Edit(Id);
        }

        /// <summary>
        /// Deletes this road after the palette asks for confirmation.
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
