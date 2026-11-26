namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// Places and edits wells, roads, and reference points. Clicks add points the way the point and polyline
    /// tools do, but this session does not call those tools. Save writes the draft into the project.
    /// </summary>
    public sealed class FeatureEditSession
    {
        /// <summary>Wells clicked during <see cref="FeatureEditMode.AddWells"/>.</summary>
        private readonly List<WellDraft> _DraftWells = new List<WellDraft>();

        /// <summary>Reference points clicked during <see cref="FeatureEditMode.AddReferencePoints"/>.</summary>
        private readonly List<ReferencePointDraft> _DraftReferencePoints = new List<ReferencePointDraft>();

        /// <summary>Centreline or single pad centre being drawn or dragged.</summary>
        private readonly List<DraftVertex> _EditableVertices = new List<DraftVertex>();

        /// <summary>Pad diameter copied onto each new well click.</summary>
        private double _NewWellPadDiameterM = FieldFeatureSet.DefaultPadDiameterM;

        /// <summary>Road bed width for the road currently being drawn or edited, in metres.</summary>
        private double _RoadBedWidthM = RoadCrossSection.DefaultBedWidthM(0.0, RoadCrossSection.DEFAULT_SIDE_SLOPE);

        /// <summary>Embankment height for the road currently being drawn or edited, in metres.</summary>
        private double _RoadHeightM = RoadCrossSection.DefaultHeightM;

        /// <summary>Side slope for the road currently being drawn or edited, as horizontal run per one vertical.</summary>
        private double _RoadSideSlope = RoadCrossSection.DEFAULT_SIDE_SLOPE;

        /// <summary>Name copied onto the next reference point, and the name of the point being edited.</summary>
        private string _NewReferenceName = "";

        /// <summary>True while the next reference-point name should advance after each click.</summary>
        private bool _ReferenceNameFollowsDefault;

        /// <summary>Number used for the next generated reference-point name.</summary>
        private int _NextReferenceNameNumber = 1;

        /// <summary>Name of the reference point currently being edited.</summary>
        private string _EditingReferenceName = "";

        /// <summary>Raised after the draft or the mode changes.</summary>
        public event Action<FeatureEditChangeKind>? Changed;

        /// <summary>Current edit operation.</summary>
        public FeatureEditMode Mode { get; private set; } = FeatureEditMode.None;

        /// <summary>Identifier of the committed feature being edited, or null while adding.</summary>
        public string? EditingFeatureId { get; private set; }

        /// <summary>True while an add or edit operation is open.</summary>
        public bool IsActive => Mode != FeatureEditMode.None;

        /// <summary>Wells placed in the current add operation, not yet saved.</summary>
        public IReadOnlyList<WellDraft> DraftWells => _DraftWells;

        /// <summary>Reference points placed in the current add operation, not yet saved.</summary>
        public IReadOnlyList<ReferencePointDraft> DraftReferencePoints => _DraftReferencePoints;

        /// <summary>Vertices of the road or well currently being drawn or dragged.</summary>
        public IReadOnlyList<DraftVertex> EditableVertices => _EditableVertices;

        /// <summary>Road bed width of the road being drawn or edited, in metres.</summary>
        public double RoadBedWidthM => _RoadBedWidthM;

        /// <summary>Embankment height of the road being drawn or edited, in metres.</summary>
        public double RoadHeightM => _RoadHeightM;

        /// <summary>Side slope of the road being drawn or edited, as horizontal run per one vertical.</summary>
        public double RoadSideSlope => _RoadSideSlope;

        /// <summary>Name that the next reference-point click will use.</summary>
        public string ReferenceName => _NewReferenceName;

        /// <summary>Name of the reference point being edited.</summary>
        public string EditingReferenceName => _EditingReferenceName;

        /// <summary>True when the map should draw an unsaved well, reference point, or centreline.</summary>
        public bool HasDraftGeometry =>
            (_DraftWells.Count > 0) || (_DraftReferencePoints.Count > 0) || (_EditableVertices.Count > 0);

        /// <summary>
        /// Starts placing new wells. Each map click adds one pad centre.
        /// </summary>
        /// <param name="PadDiameterM">Diameter applied to each new well, in metres.</param>
        public void BeginAddWells
            (
            double PadDiameterM
            )
        {
            ResetDraft(FeatureEditMode.AddWells);
            _NewWellPadDiameterM = PadDiameterM > 0.0 ? PadDiameterM : FieldFeatureSet.DefaultPadDiameterM;
            Changed?.Invoke(FeatureEditChangeKind.Structure);
        }

        /// <summary>
        /// Starts placing one road. Each map click appends a centreline vertex.
        /// </summary>
        /// <param name="BedWidthM">Road bed width applied when the road is saved, in metres.</param>
        /// <param name="HeightM">Embankment height applied when the road is saved, in metres.</param>
        /// <param name="SideSlope">Side slope applied when the road is saved, as horizontal run per one vertical.</param>
        public void BeginAddRoad
            (
            double BedWidthM,
            double HeightM,
            double SideSlope
            )
        {
            ResetDraft(FeatureEditMode.AddRoad);
            AssignRoadSection(BedWidthM, HeightM, SideSlope);
            Changed?.Invoke(FeatureEditChangeKind.Structure);
        }

        /// <summary>
        /// Starts placing reference points. Each map click adds one named point.
        /// The first name is "Reference Point" plus <paramref name="NextNumber"/>, and it advances after each click until the user types a name.
        /// </summary>
        /// <param name="NextNumber">Number used in the first generated name.</param>
        public void BeginAddReferencePoints
            (
            int NextNumber
            )
        {
            ResetDraft(FeatureEditMode.AddReferencePoints);
            _NextReferenceNameNumber = NextNumber > 0 ? NextNumber : 1;
            _ReferenceNameFollowsDefault = true;
            _NewReferenceName = ReferencePointFeature.DefaultName(_NextReferenceNameNumber);
            Changed?.Invoke(FeatureEditChangeKind.Structure);
        }

        /// <summary>
        /// Copies one well into the editor so its pad centre can be dragged.
        /// </summary>
        /// <param name="Well">Committed well to edit.</param>
        public void BeginEditWell
            (
            WellFeature Well
            )
        {
            ResetDraft(FeatureEditMode.EditWell);
            EditingFeatureId = Well.Id;
            _NewWellPadDiameterM = Well.PadDiameterM;
            _EditableVertices.Add(new DraftVertex(Well.Latitude, Well.Longitude));
            Changed?.Invoke(FeatureEditChangeKind.Structure);
        }

        /// <summary>
        /// Copies one road into the editor so its centreline vertices can be dragged.
        /// </summary>
        /// <param name="Road">Committed road to edit.</param>
        public void BeginEditRoad
            (
            RoadFeature Road
            )
        {
            ResetDraft(FeatureEditMode.EditRoad);
            EditingFeatureId = Road.Id;
            AssignRoadSection(Road.BedWidthM, Road.HeightM, Road.SideSlope);
            for (int Index = 0; Index < Road.Vertices.Count; Index++)
            {
                FeatureVertex Vertex = Road.Vertices[Index];
                _EditableVertices.Add(new DraftVertex(Vertex.Latitude, Vertex.Longitude));
            }

            Changed?.Invoke(FeatureEditChangeKind.Structure);
        }

        /// <summary>
        /// Copies one reference point into the editor so its location and name can be changed.
        /// </summary>
        /// <param name="Point">Committed reference point to edit.</param>
        public void BeginEditReferencePoint
            (
            ReferencePointFeature Point
            )
        {
            ResetDraft(FeatureEditMode.EditReferencePoint);
            EditingFeatureId = Point.Id;
            _EditingReferenceName = Point.Name;
            _EditableVertices.Add(new DraftVertex(Point.Latitude, Point.Longitude));
            Changed?.Invoke(FeatureEditChangeKind.Structure);
        }

        /// <summary>
        /// Drops the unsaved draft and leaves edit mode.
        /// </summary>
        public void Cancel
            (
            )
        {
            if (!IsActive && !HasDraftGeometry)
                return;

            ResetDraft(FeatureEditMode.None);
            Changed?.Invoke(FeatureEditChangeKind.Structure);
        }

        /// <summary>
        /// Adds one well centre. Same gesture as the point tool: one click, one point.
        /// </summary>
        /// <param name="Latitude">Click latitude in decimal degrees.</param>
        /// <param name="Longitude">Click longitude in decimal degrees.</param>
        public void AddWellAt
            (
            double Latitude,
            double Longitude
            )
        {
            if (Mode != FeatureEditMode.AddWells)
                return;

            _DraftWells.Add(new WellDraft(Latitude, Longitude, _NewWellPadDiameterM));
            Changed?.Invoke(FeatureEditChangeKind.Structure);
        }

        /// <summary>
        /// Adds one reference point. Same gesture as a well: one click, one point, with no pad.
        /// </summary>
        /// <param name="Latitude">Click latitude in decimal degrees.</param>
        /// <param name="Longitude">Click longitude in decimal degrees.</param>
        public void AddReferenceAt
            (
            double Latitude,
            double Longitude
            )
        {
            if (Mode != FeatureEditMode.AddReferencePoints)
                return;

            string Name = string.IsNullOrWhiteSpace(_NewReferenceName)
                ? ReferencePointFeature.DefaultName(_NextReferenceNameNumber)
                : _NewReferenceName;
            _DraftReferencePoints.Add(new ReferencePointDraft(Latitude, Longitude, Name));
            // A generated name advances so the next click is not given the same label. A typed name stays.
            if (_ReferenceNameFollowsDefault)
            {
                _NextReferenceNameNumber++;
                _NewReferenceName = ReferencePointFeature.DefaultName(_NextReferenceNameNumber);
            }

            Changed?.Invoke(FeatureEditChangeKind.Structure);
        }

        /// <summary>
        /// Sets the name that the next reference-point click will use.
        /// Already placed points keep the name they were given.
        /// </summary>
        /// <param name="Name">Name typed in the add header.</param>
        public void SetAddingReferenceName
            (
            string Name
            )
        {
            if (Mode != FeatureEditMode.AddReferencePoints)
                return;

            string Trimmed = Name.Trim();
            if ((Trimmed.Length == 0) || (Trimmed == _NewReferenceName))
                return;

            _NewReferenceName = Trimmed;
            _ReferenceNameFollowsDefault = false;
        }

        /// <summary>
        /// Sets the name that will be saved for the reference point being edited.
        /// The map label updates without rebuilding the list.
        /// </summary>
        /// <param name="Name">Name typed on the row.</param>
        public void SetEditingReferenceName
            (
            string Name
            )
        {
            if (Mode != FeatureEditMode.EditReferencePoint)
                return;

            string Trimmed = Name.Trim();
            if ((Trimmed.Length == 0) || (Trimmed == _EditingReferenceName))
                return;

            _EditingReferenceName = Trimmed;
            Changed?.Invoke(FeatureEditChangeKind.Geometry);
        }

        /// <summary>
        /// Sets the pad diameter for wells placed in the current add, including wells already clicked.
        /// The pad circles redraw without rebuilding the well list.
        /// </summary>
        /// <param name="PadDiameterM">Pad diameter in metres.</param>
        public void SetAddingWellPadDiameter
            (
            double PadDiameterM
            )
        {
            if ((Mode != FeatureEditMode.AddWells) || (PadDiameterM <= 0.0))
                return;

            bool ChangedDiameter = _NewWellPadDiameterM != PadDiameterM;
            _NewWellPadDiameterM = PadDiameterM;
            for (int Index = 0; Index < _DraftWells.Count; Index++)
            {
                if (_DraftWells[Index].PadDiameterM == PadDiameterM)
                    continue;

                _DraftWells[Index].PadDiameterM = PadDiameterM;
                ChangedDiameter = true;
            }

            if (ChangedDiameter)
                Changed?.Invoke(FeatureEditChangeKind.Geometry);
        }

        /// <summary>
        /// Appends one centreline vertex. Same gesture as the polyline tool: one click, one vertex.
        /// </summary>
        /// <param name="Latitude">Click latitude in decimal degrees.</param>
        /// <param name="Longitude">Click longitude in decimal degrees.</param>
        public void AppendRoadVertex
            (
            double Latitude,
            double Longitude
            )
        {
            if (Mode != FeatureEditMode.AddRoad)
                return;

            _EditableVertices.Add(new DraftVertex(Latitude, Longitude));
            // The first vertex adds the draft row. Later vertices only move the end label, so the list must not be rebuilt.
            FeatureEditChangeKind Kind = _EditableVertices.Count == 1
                ? FeatureEditChangeKind.Structure
                : FeatureEditChangeKind.Geometry;
            Changed?.Invoke(Kind);
        }

        /// <summary>
        /// Moves one vertex of the road or well currently being edited, or one vertex of a road still being drawn.
        /// </summary>
        /// <param name="Index">Vertex index. A well has only index 0.</param>
        /// <param name="Latitude">New latitude in decimal degrees.</param>
        /// <param name="Longitude">New longitude in decimal degrees.</param>
        /// <returns>True when the vertex was moved.</returns>
        public bool TryMoveEditableVertex
            (
            int Index,
            double Latitude,
            double Longitude
            )
        {
            // Add-road vertices live in the same list as an edited centreline, so a drag can fix a point before Save.
            if ((Mode != FeatureEditMode.EditWell) &&
                (Mode != FeatureEditMode.EditRoad) &&
                (Mode != FeatureEditMode.EditReferencePoint) &&
                (Mode != FeatureEditMode.AddRoad))
                return false;

            if ((Index < 0) || (Index >= _EditableVertices.Count))
                return false;

            DraftVertex Vertex = _EditableVertices[Index];
            if ((Vertex.Latitude == Latitude) && (Vertex.Longitude == Longitude))
                return true;

            Vertex.Latitude = Latitude;
            Vertex.Longitude = Longitude;
            Changed?.Invoke(FeatureEditChangeKind.Geometry);
            return true;
        }

        /// <summary>
        /// Moves one well that was clicked during add, before it is saved.
        /// </summary>
        /// <param name="Index">Index in <see cref="DraftWells"/>.</param>
        /// <param name="Latitude">New latitude in decimal degrees.</param>
        /// <param name="Longitude">New longitude in decimal degrees.</param>
        /// <returns>True when the well was moved.</returns>
        public bool TryMoveDraftWell
            (
            int Index,
            double Latitude,
            double Longitude
            )
        {
            if (Mode != FeatureEditMode.AddWells)
                return false;

            if ((Index < 0) || (Index >= _DraftWells.Count))
                return false;

            WellDraft Well = _DraftWells[Index];
            if ((Well.Latitude == Latitude) && (Well.Longitude == Longitude))
                return true;

            Well.Latitude = Latitude;
            Well.Longitude = Longitude;
            Changed?.Invoke(FeatureEditChangeKind.Geometry);
            return true;
        }

        /// <summary>
        /// Moves one reference point that was clicked during add, before it is saved.
        /// </summary>
        /// <param name="Index">Index in <see cref="DraftReferencePoints"/>.</param>
        /// <param name="Latitude">New latitude in decimal degrees.</param>
        /// <param name="Longitude">New longitude in decimal degrees.</param>
        /// <returns>True when the point was moved.</returns>
        public bool TryMoveDraftReference
            (
            int Index,
            double Latitude,
            double Longitude
            )
        {
            if (Mode != FeatureEditMode.AddReferencePoints)
                return false;

            if ((Index < 0) || (Index >= _DraftReferencePoints.Count))
                return false;

            ReferencePointDraft Point = _DraftReferencePoints[Index];
            if ((Point.Latitude == Latitude) && (Point.Longitude == Longitude))
                return true;

            Point.Latitude = Latitude;
            Point.Longitude = Longitude;
            Changed?.Invoke(FeatureEditChangeKind.Geometry);
            return true;
        }

        /// <summary>
        /// Updates the pad diameter of one unsaved well.
        /// </summary>
        /// <param name="Index">Index in <see cref="DraftWells"/>.</param>
        /// <param name="PadDiameterM">Pad diameter in metres.</param>
        public void SetDraftWellPadDiameter
            (
            int Index,
            double PadDiameterM
            )
        {
            if ((Index < 0) || (Index >= _DraftWells.Count) || (PadDiameterM <= 0.0))
                return;

            if (_DraftWells[Index].PadDiameterM == PadDiameterM)
                return;

            _DraftWells[Index].PadDiameterM = PadDiameterM;
            Changed?.Invoke(FeatureEditChangeKind.Geometry);
        }

        /// <summary>
        /// Updates the pad diameter that will be saved for the well being edited.
        /// The pad circle redraws from this value without rebuilding the well list.
        /// </summary>
        /// <param name="PadDiameterM">Pad diameter in metres.</param>
        public void SetEditingWellPadDiameter
            (
            double PadDiameterM
            )
        {
            if ((Mode != FeatureEditMode.EditWell) || (PadDiameterM <= 0.0))
                return;

            if (_NewWellPadDiameterM == PadDiameterM)
                return;

            _NewWellPadDiameterM = PadDiameterM;
            Changed?.Invoke(FeatureEditChangeKind.Geometry);
        }

        /// <summary>
        /// Updates the bed, height, and side slope that will be saved for the road being drawn or edited.
        /// A change redraws the toe and blade-width lines without rebuilding the road list.
        /// </summary>
        /// <param name="BedWidthM">Full road bed width in metres.</param>
        /// <param name="HeightM">Embankment height in metres.</param>
        /// <param name="SideSlope">Horizontal run per one vertical.</param>
        public void SetRoadSection
            (
            double BedWidthM,
            double HeightM,
            double SideSlope
            )
        {
            if ((Mode != FeatureEditMode.AddRoad) && (Mode != FeatureEditMode.EditRoad))
                return;

            bool ChangedSection = false;
            if ((BedWidthM > 0.0) && (BedWidthM != _RoadBedWidthM))
            {
                _RoadBedWidthM = BedWidthM;
                ChangedSection = true;
            }

            if ((HeightM > 0.0) && (HeightM != _RoadHeightM))
            {
                _RoadHeightM = HeightM;
                ChangedSection = true;
            }

            if ((SideSlope >= 0.0) && !double.IsNaN(SideSlope) && !double.IsInfinity(SideSlope) && (SideSlope != _RoadSideSlope))
            {
                _RoadSideSlope = SideSlope;
                ChangedSection = true;
            }

            if (ChangedSection)
                Changed?.Invoke(FeatureEditChangeKind.Geometry);
        }

        /// <summary>
        /// Pad diameter currently stored for the well being edited, in metres.
        /// </summary>
        public double EditingWellPadDiameterM => _NewWellPadDiameterM;

        /// <summary>
        /// Writes the draft into the project and leaves edit mode when the draft is complete.
        /// A road with fewer than two vertices is not a road: Save then leaves edit mode without adding it,
        /// except a single vertex is kept so the user can click the end point.
        /// </summary>
        /// <param name="Features">Project feature set that receives committed wells and roads.</param>
        /// <returns>True when at least one feature was written.</returns>
        public bool TryCommit
            (
            FieldFeatureSet Features
            )
        {
            if (Mode == FeatureEditMode.AddWells)
            {
                bool Wrote = false;
                for (int Index = 0; Index < _DraftWells.Count; Index++)
                {
                    WellDraft Draft = _DraftWells[Index];
                    if (Draft.PadDiameterM <= 0.0)
                        continue;

                    Features.AddWell(Draft.Latitude, Draft.Longitude, Draft.PadDiameterM);
                    Wrote = true;
                }

                ResetDraft(FeatureEditMode.None);
                Changed?.Invoke(FeatureEditChangeKind.Structure);
                return Wrote;
            }

            if (Mode == FeatureEditMode.AddRoad)
            {
                // A single click has a start but no end, so keep the session open.
                if (_EditableVertices.Count == 1)
                    return false;

                bool Wrote = false;
                if (_EditableVertices.Count >= FieldFeatureSet.MIN_ROAD_VERTICES)
                {
                    Features.AddRoad(CopyVertices(), _RoadBedWidthM, _RoadHeightM, _RoadSideSlope);
                    Wrote = true;
                }

                ResetDraft(FeatureEditMode.None);
                Changed?.Invoke(FeatureEditChangeKind.Structure);
                return Wrote;
            }

            if (Mode == FeatureEditMode.AddReferencePoints)
            {
                bool Wrote = false;
                for (int Index = 0; Index < _DraftReferencePoints.Count; Index++)
                {
                    ReferencePointDraft Draft = _DraftReferencePoints[Index];
                    if (string.IsNullOrWhiteSpace(Draft.Name))
                        continue;

                    Features.AddReferencePoint(Draft.Latitude, Draft.Longitude, Draft.Name);
                    Wrote = true;
                }

                ResetDraft(FeatureEditMode.None);
                Changed?.Invoke(FeatureEditChangeKind.Structure);
                return Wrote;
            }

            if (Mode == FeatureEditMode.EditWell)
            {
                WellFeature? Well = EditingFeatureId == null ? null : Features.FindWell(EditingFeatureId);
                bool Wrote = false;
                if ((Well != null) && (_EditableVertices.Count == 1) && (_NewWellPadDiameterM > 0.0))
                {
                    Well.Latitude = _EditableVertices[0].Latitude;
                    Well.Longitude = _EditableVertices[0].Longitude;
                    Well.PadDiameterM = _NewWellPadDiameterM;
                    Wrote = true;
                }

                ResetDraft(FeatureEditMode.None);
                Changed?.Invoke(FeatureEditChangeKind.Structure);
                return Wrote;
            }

            if (Mode == FeatureEditMode.EditRoad)
            {
                RoadFeature? Road = EditingFeatureId == null ? null : Features.FindRoad(EditingFeatureId);
                bool Wrote = false;
                if ((Road != null) &&
                    (_EditableVertices.Count >= FieldFeatureSet.MIN_ROAD_VERTICES) &&
                    (_RoadBedWidthM > 0.0) &&
                    (_RoadHeightM > 0.0) &&
                    (_RoadSideSlope >= 0.0))
                {
                    Road.ReplaceVertices(CopyVertices());
                    Road.BedWidthM = _RoadBedWidthM;
                    Road.HeightM = _RoadHeightM;
                    Road.SideSlope = _RoadSideSlope;
                    Wrote = true;
                }

                ResetDraft(FeatureEditMode.None);
                Changed?.Invoke(FeatureEditChangeKind.Structure);
                return Wrote;
            }

            if (Mode == FeatureEditMode.EditReferencePoint)
            {
                ReferencePointFeature? Point = EditingFeatureId == null ? null : Features.FindReferencePoint(EditingFeatureId);
                bool Wrote = false;
                if ((Point != null) && (_EditableVertices.Count == 1) && !string.IsNullOrWhiteSpace(_EditingReferenceName))
                {
                    Point.Latitude = _EditableVertices[0].Latitude;
                    Point.Longitude = _EditableVertices[0].Longitude;
                    Point.Name = _EditingReferenceName;
                    Wrote = true;
                }

                ResetDraft(FeatureEditMode.None);
                Changed?.Invoke(FeatureEditChangeKind.Structure);
                return Wrote;
            }

            return false;
        }

        /// <summary>
        /// Clears the draft and sets the mode without notifying listeners.
        /// </summary>
        /// <param name="Mode">Mode to enter.</param>
        private void ResetDraft
            (
            FeatureEditMode Mode
            )
        {
            _DraftWells.Clear();
            _DraftReferencePoints.Clear();
            _EditableVertices.Clear();
            EditingFeatureId = null;
            _NewReferenceName = "";
            _EditingReferenceName = "";
            _ReferenceNameFollowsDefault = false;
            this.Mode = Mode;
        }

        /// <summary>
        /// Copies editable vertices into immutable centreline positions.
        /// </summary>
        /// <returns>Centreline suitable for a committed road.</returns>
        private List<FeatureVertex> CopyVertices
            (
            )
        {
            List<FeatureVertex> Vertices = new List<FeatureVertex>(_EditableVertices.Count);
            for (int Index = 0; Index < _EditableVertices.Count; Index++)
            {
                DraftVertex Vertex = _EditableVertices[Index];
                Vertices.Add(new FeatureVertex(Vertex.Latitude, Vertex.Longitude));
            }

            return Vertices;
        }

        /// <summary>
        /// Stores a road cross-section, substituting the defaults when a value is not usable.
        /// </summary>
        /// <param name="BedWidthM">Full road bed width in metres.</param>
        /// <param name="HeightM">Embankment height in metres.</param>
        /// <param name="SideSlope">Horizontal run per one vertical.</param>
        private void AssignRoadSection
            (
            double BedWidthM,
            double HeightM,
            double SideSlope
            )
        {
            _RoadBedWidthM = BedWidthM > 0.0
                ? BedWidthM
                : RoadCrossSection.DefaultBedWidthM(0.0, RoadCrossSection.DEFAULT_SIDE_SLOPE);
            _RoadHeightM = HeightM > 0.0 ? HeightM : RoadCrossSection.DefaultHeightM;
            _RoadSideSlope = (SideSlope >= 0.0) && !double.IsNaN(SideSlope) && !double.IsInfinity(SideSlope)
                ? SideSlope
                : RoadCrossSection.DEFAULT_SIDE_SLOPE;
        }
    }
}
