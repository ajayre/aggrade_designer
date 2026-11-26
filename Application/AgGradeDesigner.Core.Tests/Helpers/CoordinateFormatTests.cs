using AgGradeDesigner.Helpers;
using AgGradeDesigner.ViewModels;

namespace AgGradeDesigner.Core.Tests.Helpers
{
    /// <summary>
    /// Tests for typed well coordinates and the coordinate dialog.
    /// </summary>
    public sealed class CoordinateFormatTests
    {
        /// <summary>Latitude accepted by the dialog.</summary>
        private const double SAMPLE_LATITUDE = 40.1;

        /// <summary>Longitude accepted by the dialog.</summary>
        private const double SAMPLE_LONGITUDE = -93.4;

        /// <summary>Latitude one degree past the north pole, which is not a location.</summary>
        private const double LATITUDE_PAST_NORTH = 91.0;

        /// <summary>Longitude one degree past the date line, which is not a location.</summary>
        private const double LONGITUDE_PAST_EAST = 181.0;

        /// <summary>
        /// A typed latitude and longitude in range are accepted, including the poles and the date line.
        /// </summary>
        [Fact]
        public void TryParse_AcceptsCoordinatesInRange
            (
            )
        {
            Assert.True(CoordinateFormat.TryParseLatitude(" 40.1 ", out double Latitude));
            Assert.Equal(SAMPLE_LATITUDE, Latitude, 6);
            Assert.True(CoordinateFormat.TryParseLongitude("-93.4", out double Longitude));
            Assert.Equal(SAMPLE_LONGITUDE, Longitude, 6);
            Assert.True(CoordinateFormat.TryParseLatitude("-90", out double SouthPole));
            Assert.Equal(CoordinateFormat.MIN_LATITUDE_DEG, SouthPole);
            Assert.True(CoordinateFormat.TryParseLongitude("180", out double DateLine));
            Assert.Equal(CoordinateFormat.MAX_LONGITUDE_DEG, DateLine);
        }

        /// <summary>
        /// Blank text and values past the poles or the date line are refused.
        /// </summary>
        [Fact]
        public void TryParse_RejectsBlankAndOutOfRange
            (
            )
        {
            Assert.False(CoordinateFormat.TryParseLatitude("", out _));
            Assert.False(CoordinateFormat.TryParseLatitude("north", out _));
            Assert.False(CoordinateFormat.TryParseLatitude(LATITUDE_PAST_NORTH.ToString(System.Globalization.CultureInfo.InvariantCulture), out _));
            Assert.False(CoordinateFormat.TryParseLongitude(LONGITUDE_PAST_EAST.ToString(System.Globalization.CultureInfo.InvariantCulture), out _));
        }

        /// <summary>
        /// Add stores the location only when it is inside the survey. Cancel stores nothing.
        /// </summary>
        [Fact]
        public void Add_RequiresALocationInsideTheSurvey
            (
            )
        {
            WellCoordinateDialogViewModel Dialog = new WellCoordinateDialogViewModel(
                (Latitude, Longitude) => (Latitude > 0.0) && (Longitude < 0.0));

            Dialog.CancelCommand.Execute(null);
            Assert.False(Dialog.Confirmed);

            Dialog.LatitudeText = "north";
            Dialog.LongitudeText = "-93.4";
            Dialog.AddCommand.Execute(null);
            Assert.False(Dialog.Confirmed);
            Assert.True(Dialog.HasError);

            Dialog.LatitudeText = "40.1";
            Dialog.LongitudeText = "10";
            Dialog.AddCommand.Execute(null);
            Assert.False(Dialog.Confirmed);
            Assert.Equal("That location is outside the survey.", Dialog.ErrorText);

            Dialog.LatitudeText = "40.1";
            Dialog.LongitudeText = "-93.4";
            Dialog.AddCommand.Execute(null);
            Assert.True(Dialog.Confirmed);
            Assert.Equal(SAMPLE_LATITUDE, Dialog.Latitude, 6);
            Assert.Equal(SAMPLE_LONGITUDE, Dialog.Longitude, 6);
            Assert.False(Dialog.HasError);
        }
    }
}
