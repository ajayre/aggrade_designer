using System.Collections.ObjectModel;

using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using AgGradeDesigner.Core.Features;
using AgGradeDesigner.Core.Geo;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Helpers;
using AgGradeDesigner.Services;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// Wells and Roads lists for the bottom Features palette, including add and edit mode.
    /// </summary>
    public sealed partial class FeaturesPaletteViewModel : ViewModelBase
    {
        /// <summary>Tab index for the Wells list.</summary>
        private const int WELL_TAB = 0;

        /// <summary>Tab index for the Roads list.</summary>
        private const int ROAD_TAB = 1;

        /// <summary>Open project, which can be replaced on Open.</summary>
        private readonly Func<ProjectDocument> _Project;

        /// <summary>Placement and drag session shared with the field viewer.</summary>
        private readonly FeatureEditSession _Session;

        /// <summary>Active display units for pad diameter, road bed width, and road height.</summary>
        private readonly Func<DisplayUnitSystem> _Units;

        /// <summary>Marks the project dirty after a commit or a direct size edit.</summary>
        private readonly Action _MarkModified;

        /// <summary>True while a progress overlay should block new placement.</summary>
        private readonly Func<bool> _IsBusy;

        /// <summary>True while code is selecting a tab so the change does not cancel the edit.</summary>
        private bool _SuppressTabChange;

        /// <summary>True while code is filling the road editors, so the write-back is skipped.</summary>
        private bool _SuppressRoadParamWrite;

        /// <summary>True while code is filling the well diameter, so the write-back is skipped.</summary>
        private bool _SuppressWellDiameterWrite;

        /// <summary>
        /// True while a new road's bed width should stay on the blade-and-slope formula.
        /// Cleared when the user types a bed width.
        /// </summary>
        private bool _RoadBedFollowsDefault;

        /// <summary>
        /// Creates the palette and listens for edit-session changes.
        /// </summary>
        /// <param name="Project">Returns the current project document.</param>
        /// <param name="Session">Feature edit session.</param>
        /// <param name="Units">Returns the active display unit system.</param>
        /// <param name="MarkModified">Called after a change is stored on the project.</param>
        /// <param name="IsBusy">Returns true when project-panel actions are blocked.</param>
        public FeaturesPaletteViewModel
            (
            Func<ProjectDocument> Project,
            FeatureEditSession Session,
            Func<DisplayUnitSystem> Units,
            Action MarkModified,
            Func<bool> IsBusy
            )
        {
            _Project = Project;
            _Session = Session;
            _Units = Units;
            _MarkModified = MarkModified;
            _IsBusy = IsBusy;
            _Session.Changed += OnSessionChanged;
        }

        /// <summary>Well rows, including unsaved clicks while adding.</summary>
        public ObservableCollection<WellFeatureRowViewModel> Wells { get; } = new ObservableCollection<WellFeatureRowViewModel>();

        /// <summary>Road rows, including the unsaved centreline while adding.</summary>
        public ObservableCollection<RoadFeatureRowViewModel> Roads { get; } = new ObservableCollection<RoadFeatureRowViewModel>();

        /// <summary>True when Add can start a new placement.</summary>
        [ObservableProperty]
        private bool _CanPlace;

        /// <summary>True when a well can be placed by typing a latitude and longitude.</summary>
        [ObservableProperty]
        private bool _CanAddWellByCoordinates;

        /// <summary>True when Save is shown on the Wells tab.</summary>
        [ObservableProperty]
        private bool _ShowWellSave;

        /// <summary>True when the pad-diameter editor is shown while wells are being added.</summary>
        [ObservableProperty]
        private bool _ShowWellSettings;

        /// <summary>True when Save is shown on the Roads tab.</summary>
        [ObservableProperty]
        private bool _ShowRoadSave;

        /// <summary>True when the Wells list has at least one row.</summary>
        [ObservableProperty]
        private bool _HasWells;

        /// <summary>True when the Roads list has at least one row.</summary>
        [ObservableProperty]
        private bool _HasRoads;

        /// <summary>Unit suffix for diameters and widths.</summary>
        [ObservableProperty]
        private string _LengthUnit = "ft";

        /// <summary>Pad diameter being typed for wells that are being added.</summary>
        [ObservableProperty]
        private string _WellPadDiameterText = "";

        /// <summary>Road bed width being typed for the road that is being created or edited.</summary>
        [ObservableProperty]
        private string _RoadBedWidthText = "";

        /// <summary>Embankment height being typed for the road that is being created or edited.</summary>
        [ObservableProperty]
        private string _RoadHeightText = "";

        /// <summary>Side slope being typed for the road that is being created or edited, without the ":1" suffix.</summary>
        [ObservableProperty]
        private string _RoadSideSlopeText = "";

        /// <summary>Selected Features tab. 0 is Wells, 1 is Roads.</summary>
        [ObservableProperty]
        private int _SelectedTabIndex;

        /// <summary>
        /// Refreshes Add enablement after the project or a busy overlay changes.
        /// </summary>
        public void NotifyAvailability
            (
            )
        {
            UpdateModeFlags();
        }

        /// <summary>
        /// Rebuilds both lists from the project and the current draft.
        /// </summary>
        public void Reload
            (
            )
        {
            LengthUnit = FeatureLengthFormat.UnitLabel(_Units());
            if (_Session.Mode == FeatureEditMode.AddWells)
                SeedWellDiameterText();

            if ((_Session.Mode == FeatureEditMode.AddRoad) || (_Session.Mode == FeatureEditMode.EditRoad))
                SeedRoadParameterText();

            RebuildWells();
            RebuildRoads();
            UpdateModeFlags();
        }

        /// <summary>
        /// Drops an unsaved edit. Used when the palette closes or Escape is pressed.
        /// </summary>
        public void CancelEdit
            (
            )
        {
            _Session.Cancel();
        }

        /// <summary>
        /// Applies a session change on the UI thread.
        /// </summary>
        /// <param name="Kind">Whether the list structure or only coordinates changed.</param>
        private void OnSessionChanged
            (
            FeatureEditChangeKind Kind
            )
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnSessionChanged(Kind));
                return;
            }

            if (Kind == FeatureEditChangeKind.Geometry)
                UpdateLiveLabels();
            else
                Reload();
        }

        /// <summary>
        /// Starts well placement. Each map click adds a pad centre until Save.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAddFeature))]
        private void AddWell
            (
            )
        {
            if (!CanAddFeature())
                return;

            SelectTab(WELL_TAB);
            _Session.BeginAddWells(FieldFeatureSet.DefaultPadDiameterM);
        }

        /// <summary>
        /// Prompts for a latitude and longitude, then places a well there.
        /// Cancel leaves the map unchanged. The well stays a draft until the panel Save button.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAddWellUsingCoordinates))]
        private async Task AddWellUsingCoordinates
            (
            )
        {
            if (!CanAddWellUsingCoordinates())
                return;

            WellCoordinateDialogViewModel? Accepted = await DialogService.ShowWellCoordinateDialogAsync(IsWellCoordinateInsideSurvey);
            if (Accepted == null)
                return;

            // A typed location is the same draft as a map click, so it is not in the project until panel Save.
            if (_Session.Mode != FeatureEditMode.AddWells)
            {
                SelectTab(WELL_TAB);
                _Session.BeginAddWells(FieldFeatureSet.DefaultPadDiameterM);
            }

            _Session.AddWellAt(Accepted.Latitude, Accepted.Longitude);
        }

        /// <summary>
        /// True when a survey is loaded and a coordinate well would not interrupt a road or an edit.
        /// </summary>
        /// <returns>True when Add Using Coordinates may open.</returns>
        private bool CanAddWellUsingCoordinates
            (
            )
        {
            if (!_Project().HasSurvey || _IsBusy())
                return false;

            return !_Session.IsActive || (_Session.Mode == FeatureEditMode.AddWells);
        }

        /// <summary>
        /// True when a typed well coordinate lies on the field.
        /// A survey with no boundary ring accepts the coordinate, because there is no ring to test.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <returns>True when the location may be placed.</returns>
        private bool IsWellCoordinateInsideSurvey
            (
            double Latitude,
            double Longitude
            )
        {
            ProjectDocument Project = _Project();
            List<(double Lat, double Lon)>? Ring = FieldBoundaryResolver.Resolve(Project.Survey, Project.DesignGrid);
            if ((Ring == null) || (Ring.Count < GeoConstants.MIN_POLYGON_VERTICES))
                return true;

            return BoundaryMask.IsInsideBoundaryLatLon(Latitude, Longitude, Ring);
        }

        /// <summary>
        /// Starts road placement. Each map click appends a centreline vertex until Save.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanAddFeature))]
        private void AddRoad
            (
            )
        {
            if (!CanAddFeature())
                return;

            SelectTab(ROAD_TAB);
            // A new bed is twice the blade width minus the side-slope number, until the user types a width.
            _RoadBedFollowsDefault = true;
            double SideSlope = RoadCrossSection.DEFAULT_SIDE_SLOPE;
            _Session.BeginAddRoad(
                RoadCrossSection.DefaultBedWidthM(_Project().HaulSettings.BladeWidthMm, SideSlope),
                RoadCrossSection.DefaultHeightM,
                SideSlope);
        }

        /// <summary>
        /// Writes the current draft into the project when it is complete.
        /// </summary>
        [RelayCommand]
        private void Save
            (
            )
        {
            if (!_Session.IsActive)
                return;

            bool Wrote = _Session.TryCommit(_Project().Features);
            if (Wrote)
                _MarkModified();
        }

        /// <summary>
        /// True when a survey is loaded, the panel is idle, and no edit is already open.
        /// </summary>
        /// <returns>True when Add may start.</returns>
        private bool CanAddFeature
            (
            )
        {
            return _Project().HasSurvey && !_IsBusy() && !_Session.IsActive;
        }

        /// <summary>
        /// Cancels an edit when the user switches to the other feature tab.
        /// </summary>
        /// <param name="Value">New tab index.</param>
        partial void OnSelectedTabIndexChanged
            (
            int Value
            )
        {
            if (_SuppressTabChange)
                return;

            bool WellEdit = (_Session.Mode == FeatureEditMode.AddWells) || (_Session.Mode == FeatureEditMode.EditWell);
            bool RoadEdit = (_Session.Mode == FeatureEditMode.AddRoad) || (_Session.Mode == FeatureEditMode.EditRoad);
            if (WellEdit && (Value != WELL_TAB))
                _Session.Cancel();
            else if (RoadEdit && (Value != ROAD_TAB))
                _Session.Cancel();
        }

        /// <summary>
        /// Selects a tab without treating the change as the user leaving an edit.
        /// </summary>
        /// <param name="Index">Tab index.</param>
        private void SelectTab
            (
            int Index
            )
        {
            _SuppressTabChange = true;
            SelectedTabIndex = Index;
            _SuppressTabChange = false;
        }

        /// <summary>
        /// Updates save visibility and whether Add is enabled.
        /// </summary>
        private void UpdateModeFlags
            (
            )
        {
            CanPlace = CanAddFeature();
            CanAddWellByCoordinates = CanAddWellUsingCoordinates();
            ShowWellSave = (_Session.Mode == FeatureEditMode.AddWells) || (_Session.Mode == FeatureEditMode.EditWell);
            ShowWellSettings = _Session.Mode == FeatureEditMode.AddWells;
            ShowRoadSave = (_Session.Mode == FeatureEditMode.AddRoad) || (_Session.Mode == FeatureEditMode.EditRoad);
            HasWells = Wells.Count > 0;
            HasRoads = Roads.Count > 0;
            AddWellCommand.NotifyCanExecuteChanged();
            AddWellUsingCoordinatesCommand.NotifyCanExecuteChanged();
            AddRoadCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Rebuilds the Wells list from committed wells and unsaved clicks.
        /// </summary>
        private void RebuildWells
            (
            )
        {
            Wells.Clear();
            DisplayUnitSystem Units = _Units();
            string Unit = FeatureLengthFormat.UnitLabel(Units);
            FieldFeatureSet Features = _Project().Features;
            bool Editing = _Session.Mode == FeatureEditMode.EditWell;
            string? EditingId = Editing ? _Session.EditingFeatureId : null;

            for (int Index = 0; Index < Features.Wells.Count; Index++)
            {
                WellFeature Well = Features.Wells[Index];
                bool Live = (EditingId != null) && string.Equals(Well.Id, EditingId, StringComparison.Ordinal);
                double Latitude = Well.Latitude;
                double Longitude = Well.Longitude;
                double DiameterM = Well.PadDiameterM;
                if (Live && (_Session.EditableVertices.Count == 1))
                {
                    Latitude = _Session.EditableVertices[0].Latitude;
                    Longitude = _Session.EditableVertices[0].Longitude;
                    DiameterM = _Session.EditingWellPadDiameterM;
                }

                string Id = Well.Id;
                Wells.Add(new WellFeatureRowViewModel(
                    Id,
                    FormatCoordinate(Latitude, Longitude),
                    CoordinateFormat.FormatLatitude(Latitude),
                    CoordinateFormat.FormatLongitude(Longitude),
                    FeatureLengthFormat.FormatDisplay(DiameterM, Units),
                    Unit,
                    ShowEditButton: !Live && !_Session.IsActive,
                    IsLiveEdit: Live,
                    IsAlternate: (Index % 2) == 1,
                    CanEditSize: Live,
                    ShowDiameter: true,
                    Edit: EditWell,
                    Delete: DeleteWellAsync,
                    DiameterEdited: Text => ApplyWellDiameter(Id, Live, Text),
                    LocationEdited: (LatitudeText, LongitudeText) => ApplyWellLocation(Live, LatitudeText, LongitudeText)));
            }

            if (_Session.Mode == FeatureEditMode.AddWells)
            {
                for (int Index = 0; Index < _Session.DraftWells.Count; Index++)
                {
                    WellDraft Draft = _Session.DraftWells[Index];
                    int DraftIndex = Index;
                    int RowIndex = Features.Wells.Count + Index;
                    Wells.Add(new WellFeatureRowViewModel(
                        "draft-" + DraftIndex.ToString(),
                        FormatCoordinate(Draft.Latitude, Draft.Longitude),
                        CoordinateFormat.FormatLatitude(Draft.Latitude),
                        CoordinateFormat.FormatLongitude(Draft.Longitude),
                        FeatureLengthFormat.FormatDisplay(Draft.PadDiameterM, Units),
                        Unit,
                        ShowEditButton: false,
                        IsLiveEdit: false,
                        IsAlternate: (RowIndex % 2) == 1,
                        // The add header owns the diameter, so the draft row does not repeat it.
                        CanEditSize: false,
                        ShowDiameter: false,
                        Edit: _ => { },
                        Delete: _ => Task.CompletedTask,
                        DiameterEdited: Text => ApplyDraftWellDiameter(DraftIndex, Text),
                        LocationEdited: (_, _) => { }));
                }
            }
        }

        /// <summary>
        /// Rebuilds the Roads list from committed roads and the unsaved centreline.
        /// </summary>
        private void RebuildRoads
            (
            )
        {
            Roads.Clear();
            DisplayUnitSystem Units = _Units();
            string Unit = FeatureLengthFormat.UnitLabel(Units);
            FieldFeatureSet Features = _Project().Features;
            bool Editing = _Session.Mode == FeatureEditMode.EditRoad;
            string? EditingId = Editing ? _Session.EditingFeatureId : null;

            for (int Index = 0; Index < Features.Roads.Count; Index++)
            {
                RoadFeature Road = Features.Roads[Index];
                bool Live = (EditingId != null) && string.Equals(Road.Id, EditingId, StringComparison.Ordinal);
                string Start = FormatEndpoint(Road.Vertices.Count > 0 ? Road.Vertices[0] : default, "Start");
                string End = FormatEndpoint(Road.Vertices.Count > 0 ? Road.Vertices[Road.Vertices.Count - 1] : default, "End");
                if (Live && (_Session.EditableVertices.Count > 0))
                {
                    Start = FormatDraftEndpoint(0, "Start");
                    End = FormatDraftEndpoint(_Session.EditableVertices.Count - 1, "End");
                }

                string Id = Road.Id;
                Roads.Add(new RoadFeatureRowViewModel(
                    Id,
                    Start,
                    End,
                    FeatureLengthFormat.FormatDisplay(Road.BedWidthM, Units),
                    FeatureLengthFormat.FormatDisplay(Road.HeightM, Units),
                    RoadCrossSection.FormatSideSlope(Road.SideSlope),
                    Unit,
                    ShowEditButton: !Live && !_Session.IsActive,
                    IsLiveEdit: Live,
                    IsAlternate: (Index % 2) == 1,
                    // The road being edited uses the header boxes, so its row does not repeat them.
                    ShowParameters: !Live,
                    Edit: EditRoad,
                    Delete: DeleteRoadAsync));
            }

            if ((_Session.Mode == FeatureEditMode.AddRoad) && (_Session.EditableVertices.Count > 0))
            {
                string EndText = _Session.EditableVertices.Count >= FieldFeatureSet.MIN_ROAD_VERTICES
                    ? FormatDraftEndpoint(_Session.EditableVertices.Count - 1, "End")
                    : "End —";
                int RowIndex = Features.Roads.Count;
                Roads.Add(new RoadFeatureRowViewModel(
                    "draft-road",
                    FormatDraftEndpoint(0, "Start"),
                    EndText,
                    FeatureLengthFormat.FormatDisplay(_Session.RoadBedWidthM, Units),
                    FeatureLengthFormat.FormatDisplay(_Session.RoadHeightM, Units),
                    RoadCrossSection.FormatSideSlope(_Session.RoadSideSlope),
                    Unit,
                    ShowEditButton: false,
                    IsLiveEdit: false,
                    IsAlternate: (RowIndex % 2) == 1,
                    ShowParameters: false,
                    Edit: _ => { },
                    Delete: _ => Task.CompletedTask));
            }
        }

        /// <summary>
        /// Refreshes coordinate labels for the feature currently being dragged.
        /// </summary>
        private void UpdateLiveLabels
            (
            )
        {
            if ((_Session.Mode == FeatureEditMode.EditWell) && (_Session.EditableVertices.Count == 1))
            {
                string Text = FormatCoordinate(_Session.EditableVertices[0].Latitude, _Session.EditableVertices[0].Longitude);
                double Latitude = _Session.EditableVertices[0].Latitude;
                double Longitude = _Session.EditableVertices[0].Longitude;
                for (int Index = 0; Index < Wells.Count; Index++)
                {
                    if (!Wells[Index].IsLiveEdit)
                        continue;

                    Wells[Index].CoordinateText = Text;
                    Wells[Index].SetLocation(Latitude, Longitude);
                }

                return;
            }

            if ((_Session.Mode == FeatureEditMode.EditRoad) && (_Session.EditableVertices.Count > 0))
            {
                string Start = FormatDraftEndpoint(0, "Start");
                string End = FormatDraftEndpoint(_Session.EditableVertices.Count - 1, "End");
                for (int Index = 0; Index < Roads.Count; Index++)
                {
                    if (!Roads[Index].IsLiveEdit)
                        continue;

                    Roads[Index].StartText = Start;
                    Roads[Index].EndText = End;
                }

                return;
            }

            if (_Session.Mode == FeatureEditMode.AddWells)
            {
                // Draft rows are appended after the saved wells, so a drag can update just those labels.
                int FirstDraft = Wells.Count - _Session.DraftWells.Count;
                for (int Index = 0; Index < _Session.DraftWells.Count; Index++)
                {
                    int Row = FirstDraft + Index;
                    if ((Row < 0) || (Row >= Wells.Count))
                        continue;

                    WellDraft Draft = _Session.DraftWells[Index];
                    Wells[Row].CoordinateText = FormatCoordinate(Draft.Latitude, Draft.Longitude);
                }

                return;
            }

            if ((_Session.Mode == FeatureEditMode.AddRoad) && (_Session.EditableVertices.Count > 0))
            {
                string Start = FormatDraftEndpoint(0, "Start");
                string End = _Session.EditableVertices.Count >= FieldFeatureSet.MIN_ROAD_VERTICES
                    ? FormatDraftEndpoint(_Session.EditableVertices.Count - 1, "End")
                    : "End —";
                for (int Index = 0; Index < Roads.Count; Index++)
                {
                    if (!string.Equals(Roads[Index].Id, "draft-road", StringComparison.Ordinal))
                        continue;

                    Roads[Index].StartText = Start;
                    Roads[Index].EndText = End;
                }
            }
        }

        /// <summary>
        /// Copies one committed well into the editor.
        /// </summary>
        /// <param name="Id">Well identifier.</param>
        private void EditWell
            (
            string Id
            )
        {
            if (_Session.IsActive)
                return;

            WellFeature? Well = _Project().Features.FindWell(Id);
            if (Well == null)
                return;

            SelectTab(WELL_TAB);
            _Session.BeginEditWell(Well);
        }

        /// <summary>
        /// Copies one committed road into the editor.
        /// </summary>
        /// <param name="Id">Road identifier.</param>
        private void EditRoad
            (
            string Id
            )
        {
            if (_Session.IsActive)
                return;

            RoadFeature? Road = _Project().Features.FindRoad(Id);
            if (Road == null)
                return;

            SelectTab(ROAD_TAB);
            _RoadBedFollowsDefault = false;
            _Session.BeginEditRoad(Road);
        }

        /// <summary>
        /// Asks before removing one saved well, then drops it from the project.
        /// </summary>
        /// <param name="Id">Well identifier.</param>
        private async Task DeleteWellAsync
            (
            string Id
            )
        {
            if (_Session.IsActive || _IsBusy())
                return;

            bool Confirmed = await DialogService.ShowConfirmAsync(
                "Delete well",
                "Delete this well?");
            // The list may have changed while the dialog was open.
            if (!Confirmed || _Session.IsActive || _IsBusy())
                return;

            if (!_Project().Features.RemoveWell(Id))
                return;

            _MarkModified();
            Reload();
        }

        /// <summary>
        /// Asks before removing one saved road, then drops it from the project.
        /// </summary>
        /// <param name="Id">Road identifier.</param>
        private async Task DeleteRoadAsync
            (
            string Id
            )
        {
            if (_Session.IsActive || _IsBusy())
                return;

            bool Confirmed = await DialogService.ShowConfirmAsync(
                "Delete road",
                "Delete this road?");
            if (!Confirmed || _Session.IsActive || _IsBusy())
                return;

            if (!_Project().Features.RemoveRoad(Id))
                return;

            _MarkModified();
            Reload();
        }

        /// <summary>
        /// Moves the well being edited when both typed coordinates are legal and on the survey.
        /// The move stays in the edit session until the panel Save button.
        /// </summary>
        /// <param name="Live">True when the row is the well currently being edited.</param>
        /// <param name="LatitudeText">Latitude text in decimal degrees.</param>
        /// <param name="LongitudeText">Longitude text in decimal degrees.</param>
        private void ApplyWellLocation
            (
            bool Live,
            string LatitudeText,
            string LongitudeText
            )
        {
            if (!Live)
                return;

            if (!CoordinateFormat.TryParseLatitude(LatitudeText, out double Latitude) ||
                !CoordinateFormat.TryParseLongitude(LongitudeText, out double Longitude))
                return;

            // A typed point off the survey is not a place the map click would accept.
            if (!IsWellCoordinateInsideSurvey(Latitude, Longitude))
                return;

            _Session.TryMoveEditableVertex(0, Latitude, Longitude);
        }

        /// <summary>
        /// Stores a pad diameter typed for a committed well or the well being edited.
        /// </summary>
        /// <param name="Id">Well identifier.</param>
        /// <param name="Live">True when the row is the well currently being edited.</param>
        /// <param name="Text">Diameter text in display units.</param>
        private void ApplyWellDiameter
            (
            string Id,
            bool Live,
            string Text
            )
        {
            if (!FeatureLengthFormat.TryParseDisplay(Text, _Units(), out double Metres))
                return;

            if (Live)
            {
                _Session.SetEditingWellPadDiameter(Metres);
                return;
            }

            WellFeature? Well = _Project().Features.FindWell(Id);
            if ((Well == null) || (Well.PadDiameterM == Metres))
                return;

            Well.PadDiameterM = Metres;
            _MarkModified();
        }

        /// <summary>
        /// Stores a pad diameter typed for a well that has not been saved yet.
        /// </summary>
        /// <param name="Index">Draft well index.</param>
        /// <param name="Text">Diameter text in display units.</param>
        private void ApplyDraftWellDiameter
            (
            int Index,
            string Text
            )
        {
            if (!FeatureLengthFormat.TryParseDisplay(Text, _Units(), out double Metres))
                return;

            _Session.SetDraftWellPadDiameter(Index, Metres);
        }

        /// <summary>
        /// Fills the add-well diameter from the session without writing that value back.
        /// </summary>
        private void SeedWellDiameterText
            (
            )
        {
            _SuppressWellDiameterWrite = true;
            WellPadDiameterText = FeatureLengthFormat.FormatDisplay(_Session.EditingWellPadDiameterM, _Units());
            _SuppressWellDiameterWrite = false;
        }

        /// <summary>
        /// Stores a pad diameter typed while wells are being added, and resizes wells already clicked.
        /// </summary>
        /// <param name="Value">New text box contents.</param>
        partial void OnWellPadDiameterTextChanged
            (
            string Value
            )
        {
            if (_SuppressWellDiameterWrite)
                return;

            if (!FeatureLengthFormat.TryParseDisplay(Value, _Units(), out double Metres))
                return;

            _Session.SetAddingWellPadDiameter(Metres);
        }

        /// <summary>
        /// Fills the road editors from the session without writing those values back.
        /// </summary>
        private void SeedRoadParameterText
            (
            )
        {
            DisplayUnitSystem Units = _Units();
            _SuppressRoadParamWrite = true;
            RoadBedWidthText = FeatureLengthFormat.FormatDisplay(_Session.RoadBedWidthM, Units);
            RoadHeightText = FeatureLengthFormat.FormatDisplay(_Session.RoadHeightM, Units);
            RoadSideSlopeText = RoadCrossSection.FormatSideSlope(_Session.RoadSideSlope);
            _SuppressRoadParamWrite = false;
        }

        /// <summary>
        /// Stores a bed width typed while a road is being created or edited.
        /// Typing a width leaves the blade-and-slope default.
        /// </summary>
        /// <param name="Value">New text box contents.</param>
        partial void OnRoadBedWidthTextChanged
            (
            string Value
            )
        {
            if (_SuppressRoadParamWrite)
                return;

            if (!FeatureLengthFormat.TryParseDisplay(Value, _Units(), out double Metres))
                return;

            _RoadBedFollowsDefault = false;
            _Session.SetRoadSection(Metres, _Session.RoadHeightM, _Session.RoadSideSlope);
        }

        /// <summary>
        /// Stores a height typed while a road is being created or edited.
        /// </summary>
        /// <param name="Value">New text box contents.</param>
        partial void OnRoadHeightTextChanged
            (
            string Value
            )
        {
            if (_SuppressRoadParamWrite)
                return;

            if (!FeatureLengthFormat.TryParseDisplay(Value, _Units(), out double Metres))
                return;

            _Session.SetRoadSection(_Session.RoadBedWidthM, Metres, _Session.RoadSideSlope);
        }

        /// <summary>
        /// Stores a side slope typed while a road is being created or edited.
        /// On a new road that still uses the default bed, the bed follows the new slope.
        /// </summary>
        /// <param name="Value">New text box contents.</param>
        partial void OnRoadSideSlopeTextChanged
            (
            string Value
            )
        {
            if (_SuppressRoadParamWrite)
                return;

            if (!RoadCrossSection.TryParseSideSlope(Value, out double SideSlope))
                return;

            double BedWidthM = _Session.RoadBedWidthM;
            if (_RoadBedFollowsDefault && (_Session.Mode == FeatureEditMode.AddRoad))
            {
                BedWidthM = RoadCrossSection.DefaultBedWidthM(_Project().HaulSettings.BladeWidthMm, SideSlope);
                _SuppressRoadParamWrite = true;
                RoadBedWidthText = FeatureLengthFormat.FormatDisplay(BedWidthM, _Units());
                _SuppressRoadParamWrite = false;
            }

            _Session.SetRoadSection(BedWidthM, _Session.RoadHeightM, SideSlope);
        }

        /// <summary>
        /// Formats one draft vertex as a start or end label.
        /// </summary>
        /// <param name="Index">Vertex index.</param>
        /// <param name="Label">"Start" or "End".</param>
        /// <returns>Labelled coordinate text.</returns>
        private string FormatDraftEndpoint
            (
            int Index,
            string Label
            )
        {
            DraftVertex Vertex = _Session.EditableVertices[Index];
            return Label + " " + FormatCoordinate(Vertex.Latitude, Vertex.Longitude);
        }

        /// <summary>
        /// Formats a committed centreline end.
        /// </summary>
        /// <param name="Vertex">Centreline vertex.</param>
        /// <param name="Label">"Start" or "End".</param>
        /// <returns>Labelled coordinate text.</returns>
        static private string FormatEndpoint
            (
            FeatureVertex Vertex,
            string Label
            )
        {
            return Label + " " + FormatCoordinate(Vertex.Latitude, Vertex.Longitude);
        }

        /// <summary>
        /// Formats a latitude and longitude pair the same way as the status bar.
        /// </summary>
        /// <param name="Latitude">Latitude in decimal degrees.</param>
        /// <param name="Longitude">Longitude in decimal degrees.</param>
        /// <returns>Coordinate text.</returns>
        static private string FormatCoordinate
            (
            double Latitude,
            double Longitude
            )
        {
            return CoordinateFormat.FormatLatitude(Latitude) + ", " + CoordinateFormat.FormatLongitude(Longitude);
        }
    }
}
