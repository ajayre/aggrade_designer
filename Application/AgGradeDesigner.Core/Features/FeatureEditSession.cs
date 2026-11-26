namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// Places and edits wells and roads. Clicks add points the way the point and polyline
    /// tools do, but this session does not call those tools. Save writes the draft into the project.
    /// </summary>
    public sealed class FeatureEditSession
    {
        /// <summary>Wells clicked during <see cref="FeatureEditMode.AddWells"/>.</summary>
        private readonly List<WellDraft> _DraftWells = new List<WellDraft>();

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

        /// <summary>Vertices of the road or well currently being drawn or dragged.</summary>
        public IReadOnlyList<DraftVertex> EditableVertices => _EditableVertices;

        /// <summary>Road bed width of the road being drawn or edited, in metres.</summary>
        public double RoadBedWidthM => _RoadBedWidthM;

        /// <summary>Embankment height of the road being drawn or edited, in metres.</summary>
        public double RoadHeightM => _RoadHeightM;

        /// <summary>Side slope of the road being drawn or edited, as horizontal run per one vertical.</summary>
        public double RoadSideSlope => _RoadSideSlope;

        /// <summary>True when the map should draw an unsaved well or centreline.</summary>
        public bool HasDraftGeometry => (_DraftWells.Count > 0) || (_EditableVertices.Count > 0);

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
            _EditableVertices.Clear();
            EditingFeatureId = null;
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
