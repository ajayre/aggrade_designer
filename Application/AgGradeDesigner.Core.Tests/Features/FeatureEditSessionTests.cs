using AgGradeDesigner.Core;
using AgGradeDesigner.Core.Features;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Persistence;
using AgGradeDesigner.Core.Tests.TestFixtures;
using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Tests.Features
{
    /// <summary>
    /// Tests for well and road placement, save, and project round-trip.
    /// </summary>
    public sealed class FeatureEditSessionTests
    {
        /// <summary>Latitude used for the first placed point.</summary>
        private const double FIRST_LATITUDE = 40.1;

        /// <summary>Longitude used for the first placed point.</summary>
        private const double FIRST_LONGITUDE = -93.4;

        /// <summary>Latitude used for the second placed point.</summary>
        private const double SECOND_LATITUDE = 40.2;

        /// <summary>Longitude used for the second placed point.</summary>
        private const double SECOND_LONGITUDE = -93.5;

        /// <summary>Latitude a well is dragged to.</summary>
        private const double MOVED_LATITUDE = 40.15;

        /// <summary>Longitude a well is dragged to.</summary>
        private const double MOVED_LONGITUDE = -93.45;

        /// <summary>Bed width stored to prove save and load keep a non-default road, in feet.</summary>
        private const double STORED_BED_FT = 20.0;

        /// <summary>Height stored to prove save and load keep a non-default road, in feet.</summary>
        private const double STORED_HEIGHT_FT = 2.0;

        /// <summary>Side slope stored to prove save and load keep a non-default road.</summary>
        private const double STORED_SIDE_SLOPE = 4.0;

        /// <summary>
        /// Saving after well clicks commits each point with the default pad diameter.
        /// </summary>
        [Fact]
        public void Save_CommitsClickedWells
            (
            )
        {
            FieldFeatureSet Features = new FieldFeatureSet();
            FeatureEditSession Session = new FeatureEditSession();
            Session.BeginAddWells(FieldFeatureSet.DefaultPadDiameterM);
            Session.AddWellAt(FIRST_LATITUDE, FIRST_LONGITUDE);
            Session.AddWellAt(SECOND_LATITUDE, SECOND_LONGITUDE);

            bool Wrote = Session.TryCommit(Features);

            Assert.True(Wrote);
            Assert.False(Session.IsActive);
            Assert.Equal(2, Features.Wells.Count);
            Assert.Equal(FIRST_LATITUDE, Features.Wells[0].Latitude);
            Assert.Equal(FIRST_LONGITUDE, Features.Wells[0].Longitude);
            Assert.Equal(FieldFeatureSet.DefaultPadDiameterM, Features.Wells[0].PadDiameterM);
            Assert.Equal("well_1", Features.Wells[0].Id);
            Assert.Equal("well_2", Features.Wells[1].Id);
        }

        /// <summary>
        /// A road needs a start and an end. One click stays in edit mode; the second click can be saved.
        /// </summary>
        [Fact]
        public void Save_CommitsRoadOnlyAfterTwoVertices
            (
            )
        {
            FieldFeatureSet Features = new FieldFeatureSet();
            FeatureEditSession Session = new FeatureEditSession();
            BeginDefaultRoad(Session);
            Session.AppendRoadVertex(FIRST_LATITUDE, FIRST_LONGITUDE);

            bool Incomplete = Session.TryCommit(Features);

            Assert.False(Incomplete);
            Assert.Equal(FeatureEditMode.AddRoad, Session.Mode);
            Assert.Empty(Features.Roads);

            Session.AppendRoadVertex(SECOND_LATITUDE, SECOND_LONGITUDE);
            bool Wrote = Session.TryCommit(Features);

            Assert.True(Wrote);
            Assert.False(Session.IsActive);
            Assert.Single(Features.Roads);
            Assert.Equal(FIRST_LATITUDE, Features.Roads[0].Start.Latitude);
            Assert.Equal(SECOND_LONGITUDE, Features.Roads[0].End.Longitude);
            Assert.Equal(
                RoadCrossSection.DefaultBedWidthM(0.0, RoadCrossSection.DEFAULT_SIDE_SLOPE),
                Features.Roads[0].BedWidthM);
            Assert.Equal(RoadCrossSection.DefaultHeightM, Features.Roads[0].HeightM);
            Assert.Equal(RoadCrossSection.DEFAULT_SIDE_SLOPE, Features.Roads[0].SideSlope);
        }

        /// <summary>
        /// Dragging a well updates it only when Save is used. Cancel leaves the committed centre.
        /// </summary>
        [Fact]
        public void Save_CommitsDraggedWell_CancelLeavesOriginal
            (
            )
        {
            FieldFeatureSet Features = new FieldFeatureSet();
            WellFeature Well = Features.AddWell(FIRST_LATITUDE, FIRST_LONGITUDE, FieldFeatureSet.DefaultPadDiameterM);
            FeatureEditSession Session = new FeatureEditSession();
            Session.BeginEditWell(Well);
            Session.TryMoveEditableVertex(0, MOVED_LATITUDE, MOVED_LONGITUDE);
            Session.Cancel();

            Assert.Equal(FIRST_LATITUDE, Well.Latitude);
            Assert.False(Session.IsActive);

            Session.BeginEditWell(Well);
            Session.TryMoveEditableVertex(0, MOVED_LATITUDE, MOVED_LONGITUDE);
            bool Wrote = Session.TryCommit(Features);

            Assert.True(Wrote);
            Assert.Equal(MOVED_LATITUDE, Well.Latitude);
            Assert.Equal(MOVED_LONGITUDE, Well.Longitude);
            Assert.Equal("well_1", Well.Id);
        }

        /// <summary>
        /// Dragging a well while it is still being added moves that well, and Save keeps the new centre.
        /// </summary>
        [Fact]
        public void Drag_MovesDraftWellBeforeSave
            (
            )
        {
            FieldFeatureSet Features = new FieldFeatureSet();
            FeatureEditSession Session = new FeatureEditSession();
            Session.BeginAddWells(FieldFeatureSet.DefaultPadDiameterM);
            Session.AddWellAt(FIRST_LATITUDE, FIRST_LONGITUDE);

            bool Moved = Session.TryMoveDraftWell(0, MOVED_LATITUDE, MOVED_LONGITUDE);
            bool Wrote = Session.TryCommit(Features);

            Assert.True(Moved);
            Assert.True(Wrote);
            Assert.Equal(MOVED_LATITUDE, Features.Wells[0].Latitude);
            Assert.Equal(MOVED_LONGITUDE, Features.Wells[0].Longitude);
        }

        /// <summary>
        /// Dragging a centreline point while the road is still being added moves that point.
        /// </summary>
        [Fact]
        public void Drag_MovesRoadVertexBeforeSave
            (
            )
        {
            FieldFeatureSet Features = new FieldFeatureSet();
            FeatureEditSession Session = new FeatureEditSession();
            BeginDefaultRoad(Session);
            Session.AppendRoadVertex(FIRST_LATITUDE, FIRST_LONGITUDE);
            Session.AppendRoadVertex(SECOND_LATITUDE, SECOND_LONGITUDE);

            bool Moved = Session.TryMoveEditableVertex(0, MOVED_LATITUDE, MOVED_LONGITUDE);
            bool Wrote = Session.TryCommit(Features);

            Assert.True(Moved);
            Assert.True(Wrote);
            Assert.Equal(MOVED_LATITUDE, Features.Roads[0].Start.Latitude);
            Assert.Equal(MOVED_LONGITUDE, Features.Roads[0].Start.Longitude);
            Assert.Equal(SECOND_LATITUDE, Features.Roads[0].End.Latitude);
        }

        /// <summary>
        /// The second road click updates the draft line and does not ask the list to rebuild.
        /// </summary>
        [Fact]
        public void AppendRoadVertex_SecondPointIsGeometryChange
            (
            )
        {
            FeatureEditSession Session = new FeatureEditSession();
            FeatureEditChangeKind LastKind = FeatureEditChangeKind.Structure;
            int Raises = 0;
            Session.Changed += Kind =>
            {
                LastKind = Kind;
                Raises++;
            };

            BeginDefaultRoad(Session);
            Session.AppendRoadVertex(FIRST_LATITUDE, FIRST_LONGITUDE);
            Session.AppendRoadVertex(SECOND_LATITUDE, SECOND_LONGITUDE);

            Assert.Equal(3, Raises);
            Assert.Equal(FeatureEditChangeKind.Geometry, LastKind);
        }

        /// <summary>
        /// Removing a well or road drops only that feature and leaves the other ids in place.
        /// </summary>
        [Fact]
        public void Remove_DropsNamedWellAndRoad
            (
            )
        {
            FieldFeatureSet Features = new FieldFeatureSet();
            Features.AddWell(FIRST_LATITUDE, FIRST_LONGITUDE, FieldFeatureSet.DefaultPadDiameterM);
            Features.AddWell(SECOND_LATITUDE, SECOND_LONGITUDE, FieldFeatureSet.DefaultPadDiameterM);
            Features.AddRoad(
                new FeatureVertex[]
                {
                    new FeatureVertex(FIRST_LATITUDE, FIRST_LONGITUDE),
                    new FeatureVertex(SECOND_LATITUDE, SECOND_LONGITUDE),
                },
                RoadCrossSection.DefaultBedWidthM(0.0, RoadCrossSection.DEFAULT_SIDE_SLOPE),
                RoadCrossSection.DefaultHeightM,
                RoadCrossSection.DEFAULT_SIDE_SLOPE);

            bool RemovedWell = Features.RemoveWell("well_1");
            bool RemovedRoad = Features.RemoveRoad("road_1");

            Assert.True(RemovedWell);
            Assert.True(RemovedRoad);
            Assert.Single(Features.Wells);
            Assert.Equal("well_2", Features.Wells[0].Id);
            Assert.Empty(Features.Roads);
            Assert.False(Features.RemoveWell("well_1"));
        }

        /// <summary>
        /// Imperial pad diameter text converts to metres and back.
        /// </summary>
        [Fact]
        public void LengthFormat_RoundTripsFeet
            (
            )
        {
            string Text = FeatureLengthFormat.FormatDisplay(FieldFeatureSet.DefaultPadDiameterM, DisplayUnitSystem.Imperial);
            bool Parsed = FeatureLengthFormat.TryParseDisplay(Text, DisplayUnitSystem.Imperial, out double Metres);

            Assert.Equal("90", Text);
            Assert.True(Parsed);
            Assert.Equal(FieldFeatureSet.DefaultPadDiameterM, Metres, 6);
        }

        /// <summary>
        /// Wells and roads survive a project save and load.
        /// </summary>
        [Fact]
        public void SaveAndLoad_PreservesWellsAndRoads
            (
            )
        {
            string Path = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                ProjectDocument Project = new ProjectDocument();
                Project.Features.AddWell(FIRST_LATITUDE, FIRST_LONGITUDE, FieldFeatureSet.DefaultPadDiameterM);
                double BedWidthM = STORED_BED_FT * FieldConstants.METERS_PER_FOOT;
                double HeightM = STORED_HEIGHT_FT * FieldConstants.METERS_PER_FOOT;
                Project.Features.AddRoad(
                    new FeatureVertex[]
                    {
                        new FeatureVertex(FIRST_LATITUDE, FIRST_LONGITUDE),
                        new FeatureVertex(SECOND_LATITUDE, SECOND_LONGITUDE),
                    },
                    BedWidthM,
                    HeightM,
                    STORED_SIDE_SLOPE);

                AgDesignSerializer.Save(Project, Path);
                ProjectDocument Loaded = AgDesignSerializer.Load(Path);

                Assert.Single(Loaded.Features.Wells);
                Assert.Equal(FIRST_LATITUDE, Loaded.Features.Wells[0].Latitude, 8);
                Assert.Equal(FieldFeatureSet.DefaultPadDiameterM, Loaded.Features.Wells[0].PadDiameterM, 8);
                Assert.Single(Loaded.Features.Roads);
                Assert.Equal(SECOND_LONGITUDE, Loaded.Features.Roads[0].End.Longitude, 8);
                Assert.Equal(BedWidthM, Loaded.Features.Roads[0].BedWidthM, 8);
                Assert.Equal(HeightM, Loaded.Features.Roads[0].HeightM, 8);
                Assert.Equal(STORED_SIDE_SLOPE, Loaded.Features.Roads[0].SideSlope, 8);
                Assert.Equal("well_2", Loaded.Features.AddWell(0, 0, 1).Id);
            }
            finally
            {
                File.Delete(Path);
            }
        }

        /// <summary>
        /// Changing the bed, height, or slope updates the draft without asking the list to rebuild.
        /// </summary>
        [Fact]
        public void SetRoadSection_IsGeometryChange
            (
            )
        {
            FeatureEditSession Session = new FeatureEditSession();
            FeatureEditChangeKind LastKind = FeatureEditChangeKind.Structure;
            int Raises = 0;
            Session.Changed += Kind =>
            {
                LastKind = Kind;
                Raises++;
            };

            BeginDefaultRoad(Session);
            int AfterBegin = Raises;
            Session.SetRoadSection(
                STORED_BED_FT * FieldConstants.METERS_PER_FOOT,
                STORED_HEIGHT_FT * FieldConstants.METERS_PER_FOOT,
                STORED_SIDE_SLOPE);

            Assert.Equal(AfterBegin + 1, Raises);
            Assert.Equal(FeatureEditChangeKind.Geometry, LastKind);
            Assert.Equal(STORED_SIDE_SLOPE, Session.RoadSideSlope);
        }

        /// <summary>
        /// The diameter typed while adding applies to the next click and to wells already placed.
        /// </summary>
        [Fact]
        public void SetAddingWellPadDiameter_UpdatesDrafts
            (
            )
        {
            FeatureEditSession Session = new FeatureEditSession();
            Session.BeginAddWells(FieldFeatureSet.DefaultPadDiameterM);
            Session.AddWellAt(FIRST_LATITUDE, FIRST_LONGITUDE);
            FeatureEditChangeKind LastKind = FeatureEditChangeKind.Structure;
            Session.Changed += Kind => LastKind = Kind;

            double EditedDiameterM = FieldFeatureSet.DefaultPadDiameterM + FieldFeatureSet.DefaultPadDiameterM;
            Session.SetAddingWellPadDiameter(EditedDiameterM);

            Assert.Equal(FeatureEditChangeKind.Geometry, LastKind);
            Assert.Equal(EditedDiameterM, Session.DraftWells[0].PadDiameterM);

            Session.AddWellAt(SECOND_LATITUDE, SECOND_LONGITUDE);
            Assert.Equal(EditedDiameterM, Session.DraftWells[1].PadDiameterM);
        }

        /// <summary>
        /// Changing a well's pad diameter redraws the pad circle without rebuilding the well list.
        /// </summary>
        [Fact]
        public void SetEditingWellPadDiameter_IsGeometryChange
            (
            )
        {
            FieldFeatureSet Features = new FieldFeatureSet();
            WellFeature Well = Features.AddWell(FIRST_LATITUDE, FIRST_LONGITUDE, FieldFeatureSet.DefaultPadDiameterM);
            FeatureEditSession Session = new FeatureEditSession();
            FeatureEditChangeKind LastKind = FeatureEditChangeKind.Structure;
            int Raises = 0;
            Session.Changed += Kind =>
            {
                LastKind = Kind;
                Raises++;
            };

            Session.BeginEditWell(Well);
            int AfterBegin = Raises;
            double EditedDiameterM = FieldFeatureSet.DefaultPadDiameterM + FieldFeatureSet.DefaultPadDiameterM;
            Session.SetEditingWellPadDiameter(EditedDiameterM);

            Assert.Equal(AfterBegin + 1, Raises);
            Assert.Equal(FeatureEditChangeKind.Geometry, LastKind);
            Assert.Equal(EditedDiameterM, Session.EditingWellPadDiameterM);
        }

        /// <summary>
        /// A generated reference-point name advances after each click, and a typed name stays.
        /// Save keeps the name and the location, and does not reuse a deleted id.
        /// </summary>
        [Fact]
        public void ReferencePoint_NameAdvancesUntilTheUserTypesOne
            (
            )
        {
            FieldFeatureSet Features = new FieldFeatureSet();
            FeatureEditSession Session = new FeatureEditSession();
            Session.BeginAddReferencePoints(Features.NextReferenceNumber);
            Assert.Equal(ReferencePointFeature.DefaultName(1), Session.ReferenceName);

            Session.AddReferenceAt(FIRST_LATITUDE, FIRST_LONGITUDE);
            Assert.Equal(ReferencePointFeature.DefaultName(1), Session.DraftReferencePoints[0].Name);
            Assert.Equal(ReferencePointFeature.DefaultName(2), Session.ReferenceName);

            Session.SetAddingReferenceName("North gate");
            Session.AddReferenceAt(SECOND_LATITUDE, SECOND_LONGITUDE);
            Assert.Equal("North gate", Session.DraftReferencePoints[1].Name);
            Assert.Equal("North gate", Session.ReferenceName);

            Assert.True(Session.TryCommit(Features));
            Assert.Equal(2, Features.ReferencePoints.Count);
            Assert.Equal(FIRST_LATITUDE, Features.ReferencePoints[0].Latitude, 8);
            Assert.Equal("North gate", Features.ReferencePoints[1].Name);
            Assert.Equal(3, Features.NextReferenceNumber);

            string RemovedId = Features.ReferencePoints[0].Id;
            Assert.True(Features.RemoveReferencePoint(RemovedId));
            ReferencePointFeature Added = Features.AddReferencePoint(FIRST_LATITUDE, FIRST_LONGITUDE, "Again");
            Assert.NotEqual(RemovedId, Added.Id);
        }

        /// <summary>
        /// Editing a reference point stores a new name and location, and the project file keeps both.
        /// </summary>
        [Fact]
        public void ReferencePoint_EditAndProjectRoundTrip
            (
            )
        {
            FieldFeatureSet Features = new FieldFeatureSet();
            ReferencePointFeature Point = Features.AddReferencePoint(FIRST_LATITUDE, FIRST_LONGITUDE, "Gate");
            FeatureEditSession Session = new FeatureEditSession();
            FeatureEditChangeKind LastKind = FeatureEditChangeKind.Structure;
            Session.Changed += Kind => LastKind = Kind;

            Session.BeginEditReferencePoint(Point);
            Session.SetEditingReferenceName("South gate");
            Assert.Equal(FeatureEditChangeKind.Geometry, LastKind);
            Session.TryMoveEditableVertex(0, SECOND_LATITUDE, SECOND_LONGITUDE);
            Assert.True(Session.TryCommit(Features));
            Assert.Equal("South gate", Point.Name);
            Assert.Equal(SECOND_LONGITUDE, Point.Longitude, 8);

            string Path = TempFile.Path(ProjectDocument.FILE_EXTENSION);
            try
            {
                ProjectDocument Project = new ProjectDocument();
                Project.Features.AddReferencePoint(Point.Latitude, Point.Longitude, Point.Name);
                AgDesignSerializer.Save(Project, Path);
                ProjectDocument Loaded = AgDesignSerializer.Load(Path);
                Assert.Single(Loaded.Features.ReferencePoints);
                Assert.Equal("South gate", Loaded.Features.ReferencePoints[0].Name);
                Assert.Equal(SECOND_LATITUDE, Loaded.Features.ReferencePoints[0].Latitude, 8);
                Assert.Equal("reference_2", Loaded.Features.AddReferencePoint(0, 0, "Next").Id);
            }
            finally
            {
                File.Delete(Path);
            }
        }

        /// <summary>
        /// Starts a road with the reference bed, a 1 ft height, and a 3:1 slope.
        /// </summary>
        /// <param name="Session">Session that receives the road.</param>
        private static void BeginDefaultRoad
            (
            FeatureEditSession Session
            )
        {
            Session.BeginAddRoad(
                RoadCrossSection.DefaultBedWidthM(0.0, RoadCrossSection.DEFAULT_SIDE_SLOPE),
                RoadCrossSection.DefaultHeightM,
                RoadCrossSection.DEFAULT_SIDE_SLOPE);
        }
    }
}
