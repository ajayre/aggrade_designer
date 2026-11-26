using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AgGradeDesigner.Helpers;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// One well row in the Features palette.
    /// </summary>
    public sealed partial class WellFeatureRowViewModel : ViewModelBase
    {
        /// <summary>Called when the user edits the pad diameter text.</summary>
        private readonly Action<string> _DiameterEdited;

        /// <summary>Called when the user edits the latitude or longitude text.</summary>
        private readonly Action<string, string> _LocationEdited;

        /// <summary>Called when the user clicks Edit for this well.</summary>
        private readonly Action<string> _Edit;

        /// <summary>Called when the user confirms Delete for this well.</summary>
        private readonly Func<string, Task> _Delete;

        /// <summary>True while the view model is assigning diameter text, so the write-back is skipped.</summary>
        private bool _SuppressDiameterWrite;

        /// <summary>True while the view model is assigning latitude or longitude, so the write-back is skipped.</summary>
        private bool _SuppressLocationWrite;

        /// <summary>
        /// Creates a well row.
        /// </summary>
        /// <param name="Id">Committed well id, or a draft token.</param>
        /// <param name="CoordinateText">Latitude and longitude label for a draft row.</param>
        /// <param name="LatitudeText">Latitude in decimal degrees.</param>
        /// <param name="LongitudeText">Longitude in decimal degrees.</param>
        /// <param name="PadDiameterText">Pad diameter in display units.</param>
        /// <param name="LengthUnit">Unit suffix, ft or m.</param>
        /// <param name="ShowEditButton">True when Edit should be shown.</param>
        /// <param name="IsLiveEdit">True when this is the well being edited. The row is highlighted.</param>
        /// <param name="IsAlternate">True for the second, fourth, and later stripe.</param>
        /// <param name="CanEditSize">True only while this well itself is being edited. Latitude, longitude, and diameter then accept typing.</param>
        /// <param name="ShowDiameter">True when the diameter is listed on the row.</param>
        /// <param name="Edit">Edit command handler.</param>
        /// <param name="Delete">Delete command handler. Runs after the user confirms.</param>
        /// <param name="DiameterEdited">Diameter text handler.</param>
        /// <param name="LocationEdited">Latitude and longitude text handler.</param>
        public WellFeatureRowViewModel
            (
            string Id,
            string CoordinateText,
            string LatitudeText,
            string LongitudeText,
            string PadDiameterText,
            string LengthUnit,
            bool ShowEditButton,
            bool IsLiveEdit,
            bool IsAlternate,
            bool CanEditSize,
            bool ShowDiameter,
            Action<string> Edit,
            Func<string, Task> Delete,
            Action<string> DiameterEdited,
            Action<string, string> LocationEdited
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
            _LocationEdited = LocationEdited;
            _SuppressDiameterWrite = true;
            _SuppressLocationWrite = true;
            this.PadDiameterText = PadDiameterText;
            this.LatitudeText = LatitudeText;
            this.LongitudeText = LongitudeText;
            _SuppressDiameterWrite = false;
            _SuppressLocationWrite = false;
        }

        /// <summary>Committed well id, or a draft token.</summary>
        public string Id { get; }

        /// <summary>Unit suffix shown beside the diameter.</summary>
        public string LengthUnit { get; }

        /// <summary>True when the Edit button is shown.</summary>
        public bool ShowEditButton { get; }

        /// <summary>True when this is the well being edited. The row is highlighted, and map drags refresh its coordinates.</summary>
        public bool IsLiveEdit { get; }

        /// <summary>True when this row uses the alternate list stripe.</summary>
        public bool IsAlternate { get; }

        /// <summary>True when latitude, longitude, and diameter accept typing. Other wells stay read-only.</summary>
        public bool CanEditSize { get; }

        /// <summary>True when the diameter is listed on the row. Wells being added use the header editor instead.</summary>
        public bool ShowDiameter { get; }

        /// <summary>Latitude and longitude label for a well that is still being added.</summary>
        [ObservableProperty]
        private string _CoordinateText;

        /// <summary>Latitude in decimal degrees.</summary>
        [ObservableProperty]
        private string _LatitudeText;

        /// <summary>Longitude in decimal degrees.</summary>
        [ObservableProperty]
        private string _LongitudeText;

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
        /// Writes a user-edited latitude back to the well being edited.
        /// </summary>
        /// <param name="Value">New text box contents.</param>
        partial void OnLatitudeTextChanged
            (
            string Value
            )
        {
            PublishLocation();
        }

        /// <summary>
        /// Writes a user-edited longitude back to the well being edited.
        /// </summary>
        /// <param name="Value">New text box contents.</param>
        partial void OnLongitudeTextChanged
            (
            string Value
            )
        {
            PublishLocation();
        }

        /// <summary>
        /// Shows a latitude and longitude chosen by a map drag without treating that as typing.
        /// Text that already rounds to the same coordinate is left alone so typing is not rewritten.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        public void SetLocation
            (
            double Latitude,
            double Longitude
            )
        {
            _SuppressLocationWrite = true;
            string NextLatitude = CoordinateFormat.FormatLatitude(Latitude);
            string NextLongitude = CoordinateFormat.FormatLongitude(Longitude);
            if (!ShowsSameCoordinate(LatitudeText, NextLatitude, true))
                LatitudeText = NextLatitude;
            if (!ShowsSameCoordinate(LongitudeText, NextLongitude, false))
                LongitudeText = NextLongitude;
            _SuppressLocationWrite = false;
        }

        /// <summary>
        /// Sends the current latitude and longitude text when the user typed it.
        /// </summary>
        private void PublishLocation
            (
            )
        {
            if (_SuppressLocationWrite || !CanEditSize)
                return;

            _LocationEdited(LatitudeText, LongitudeText);
        }

        /// <summary>
        /// True when the box already shows the formatted coordinate, even with fewer decimal places.
        /// </summary>
        /// <param name="Current">Text currently in the box.</param>
        /// <param name="Formatted">Text the map location would show.</param>
        /// <param name="Latitude">True to parse a latitude. False parses a longitude.</param>
        /// <returns>True when both texts are the same coordinate at display precision.</returns>
        static private bool ShowsSameCoordinate
            (
            string Current,
            string Formatted,
            bool Latitude
            )
        {
            if (string.Equals(Current, Formatted, StringComparison.Ordinal))
                return true;

            bool CurrentParsed;
            double Typed;
            bool FormattedParsed;
            double Shown;
            if (Latitude)
            {
                CurrentParsed = CoordinateFormat.TryParseLatitude(Current, out Typed);
                FormattedParsed = CoordinateFormat.TryParseLatitude(Formatted, out Shown);
            }
            else
            {
                CurrentParsed = CoordinateFormat.TryParseLongitude(Current, out Typed);
                FormattedParsed = CoordinateFormat.TryParseLongitude(Formatted, out Shown);
            }

            if (!CurrentParsed || !FormattedParsed)
                return false;

            return CoordinateFormat.RoundCoordinate(Typed) == CoordinateFormat.RoundCoordinate(Shown);
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
