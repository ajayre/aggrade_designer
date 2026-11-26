using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// One road row in the Features palette.
    /// </summary>
    public sealed partial class RoadFeatureRowViewModel : ViewModelBase
    {
        /// <summary>Called when the user clicks Edit for this road.</summary>
        private readonly Action<string> _Edit;

        /// <summary>Called when the user confirms Delete for this road.</summary>
        private readonly Func<string, Task> _Delete;

        /// <summary>
        /// Creates a road row.
        /// </summary>
        /// <param name="Id">Committed road id, or a draft token.</param>
        /// <param name="StartText">Start latitude and longitude label.</param>
        /// <param name="EndText">End latitude and longitude label.</param>
        /// <param name="BedWidthText">Road bed width in display units.</param>
        /// <param name="HeightText">Embankment height in display units.</param>
        /// <param name="SideSlopeText">Side slope number, without the ":1" suffix.</param>
        /// <param name="LengthUnit">Unit suffix, ft or m.</param>
        /// <param name="ShowEditButton">True when Edit should be shown.</param>
        /// <param name="IsLiveEdit">True when this row follows dragged centreline ends.</param>
        /// <param name="IsAlternate">True for the second, fourth, and later stripe.</param>
        /// <param name="ShowParameters">True when the saved bed, height, and slope are shown on the row.</param>
        /// <param name="Edit">Edit command handler.</param>
        /// <param name="Delete">Delete command handler. Runs after the user confirms.</param>
        public RoadFeatureRowViewModel
            (
            string Id,
            string StartText,
            string EndText,
            string BedWidthText,
            string HeightText,
            string SideSlopeText,
            string LengthUnit,
            bool ShowEditButton,
            bool IsLiveEdit,
            bool IsAlternate,
            bool ShowParameters,
            Action<string> Edit,
            Func<string, Task> Delete
            )
        {
            this.Id = Id;
            this.BedWidthText = BedWidthText;
            this.HeightText = HeightText;
            this.SideSlopeText = SideSlopeText;
            this.LengthUnit = LengthUnit;
            this.ShowEditButton = ShowEditButton;
            this.IsLiveEdit = IsLiveEdit;
            this.IsAlternate = IsAlternate;
            this.ShowParameters = ShowParameters;
            _StartText = StartText;
            _EndText = EndText;
            _Edit = Edit;
            _Delete = Delete;
        }

        /// <summary>Committed road id, or a draft token.</summary>
        public string Id { get; }

        /// <summary>Road bed width in display units.</summary>
        public string BedWidthText { get; }

        /// <summary>Embankment height in display units.</summary>
        public string HeightText { get; }

        /// <summary>Side slope number, without the ":1" suffix.</summary>
        public string SideSlopeText { get; }

        /// <summary>Unit suffix shown beside the bed width and height.</summary>
        public string LengthUnit { get; }

        /// <summary>True when the Edit button is shown.</summary>
        public bool ShowEditButton { get; }

        /// <summary>True when map drags should refresh this row's end coordinates.</summary>
        public bool IsLiveEdit { get; }

        /// <summary>True when this row uses the alternate list stripe.</summary>
        public bool IsAlternate { get; }

        /// <summary>True when the saved cross-section is listed on the row. The road being drawn uses the header editors instead.</summary>
        public bool ShowParameters { get; }

        /// <summary>Start latitude and longitude label.</summary>
        [ObservableProperty]
        private string _StartText;

        /// <summary>End latitude and longitude label.</summary>
        [ObservableProperty]
        private string _EndText;

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
