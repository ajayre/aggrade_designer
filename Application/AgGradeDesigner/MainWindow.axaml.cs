using AgGradeDesigner.Controls;
using AgGradeDesigner.ViewModels;

using Avalonia.Controls;

namespace AgGradeDesigner
{
    /// <summary>
    /// Primary application window hosting the field viewer and toolbar bindings.
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Initializes the window, view model, and field viewer event wiring.
        /// </summary>
        public MainWindow
            (
            )
        {
            InitializeComponent();
            MainWindowViewModel Vm = new MainWindowViewModel();
            DataContext = Vm;

            // Keep status bar coordinates in sync when the user pans or zooms the field viewer.
            FieldViewerControl? Viewer = this.FindControl<FieldViewerControl>("FieldViewer");
            if (Viewer != null)
            {
                Viewer.ViewCentreChanged += (_, _) =>
                {
                    Vm.OnViewCentreChanged(Viewer.CentreLatitude, Viewer.CentreLongitude, Viewer.ZoomFactor);
                    Vm.OnOrbitCentreChanged(Viewer.OrbitCentreLatitude, Viewer.OrbitCentreLongitude);
                };
            }

            // Invalidate Skia surface or visual layers when project or display bindings change.
            Vm.PropertyChanged += (_, Args) =>
            {
                if (Viewer == null)
                    return;

                string? Name = Args.PropertyName;
                if (Name == nameof(MainWindowViewModel.Project))
                {
                    Viewer.InvalidateSurface();
                    if (Vm.HasSurvey)
                        Viewer.InvalidateVisual();
                }

                if (Name == nameof(MainWindowViewModel.HasSurvey) ||
                    Name == nameof(MainWindowViewModel.HasDesign))
                {
                    Viewer.InvalidateSurface();
                }

                if (Name == nameof(MainWindowViewModel.Display) ||
                    Name == nameof(MainWindowViewModel.CentreLat) ||
                    Name == nameof(MainWindowViewModel.CentreLon) ||
                    Name == nameof(MainWindowViewModel.Zoom) ||
                    Name == nameof(MainWindowViewModel.SatelliteToggle) ||
                    Name == nameof(MainWindowViewModel.SurveyPointsToggle) ||
                    Name == nameof(MainWindowViewModel.ContoursToggle) ||
                    Name == nameof(MainWindowViewModel.HillshadeToggle) ||
                    Name == nameof(MainWindowViewModel.BenchmarksToggle) ||
                    Name == nameof(MainWindowViewModel.DesignOverlayToggle) ||
                    Name == nameof(MainWindowViewModel.VerticalExaggeration) ||
                    Name == nameof(MainWindowViewModel.ViewMode) ||
                    Name == nameof(MainWindowViewModel.FieldContentView))
                {
                    Viewer.InvalidateVisual();
                }
            };
        }
    }
}
