namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// Active map-editing tool selected in the left sidebar TOOLS section (TOOLS-SPEC §3).
    /// </summary>
    public enum MapEditingToolKind
    {
        /// <summary>No map-editing tool is active.</summary>
        None = 0,

        /// <summary>Add Point toggle tool (TOOLS-SPEC §3.1).</summary>
        AddPoint = 1,

        /// <summary>Add Polyline toggle tool (TOOLS-SPEC §3.2).</summary>
        AddPolyline = 2,

        /// <summary>Erase toggle tool (TOOLS-SPEC §3.3).</summary>
        Erase = 3,
    }
}
