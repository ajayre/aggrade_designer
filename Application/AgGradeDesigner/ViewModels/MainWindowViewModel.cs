using System.Collections.ObjectModel;
using System.Globalization;

using Avalonia.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AgGradeDesigner.Core.Color;
using AgGradeDesigner.Core.Design;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Rendering;
using AgGradeDesigner.Core.Survey;
using AgGradeDesigner.Helpers;
using AgGradeDesigner.Services;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Main window state: project, viewer bindings, toolbar and sidebar commands.
    /// </summary>
    public sealed partial class MainWindowViewModel : ViewModelBase
    {
        private const double DEFAULT_STATUS_LATITUDE = 36.0;
        private const double DEFAULT_STATUS_LONGITUDE = -90.0;
        private const double DEFAULT_VERTICAL_EXAGGERATION = 1.0;
        private const int DEFAULT_VIEWPORT_WIDTH = 800;
        private const int DEFAULT_VIEWPORT_HEIGHT = 600;
        private const double CAMERA_DISTANCE_EXTENT_FACTOR = 0.85;
        private const double CAMERA_DISTANCE_MIN_M = 80.0;
        private const double CAMERA_DISTANCE_MAX_M = 5000.0;
        private const int ARGB_ALPHA_SHIFT = 24;
        private const int ARGB_RED_SHIFT = 16;
        private const int ARGB_GREEN_SHIFT = 8;
        private const int ARGB_BYTE_MASK = 0xFF;

        private ProjectDocument _Project = new ProjectDocument();

        /// <summary>
        /// Window title derived from the project name and modified flag.
        /// </summary>
        [ObservableProperty]
        private string _ProjectTitle = "Untitled";

        /// <summary>
        /// Status bar latitude formatted for display.
        /// </summary>
        [ObservableProperty]
        private string _StatusLatitude = CoordinateFormat.FormatLatitude(DEFAULT_STATUS_LATITUDE);

        /// <summary>
        /// Status bar longitude formatted for display.
        /// </summary>
        [ObservableProperty]
        private string _StatusLongitude = CoordinateFormat.FormatLongitude(DEFAULT_STATUS_LONGITUDE);

        /// <summary>
        /// Whether the 2D map view mode toggle is active.
        /// </summary>
        [ObservableProperty]
        private bool _IsTwoD = true;

        /// <summary>
        /// Whether the 3D orbit view mode toggle is active.
        /// </summary>
        [ObservableProperty]
        private bool _IsThreeD;

        /// <summary>
        /// Whether the existing-surface content view button is highlighted.
        /// </summary>
        [ObservableProperty]
        private bool _IsExistingActive;

        /// <summary>
        /// Whether the proposed design content view button is highlighted.
        /// </summary>
        [ObservableProperty]
        private bool _IsProposedActive;

        /// <summary>
        /// Whether the cut/fill content view button is highlighted.
        /// </summary>
        [ObservableProperty]
        private bool _IsCutFillActive;

        /// <summary>
        /// Whether the current project includes survey data.
        /// </summary>
        [ObservableProperty]
        private bool _HasSurvey;

        /// <summary>
        /// Whether the current project includes a generated design.
        /// </summary>
        [ObservableProperty]
        private bool _HasDesign;

        /// <summary>
        /// Whether the design dialog command is enabled.
        /// </summary>
        [ObservableProperty]
        private bool _CanDesign;

        /// <summary>
        /// Whether the proposed surface view can be selected.
        /// </summary>
        [ObservableProperty]
        private bool _CanProposedView;

        /// <summary>
        /// Whether the cut/fill comparison view can be selected.
        /// </summary>
        [ObservableProperty]
        private bool _CanCutFillView;

        /// <summary>
        /// Whether the elevation legend sidebar is visible.
        /// </summary>
        [ObservableProperty]
        private bool _ShowLegend;

        /// <summary>
        /// Whether the vertical exaggeration slider is visible (3D mode).
        /// </summary>
        [ObservableProperty]
        private bool _ShowVerticalExaggeration;

        /// <summary>
        /// Vertical exaggeration multiplier bound to the 3D viewer.
        /// </summary>
        [ObservableProperty]
        private double _VerticalExaggeration = DEFAULT_VERTICAL_EXAGGERATION;

        /// <summary>
        /// Formatted vertical exaggeration label for the status bar.
        /// </summary>
        [ObservableProperty]
        private string _VerticalExaggerationLabel = "1.0×";

        /// <summary>
        /// Survey point overlay visibility toggle.
        /// </summary>
        [ObservableProperty]
        private bool _SurveyPointsToggle = true;

        /// <summary>
        /// Contour overlay visibility toggle.
        /// </summary>
        [ObservableProperty]
        private bool _ContoursToggle = true;

        /// <summary>
        /// Hillshade overlay visibility toggle.
        /// </summary>
        [ObservableProperty]
        private bool _HillshadeToggle = true;

        /// <summary>
        /// Benchmark marker overlay visibility toggle.
        /// </summary>
        [ObservableProperty]
        private bool _BenchmarksToggle = true;

        /// <summary>
        /// Proposed design overlay visibility toggle in non-proposed views.
        /// </summary>
        [ObservableProperty]
        private bool _DesignOverlayToggle = true;

        /// <summary>
        /// Satellite basemap visibility toggle.
        /// </summary>
        [ObservableProperty]
        private bool _SatelliteToggle;

        /// <summary>
        /// Whether the benchmarks overlay toggle is enabled (benchmarks present).
        /// </summary>
        [ObservableProperty]
        private bool _BenchmarksToggleEnabled;

        /// <summary>
        /// Legend colour bands for the active field content view.
        /// </summary>
        public ObservableCollection<LegendBandViewModel> LegendBands { get; } = new ObservableCollection<LegendBandViewModel>();

        /// <summary>
        /// The open project document shared with persistence and rendering.
        /// </summary>
        public ProjectDocument Project => _Project;

        /// <summary>
        /// Current 2D or 3D viewer mode from project viewer state.
        /// </summary>
        public ViewMode ViewMode => _Project.ViewerState.ViewMode;

        /// <summary>
        /// Active field surface view (existing, proposed, cut/fill).
        /// </summary>
        public FieldContentView FieldContentView => _Project.ViewerState.FieldContentView;

        /// <summary>
        /// Layer and overlay display settings bound to the map viewer.
        /// </summary>
        public DisplaySettings Display => _Project.ViewerState.Display;

        /// <summary>
        /// Map centre latitude in degrees (WGS84).
        /// </summary>
        public double CentreLat => _Project.ViewerState.Camera2D.CentreLat;

        /// <summary>
        /// Map centre longitude in degrees (WGS84).
        /// </summary>
        public double CentreLon => _Project.ViewerState.Camera2D.CentreLon;

        /// <summary>
        /// Map zoom factor for the 2D camera.
        /// </summary>
        public double Zoom => _Project.ViewerState.Camera2D.Zoom;

        /// <summary>
        /// 3D orbit heading in degrees.
        /// </summary>
        public double OrbitHeading => _Project.ViewerState.Camera3D.OrbitHeadingDeg;

        /// <summary>
        /// 3D orbit pitch in degrees.
        /// </summary>
        public double OrbitPitch => _Project.ViewerState.Camera3D.OrbitPitchDeg;

        /// <summary>
        /// 3D camera distance from the orbit pivot in metres.
        /// </summary>
        public double OrbitDistance => _Project.ViewerState.Camera3D.DistanceM;

        /// <summary>
        /// 3D orbit pivot latitude in degrees.
        /// </summary>
        public double OrbitCentreLat => _Project.ViewerState.Camera3D.OrbitCentreLat;

        /// <summary>
        /// 3D orbit pivot longitude in degrees.
        /// </summary>
        public double OrbitCentreLon => _Project.ViewerState.Camera3D.OrbitCentreLon;

        /// <summary>
        /// Initializes the main window view model and synchronizes bindings from the default project.
        /// </summary>
        public MainWindowViewModel
            (
            )
        {
            RefreshAll();
        }

        /// <summary>
        /// Updates the 2D camera and status bar when the map view centre or zoom changes.
        /// </summary>
        /// <param name="Lat">New centre latitude in degrees.</param>
        /// <param name="Lon">New centre longitude in degrees.</param>
        /// <param name="Zoom">New zoom factor.</param>
        public void OnViewCentreChanged
            (
            double Lat,
            double Lon,
            double Zoom
            )
        {
            // Mirror camera centre and zoom into project state and status bar labels.
            _Project.ViewerState.Camera2D.CentreLat = Lat;
            _Project.ViewerState.Camera2D.CentreLon = Lon;
            _Project.ViewerState.Camera2D.Zoom = Zoom;
            StatusLatitude = CoordinateFormat.FormatLatitude(Lat);
            StatusLongitude = CoordinateFormat.FormatLongitude(Lon);
        }

        /// <summary>
        /// Fits 2D and 3D cameras to the project field bounds for the given viewport size.
        /// </summary>
        /// <param name="ViewportWidth">Viewport width in pixels.</param>
        /// <param name="ViewportHeight">Viewport height in pixels.</param>
        public void FitViewerCameraToField
            (
            int ViewportWidth = DEFAULT_VIEWPORT_WIDTH,
            int ViewportHeight = DEFAULT_VIEWPORT_HEIGHT
            )
        {
            if (!FieldBounds.TryFromProject(_Project, out FieldBounds Bounds))
                return;

            MapViewFitResult Fit = MapViewFitter.Fit(Bounds, ViewportWidth, ViewportHeight);
            _Project.ViewerState.Camera2D.CentreLat = Fit.CentreLat;
            _Project.ViewerState.Camera2D.CentreLon = Fit.CentreLon;
            _Project.ViewerState.Camera2D.Zoom = Fit.ZoomFactor;
            StatusLatitude = CoordinateFormat.FormatLatitude(Fit.CentreLat);
            StatusLongitude = CoordinateFormat.FormatLongitude(Fit.CentreLon);

            // Derive 3D distance from field extent so the whole site remains in frame.
            UTM.UTMCoordinate SouthWest = UTM.FromLatLon(Bounds.MinLat, Bounds.MinLon);
            UTM.UTMCoordinate NorthEast = UTM.FromLatLon(Bounds.MaxLat, Bounds.MaxLon);
            double ExtentM = Math.Max(
                Math.Abs(NorthEast.Easting - SouthWest.Easting),
                Math.Abs(NorthEast.Northing - SouthWest.Northing));
            _Project.ViewerState.Camera3D.DistanceM = Math.Clamp(
                ExtentM * CAMERA_DISTANCE_EXTENT_FACTOR,
                CAMERA_DISTANCE_MIN_M,
                CAMERA_DISTANCE_MAX_M);
            _Project.ViewerState.Camera3D.OrbitCentreLat = Fit.CentreLat;
            _Project.ViewerState.Camera3D.OrbitCentreLon = Fit.CentreLon;
            NotifyViewerProperties();
        }

        /// <summary>
        /// Updates the 3D orbit pivot stored in project viewer state.
        /// </summary>
        /// <param name="Lat">Orbit pivot latitude in degrees.</param>
        /// <param name="Lon">Orbit pivot longitude in degrees.</param>
        public void OnOrbitCentreChanged
            (
            double Lat,
            double Lon
            )
        {
            _Project.ViewerState.Camera3D.OrbitCentreLat = Lat;
            _Project.ViewerState.Camera3D.OrbitCentreLon = Lon;
        }

        /// <summary>
        /// Recentres the active view on the field geometric centre (clears pan offset from the prior mode).
        /// </summary>
        private void ResetViewCentreToField
            (
            )
        {
            if (!FieldBounds.TryFromProject(_Project, out FieldBounds Bounds))
                return;

            if (_Project.ViewerState.ViewMode == ViewMode.TwoD)
            {
                _Project.ViewerState.Camera2D.CentreLat = Bounds.CentreLat;
                _Project.ViewerState.Camera2D.CentreLon = Bounds.CentreLon;
                StatusLatitude = CoordinateFormat.FormatLatitude(Bounds.CentreLat);
                StatusLongitude = CoordinateFormat.FormatLongitude(Bounds.CentreLon);
            }
            else
            {
                _Project.ViewerState.Camera3D.OrbitCentreLat = Bounds.CentreLat;
                _Project.ViewerState.Camera3D.OrbitCentreLon = Bounds.CentreLon;
            }
        }

        /// <summary>
        /// Keeps view mode, display toggles, and project viewer state aligned when 2D is selected.
        /// </summary>
        /// <param name="Value">New value of the 2D mode toggle.</param>
        partial void OnIsTwoDChanged
            (
            bool Value
            )
        {
            if (Value)
            {
                // Mutual exclusion: activating 2D deactivates 3D and hides vertical exaggeration.
                IsThreeD = false;
                _Project.ViewerState.ViewMode = ViewMode.TwoD;
                ShowVerticalExaggeration = false;
                ResetViewCentreToField();
            }
            SyncDisplayToProject();
            NotifyViewerProperties();
        }

        /// <summary>
        /// Keeps view mode, display toggles, and project viewer state aligned when 3D is selected.
        /// </summary>
        /// <param name="Value">New value of the 3D mode toggle.</param>
        partial void OnIsThreeDChanged
            (
            bool Value
            )
        {
            if (Value)
            {
                // Mutual exclusion: activating 3D deactivates 2D and exposes vertical exaggeration.
                IsTwoD = false;
                _Project.ViewerState.ViewMode = ViewMode.ThreeD;
                ShowVerticalExaggeration = true;
                ResetViewCentreToField();
            }
            SyncDisplayToProject();
            NotifyViewerProperties();
        }

        /// <summary>
        /// Propagates vertical exaggeration to the status label and project display settings.
        /// </summary>
        /// <param name="Value">New vertical exaggeration multiplier.</param>
        partial void OnVerticalExaggerationChanged
            (
            double Value
            )
        {
            VerticalExaggerationLabel = Value.ToString("F1", CultureInfo.InvariantCulture) + "×";
            _Project.ViewerState.Display.VerticalExaggeration = Value;
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Mirrors the survey-points overlay toggle into project display settings.
        /// </summary>
        /// <param name="Value">New survey-points visibility.</param>
        partial void OnSurveyPointsToggleChanged
            (
            bool Value
            )
        {
            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Mirrors the contours overlay toggle into project display settings.
        /// </summary>
        /// <param name="Value">New contours visibility.</param>
        partial void OnContoursToggleChanged
            (
            bool Value
            )
        {
            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Mirrors the hillshade overlay toggle into project display settings.
        /// </summary>
        /// <param name="Value">New hillshade visibility.</param>
        partial void OnHillshadeToggleChanged
            (
            bool Value
            )
        {
            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Mirrors the benchmarks overlay toggle into project display settings.
        /// </summary>
        /// <param name="Value">New benchmarks visibility.</param>
        partial void OnBenchmarksToggleChanged
            (
            bool Value
            )
        {
            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Mirrors the design overlay toggle into project display settings.
        /// </summary>
        /// <param name="Value">New design overlay visibility.</param>
        partial void OnDesignOverlayToggleChanged
            (
            bool Value
            )
        {
            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Mirrors the satellite basemap toggle into project display settings.
        /// </summary>
        /// <param name="Value">New satellite basemap visibility.</param>
        partial void OnSatelliteToggleChanged
            (
            bool Value
            )
        {
            SyncDisplayToProject();
            OnPropertyChanged(nameof(Display));
        }

        /// <summary>
        /// Creates a blank project after optional save of unsaved changes.
        /// </summary>
        [RelayCommand]
        private async Task NewProjectAsync
            (
            )
        {
            if (_Project.IsModified)
            {
                // Offer save-before-discard when the current project has unsaved edits.
                bool Save = await DialogService.ShowConfirmAsync(
                    "Unsaved changes",
                    "Save changes to the current project before starting a new one?");
                if (Save)
                    await SaveProjectAsync();
            }

            _Project.ResetToEmpty();
            ApplyDefaultFieldContentView();
            RefreshAll();
        }

        /// <summary>
        /// Opens a project file from disk and refreshes all bindings.
        /// </summary>
        [RelayCommand]
        private async Task OpenProjectAsync
            (
            )
        {
            string? Path = await DialogService.PickOpenFileAsync("Open Project", "*.agdesign");
            if (Path == null)
                return;

            try
            {
                _Project = AgDesignSerializer.Load(Path);
                ApplyOpenFieldContentView();
                RefreshAll();
            }
            catch (Exception Ex)
            {
                await DialogService.ShowConfirmAsync("Open failed", Ex.Message);
            }
        }

        /// <summary>
        /// Saves the project to its current path, or prompts for a path when none is set.
        /// </summary>
        [RelayCommand]
        private async Task SaveProjectAsync
            (
            )
        {
            if (string.IsNullOrWhiteSpace(_Project.FilePath))
            {
                await SaveProjectAsAsync();
                return;
            }

            try
            {
                AgDesignSerializer.Save(_Project, _Project.FilePath);
                RefreshTitle();
            }
            catch (Exception Ex)
            {
                await DialogService.ShowConfirmAsync("Save failed", Ex.Message);
            }
        }

        /// <summary>
        /// Prompts for a save path and writes the project file.
        /// </summary>
        [RelayCommand]
        private async Task SaveProjectAsAsync
            (
            )
        {
            string? Path = await DialogService.PickSaveFileAsync("Save Project As", "Project.agdesign", "*.agdesign");
            if (Path == null)
                return;

            try
            {
                AgDesignSerializer.Save(_Project, Path);
                RefreshTitle();
            }
            catch (Exception Ex)
            {
                await DialogService.ShowConfirmAsync("Save failed", Ex.Message);
            }
        }

        /// <summary>
        /// Shows the import choice dialog and runs the selected import workflow.
        /// </summary>
        [RelayCommand]
        private async Task ImportAsync
            (
            )
        {
            ImportChoiceViewModel.ImportKind? Kind = await DialogService.ShowImportChoiceAsync();
            if ((Kind == null) || (Kind == ImportChoiceViewModel.ImportKind.None))
                return;

            if (Kind == ImportChoiceViewModel.ImportKind.Survey)
                await ImportSurveyAsync();
            else
                await ImportDesignAsync();
        }

        /// <summary>
        /// Shows the export choice dialog and writes the selected artefact to disk.
        /// </summary>
        [RelayCommand]
        private async Task ExportAsync
            (
            )
        {
            ExportChoiceViewModel.ExportKind? Kind = await DialogService.ShowExportChoiceAsync(HasSurvey, HasDesign);
            if ((Kind == null) || (Kind == ExportChoiceViewModel.ExportKind.None))
                return;

            try
            {
                if (Kind == ExportChoiceViewModel.ExportKind.SurveyAgs)
                {
                    string? Path = await DialogService.PickSaveFileAsync("Export Survey", "survey.ags", "*.ags");
                    if ((Path != null) && (_Project.Survey != null))
                        _Project.Survey.SaveToAgs(Path);
                }
                else if (Kind == ExportChoiceViewModel.ExportKind.SurveyMultiplane)
                {
                    string? Path = await DialogService.PickSaveFileAsync("Export Survey", "survey.txt", "*.txt");
                    if ((Path != null) && (_Project.Survey != null))
                        _Project.Survey.SaveToMultiplane(Path);
                }
                else if (Kind == ExportChoiceViewModel.ExportKind.DesignAgd)
                {
                    string? Path = await DialogService.PickSaveFileAsync("Export Design", "design.agd", "*.agd");
                    if ((Path != null) && (_Project.Survey != null) && (_Project.DesignGrid != null) && (_Project.DesignParameters != null))
                        AgdService.Export(Path, _Project.Survey, _Project.DesignGrid, _Project.DesignParameters);
                }
            }
            catch (Exception Ex)
            {
                await DialogService.ShowConfirmAsync("Export failed", Ex.Message);
            }
        }

        /// <summary>
        /// Opens the design dialog and generates or replaces the field design from survey data.
        /// </summary>
        [RelayCommand]
        private async Task OpenDesignDialogAsync
            (
            )
        {
            if (!HasSurvey)
                return;

            DesignDialogViewModel? Result = await DialogService.ShowDesignDialogAsync(_Project.DesignParameters);
            if (Result == null)
                return;

            if (HasDesign)
            {
                // Confirm overwrite when a design already exists for this project.
                bool Overwrite = await DialogService.ShowConfirmAsync(
                    "Replace design?",
                    "A design already exists. Replace it with the new parameters?");
                if (!Overwrite)
                    return;
            }

            try
            {
                DesignParameters Params = Result.ToParameters();
                FieldCreator Creator = new FieldCreator();
                (DesignGrid Grid, FieldCreator.Statistics Stats) = Creator.CreateDesign(_Project.Survey!, Params);
                _Project.DesignParameters = Params;
                _Project.DesignGrid = Grid;
                _Project.ViewerState.FieldContentView = FieldContentView.Proposed;
                _Project.MarkModified();
                FitViewerCameraToField();
                RefreshAll();
            }
            catch (Exception Ex)
            {
                await DialogService.ShowConfirmAsync("Design failed", Ex.Message);
            }
        }

        /// <summary>
        /// Shows survey statistics for the current project survey, if any.
        /// </summary>
        [RelayCommand]
        private async Task ShowSurveyStatisticsAsync
            (
            )
        {
            await DialogService.ShowSurveyStatisticsAsync(_Project.Survey);
        }

        /// <summary>
        /// Switches the viewer to the existing-surface field content view.
        /// </summary>
        [RelayCommand]
        private void SelectExisting
            (
            )
        {
            if (!HasSurvey)
                return;
            _Project.ViewerState.FieldContentView = FieldContentView.Existing;
            RefreshViewButtons();
            UpdateLegend();
            NotifyViewerProperties();
        }

        /// <summary>
        /// Switches the viewer to the proposed design field content view.
        /// </summary>
        [RelayCommand]
        private void SelectProposed
            (
            )
        {
            if (!CanProposedView)
                return;
            _Project.ViewerState.FieldContentView = FieldContentView.Proposed;
            RefreshViewButtons();
            UpdateLegend();
            NotifyViewerProperties();
        }

        /// <summary>
        /// Switches the viewer to the cut/fill comparison field content view.
        /// </summary>
        [RelayCommand]
        private void SelectCutFill
            (
            )
        {
            if (!CanCutFillView)
                return;
            _Project.ViewerState.FieldContentView = FieldContentView.CutFill;
            RefreshViewButtons();
            UpdateLegend();
            NotifyViewerProperties();
        }

        /// <summary>
        /// Imports survey data from AGS or multiplane text, merging or replacing as chosen.
        /// </summary>
        private async Task ImportSurveyAsync
            (
            )
        {
            string? Path = await DialogService.PickOpenFileAsync("Import Survey", "*.ags", "*.txt");
            if (Path == null)
                return;

            try
            {
                SurveyModel Imported = new SurveyModel();
                Imported.LoadAuto(Path);

                if (HasSurvey)
                {
                    // Let the user replace or merge when survey data is already loaded.
                    bool Replace = await DialogService.ShowConfirmAsync(
                        "Survey import",
                        "Replace existing survey data? Choose No to add imported points to the current survey.");
                    if (Replace)
                        _Project.Survey = Imported;
                    else
                    {
                        _Project.Survey ??= new SurveyModel();
                        _Project.Survey.MergeFrom(Imported);
                    }
                }
                else
                {
                    _Project.Survey = Imported;
                }

                _Project.SurveySourceFormat = Path.EndsWith(".ags", StringComparison.OrdinalIgnoreCase)
                    ? SurveySourceFormat.Ags
                    : SurveySourceFormat.Multiplane;
                _Project.ViewerState.FieldContentView = FieldContentView.Existing;
                // Hide dense survey points by default after import for a cleaner first view.
                _Project.ViewerState.Display.SurveyPoints = false;
                _Project.MarkModified();
                FitViewerCameraToField();
                RefreshAll();
            }
            catch (Exception Ex)
            {
                await DialogService.ShowConfirmAsync("Import failed", Ex.Message);
            }
        }

        /// <summary>
        /// Imports a complete AGD design package, replacing existing survey and design when confirmed.
        /// </summary>
        private async Task ImportDesignAsync
            (
            )
        {
            string? Path = await DialogService.PickOpenFileAsync("Import Design", "*.agd");
            if (Path == null)
                return;

            if (HasDesign || HasSurvey)
            {
                // AGD import replaces both survey and design; confirm before destructive merge.
                bool Replace = await DialogService.ShowConfirmAsync(
                    "Import design",
                    "Replace existing project survey and design data with the imported AGD file?");
                if (!Replace)
                    return;
            }

            try
            {
                AgdImportResult Result = AgdService.Import(Path);
                _Project.Survey = Result.Survey;
                _Project.DesignParameters = Result.Parameters;
                _Project.DesignGrid = Result.Grid;
                _Project.SurveySourceFormat = SurveySourceFormat.Agd;
                _Project.ViewerState.FieldContentView = FieldContentView.Proposed;
                _Project.MarkModified();
                FitViewerCameraToField();
                RefreshAll();
            }
            catch (Exception Ex)
            {
                await DialogService.ShowConfirmAsync("Import failed", Ex.Message);
            }
        }

        /// <summary>
        /// Synchronizes all observable flags and derived UI from the current project document.
        /// </summary>
        private void RefreshAll
            (
            )
        {
            // Push derived flags and toggles from project state into bindable properties.
            HasSurvey = _Project.HasSurvey;
            HasDesign = _Project.HasDesign;
            CanDesign = HasSurvey;
            CanProposedView = (HasSurvey) && (HasDesign);
            CanCutFillView = (HasSurvey) && (HasDesign);
            BenchmarksToggleEnabled = (_Project.Survey != null) && (_Project.Survey.Benchmarks.Count > 0);

            IsTwoD = (_Project.ViewerState.ViewMode == ViewMode.TwoD);
            IsThreeD = (_Project.ViewerState.ViewMode == ViewMode.ThreeD);
            ShowVerticalExaggeration = IsThreeD;

            SurveyPointsToggle = _Project.ViewerState.Display.SurveyPoints;
            ContoursToggle = _Project.ViewerState.Display.Contours;
            HillshadeToggle = _Project.ViewerState.Display.Hillshade;
            BenchmarksToggle = _Project.ViewerState.Display.Benchmarks;
            DesignOverlayToggle = _Project.ViewerState.Display.DesignOverlay;
            SatelliteToggle = _Project.ViewerState.Display.Satellite;
            VerticalExaggeration = _Project.ViewerState.Display.VerticalExaggeration;

            StatusLatitude = CoordinateFormat.FormatLatitude(_Project.ViewerState.Camera2D.CentreLat);
            StatusLongitude = CoordinateFormat.FormatLongitude(_Project.ViewerState.Camera2D.CentreLon);

            RefreshTitle();
            RefreshViewButtons();
            UpdateLegend();
            NotifyViewerProperties();
        }

        /// <summary>
        /// Raises change notification for viewer-facing properties backed by project state.
        /// </summary>
        private void NotifyViewerProperties
            (
            )
        {
            // Re-raise viewer-facing passthrough properties after project mutations.
            OnPropertyChanged(nameof(Project));
            OnPropertyChanged(nameof(ViewMode));
            OnPropertyChanged(nameof(FieldContentView));
            OnPropertyChanged(nameof(Display));
            OnPropertyChanged(nameof(CentreLat));
            OnPropertyChanged(nameof(CentreLon));
            OnPropertyChanged(nameof(Zoom));
            OnPropertyChanged(nameof(OrbitHeading));
            OnPropertyChanged(nameof(OrbitPitch));
            OnPropertyChanged(nameof(OrbitDistance));
            OnPropertyChanged(nameof(OrbitCentreLat));
            OnPropertyChanged(nameof(OrbitCentreLon));
        }

        /// <summary>
        /// Updates the window title from the project title bar name.
        /// </summary>
        private void RefreshTitle
            (
            )
        {
            ProjectTitle = _Project.TitleBarName;
        }

        /// <summary>
        /// Synchronizes field-content view toggle button active states from project state.
        /// </summary>
        private void RefreshViewButtons
            (
            )
        {
            FieldContentView View = _Project.ViewerState.FieldContentView;
            IsExistingActive = (View == FieldContentView.Existing);
            IsProposedActive = (View == FieldContentView.Proposed);
            IsCutFillActive = (View == FieldContentView.CutFill);
        }

        /// <summary>
        /// Copies overlay and exaggeration toggles from view-model properties into project display settings.
        /// </summary>
        private void SyncDisplayToProject
            (
            )
        {
            DisplaySettings D = _Project.ViewerState.Display;
            D.SurveyPoints = SurveyPointsToggle;
            D.Contours = ContoursToggle;
            D.Hillshade = HillshadeToggle;
            D.Benchmarks = BenchmarksToggle;
            D.DesignOverlay = DesignOverlayToggle;
            D.Satellite = SatelliteToggle;
            D.VerticalExaggeration = VerticalExaggeration;
        }

        /// <summary>
        /// Resets field content view after creating a new empty project.
        /// </summary>
        private void ApplyDefaultFieldContentView
            (
            )
        {
            _Project.ViewerState.FieldContentView = FieldContentView.None;
        }

        /// <summary>
        /// Chooses an initial field content view after opening a project from disk.
        /// </summary>
        private void ApplyOpenFieldContentView
            (
            )
        {
            if (_Project.HasDesign)
                _Project.ViewerState.FieldContentView = FieldContentView.Proposed;
            else if (_Project.HasSurvey)
                _Project.ViewerState.FieldContentView = FieldContentView.Existing;
            else
                _Project.ViewerState.FieldContentView = FieldContentView.None;
        }

        /// <summary>
        /// Rebuilds legend bands from the scalar range for the active field content view.
        /// </summary>
        private void UpdateLegend
            (
            )
        {
            LegendBands.Clear();
            ShowLegend = HasSurvey;

            if (!HasSurvey)
                return;

            FieldContentView View = _Project.ViewerState.FieldContentView;
            // Default legend view when none is selected: proposed if designed, else existing survey.
            if (View == FieldContentView.None)
                View = HasDesign ? FieldContentView.Proposed : FieldContentView.Existing;

            FieldSurfaceSampler.ComputeScalarRangeFromProject(_Project, View, out double VMin, out double VMax);
            double[] Boundaries = NineBandColorScale.GetBandBoundaries(VMin, VMax);

            for (Int32 BandIndex = 0; BandIndex < NineBandColorScale.BandNames.Length; BandIndex++)
            {
                uint Argb = NineBandColorScale.BandColorsArgb[BandIndex];
                byte A = (byte)((Argb >> ARGB_ALPHA_SHIFT) & ARGB_BYTE_MASK);
                byte R = (byte)((Argb >> ARGB_RED_SHIFT) & ARGB_BYTE_MASK);
                byte G = (byte)((Argb >> ARGB_GREEN_SHIFT) & ARGB_BYTE_MASK);
                byte B = (byte)(Argb & ARGB_BYTE_MASK);

                LegendBands.Add(new LegendBandViewModel
                {
                    Name = NineBandColorScale.BandNames[BandIndex],
                    BoundaryText = NineBandColorScale.FormatMetres(Boundaries[BandIndex]) + " m",
                    Swatch = new SolidColorBrush(Color.FromArgb(A, R, G, B))
                });
            }
        }
    }
}
