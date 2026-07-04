namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// Projects a geographic location to viewport screen coordinates for erase hit testing.
    /// </summary>
    /// <param name="Latitude">Point latitude in decimal degrees.</param>
    /// <param name="Longitude">Point longitude in decimal degrees.</param>
    /// <param name="ScreenX">Output horizontal screen coordinate in pixels.</param>
    /// <param name="ScreenY">Output vertical screen coordinate in pixels.</param>
    /// <returns>True when projection succeeds.</returns>
    public delegate bool ToolDataProjectToScreenHandler
        (
        double Latitude,
        double Longitude,
        out float ScreenX,
        out float ScreenY
        );
}
