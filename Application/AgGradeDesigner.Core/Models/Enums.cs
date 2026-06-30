namespace AgGradeDesigner.Core.Models
{
    /// <summary>
    /// Primary viewer presentation mode (plan map versus orbit camera).
    /// </summary>
    public enum ViewMode
    {
        /// <summary>North-up plan map with pan and zoom.</summary>
        TwoD,

        /// <summary>Orbit camera over a terrain mesh.</summary>
        ThreeD
    }

    /// <summary>
    /// Which elevation surface is shown on the field overlay.
    /// </summary>
    public enum FieldContentView
    {
        /// <summary>No field surface overlay.</summary>
        None,

        /// <summary>Existing survey surface.</summary>
        Existing,

        /// <summary>Proposed design surface.</summary>
        Proposed,

        /// <summary>Cut/fill delta between existing and proposed.</summary>
        CutFill
    }

    /// <summary>
    /// Original file format of the loaded survey data.
    /// </summary>
    public enum SurveySourceFormat
    {
        /// <summary>No survey loaded or format unknown.</summary>
        None,

        /// <summary>Comma-separated AGS survey file.</summary>
        Ags,

        /// <summary>Trimble multiplane tab-delimited text file.</summary>
        Multiplane,

        /// <summary>AgGrade Designer grid export (.agd).</summary>
        Agd
    }
}
