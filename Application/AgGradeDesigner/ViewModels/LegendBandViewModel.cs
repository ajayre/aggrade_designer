using Avalonia.Media;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// One row in the nine-band elevation legend sidebar.
    /// </summary>
    public sealed class LegendBandViewModel : ViewModelBase
    {
        private string _Name = "";
        private string _BoundaryText = "";
        private IBrush _Swatch = Brushes.Gray;

        /// <summary>
        /// Band label shown beside the colour swatch.
        /// </summary>
        public string Name
        {
            get => _Name;
            set => SetProperty(ref _Name, value);
        }

        /// <summary>
        /// Formatted elevation boundary for this band.
        /// </summary>
        public string BoundaryText
        {
            get => _BoundaryText;
            set => SetProperty(ref _BoundaryText, value);
        }

        /// <summary>
        /// Fill brush matching the band colour in the field viewer.
        /// </summary>
        public IBrush Swatch
        {
            get => _Swatch;
            set => SetProperty(ref _Swatch, value);
        }
    }
}
