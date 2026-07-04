namespace AgGradeDesigner.Core.MapTools
{
    /// <summary>
    /// Named constants for map-editing tool data and erase interaction (TOOLS-SPEC §3–§4).
    /// </summary>
    static public class ToolDataConstants
    {
        /// <summary>Prefix for stable tool point identifiers returned by <c>get_tool_data</c>.</summary>
        public const string TOOL_POINT_ID_PREFIX = "tp_";

        /// <summary>Prefix for stable tool polyline identifiers returned by <c>get_tool_data</c>.</summary>
        public const string TOOL_POLYLINE_ID_PREFIX = "pl_";

        /// <summary>Coordinate frame token for <c>get_tool_data</c> JSON (AI Features §9.10.3).</summary>
        public const string COORDINATE_FRAME_UTM_METRES = "utm_metres";

        /// <summary>Elevation reference token for <c>get_tool_data</c> JSON (AI Features §9.10.3).</summary>
        public const string ELEVATION_REFERENCE_PROPOSED = "proposed";

        /// <summary>Minimum positive identifier ordinal assigned to new tool features.</summary>
        public const int FIRST_FEATURE_ID = 1;

        /// <summary>Red component for tool polyline segment stroke (contrasting colour, TOOLS-SPEC §3.2.8).</summary>
        public const byte TOOL_POLYLINE_STROKE_RED = 255;

        /// <summary>Green component for tool polyline segment stroke.</summary>
        public const byte TOOL_POLYLINE_STROKE_GREEN = 120;

        /// <summary>Blue component for tool polyline segment stroke.</summary>
        public const byte TOOL_POLYLINE_STROKE_BLUE = 0;

        /// <summary>Alpha component for tool polyline segment stroke.</summary>
        public const byte TOOL_POLYLINE_STROKE_ALPHA = 255;

        /// <summary>Red component for tool point marker fill.</summary>
        public const byte TOOL_POINT_FILL_RED = 255;

        /// <summary>Green component for tool point marker fill.</summary>
        public const byte TOOL_POINT_FILL_GREEN = 120;

        /// <summary>Blue component for tool point marker fill.</summary>
        public const byte TOOL_POINT_FILL_BLUE = 0;

        /// <summary>Alpha component for tool point marker fill.</summary>
        public const byte TOOL_POINT_FILL_ALPHA = 255;

        /// <summary>Red component for erase-rectangle border (TOOLS-SPEC §3.3.3).</summary>
        public const byte ERASE_RECT_BORDER_RED = 255;

        /// <summary>Green component for erase-rectangle border.</summary>
        public const byte ERASE_RECT_BORDER_GREEN = 255;

        /// <summary>Blue component for erase-rectangle border.</summary>
        public const byte ERASE_RECT_BORDER_BLUE = 255;

        /// <summary>Alpha component for erase-rectangle border.</summary>
        public const byte ERASE_RECT_BORDER_ALPHA = 255;

        /// <summary>Red component for erase-rectangle translucent fill.</summary>
        public const byte ERASE_RECT_FILL_RED = 144;

        /// <summary>Green component for erase-rectangle translucent fill.</summary>
        public const byte ERASE_RECT_FILL_GREEN = 238;

        /// <summary>Blue component for erase-rectangle translucent fill.</summary>
        public const byte ERASE_RECT_FILL_BLUE = 144;

        /// <summary>Alpha component for erase-rectangle translucent fill.</summary>
        public const byte ERASE_RECT_FILL_ALPHA = 96;

        /// <summary>Red component for erase preview highlight on selected tool points.</summary>
        public const byte ERASE_PREVIEW_POINT_RED = 255;

        /// <summary>Green component for erase preview highlight on selected tool points.</summary>
        public const byte ERASE_PREVIEW_POINT_GREEN = 64;

        /// <summary>Blue component for erase preview highlight on selected tool points.</summary>
        public const byte ERASE_PREVIEW_POINT_BLUE = 64;

        /// <summary>Alpha component for erase preview highlight on selected tool points.</summary>
        public const byte ERASE_PREVIEW_POINT_ALPHA = 255;

        /// <summary>Screen-space radius of tool point markers in pixels.</summary>
        public const float TOOL_POINT_RADIUS_PX = 4f;

        /// <summary>White outline stroke width around tool point markers in pixels.</summary>
        public const float TOOL_POINT_OUTLINE_WIDTH_PX = 1.5f;

        /// <summary>Screen-space stroke width of tool polyline segments in pixels.</summary>
        public const float TOOL_POLYLINE_STROKE_WIDTH_PX = 2f;

        /// <summary>Extra width added to each side of polyline segments for the white outline in pixels.</summary>
        public const float TOOL_POLYLINE_OUTLINE_EXTRA_PX = 1.5f;

        /// <summary>Minimum erase-rectangle width and height in pixels (avoids zero-area vertical/horizontal drags).</summary>
        public const float ERASE_RECT_MIN_DIMENSION_PX = 1f;

        /// <summary>Red component for tool feature white outline stroke.</summary>
        public const byte TOOL_OUTLINE_RED = 255;

        /// <summary>Green component for tool feature white outline stroke.</summary>
        public const byte TOOL_OUTLINE_GREEN = 255;

        /// <summary>Blue component for tool feature white outline stroke.</summary>
        public const byte TOOL_OUTLINE_BLUE = 255;

        /// <summary>Alpha component for tool feature white outline stroke.</summary>
        public const byte TOOL_OUTLINE_ALPHA = 255;

        /// <summary>Screen-space stroke width of the erase rectangle border in pixels.</summary>
        public const float ERASE_RECT_BORDER_WIDTH_PX = 1.5f;

        /// <summary>Maximum pointer movement in pixels treated as a click rather than a pan gesture.</summary>
        public const double MAP_CLICK_DRAG_THRESHOLD_PX = 5.0;
    }
}
