namespace AgGradeDesigner.Core.Features
{
    /// <summary>
    /// What the feature editor is doing. Independent of the sidebar map tools.
    /// </summary>
    public enum FeatureEditMode
    {
        /// <summary>No well, road, or reference-point edit is in progress.</summary>
        None = 0,

        /// <summary>Clicks place new well centres until Save.</summary>
        AddWells = 1,

        /// <summary>Clicks append centreline vertices for one new road until Save.</summary>
        AddRoad = 2,

        /// <summary>The pad centre of one existing well can be dragged until Save.</summary>
        EditWell = 3,

        /// <summary>The centreline vertices of one existing road can be dragged until Save.</summary>
        EditRoad = 4,

        /// <summary>Clicks place new reference points until Save.</summary>
        AddReferencePoints = 5,

        /// <summary>One existing reference point can be moved until Save.</summary>
        EditReferencePoint = 6,
    }
}
