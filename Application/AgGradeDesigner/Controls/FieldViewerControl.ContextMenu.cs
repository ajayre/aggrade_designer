using System;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using AgGradeDesigner.Core.Models;

namespace AgGradeDesigner.Controls
{
    /// <summary>
    /// Viewport context-menu handling for <see cref="FieldViewerControl"/>.
    /// </summary>
    public partial class FieldViewerControl
    {
        /// <summary>Pointer movement below this threshold is treated as a click, not a drag.</summary>
        private const double CONTEXT_MENU_DRAG_THRESHOLD_PX = 4.0;

        /// <summary>Asset URI for the pivot context-menu icon.</summary>
        private const string PIVOT_CONTEXT_MENU_ICON_URI = "avares://AgGradeDesigner/Assets/Icons/pivot_32px.png";

        /// <summary>CSS class applied to viewport context menus for shared item sizing.</summary>
        private const string CONTEXT_MENU_STYLE_CLASS = "context-menu";

        /// <summary>CSS class applied to icons shown in viewport context menus.</summary>
        private const string CONTEXT_MENU_ICON_STYLE_CLASS = "context-menu-icon";

        /// <summary>Screen position where the user pressed the right mouse button for the context menu.</summary>
        private Point? _PivotContextMenuPressPoint;

        /// <summary>Latitude captured at right-button press for the pivot context menu.</summary>
        private double _PivotContextMenuPressLat;

        /// <summary>Longitude captured at right-button press for the pivot context menu.</summary>
        private double _PivotContextMenuPressLon;

        /// <summary>True when lat/lon were resolved for the current pivot context-menu press.</summary>
        private bool _PivotContextMenuPressValid;

        /// <summary>True while waiting to distinguish a 3D right-click from a right-drag.</summary>
        private bool _PendingRightPress;

        /// <summary>Origin of a pending or active right-button gesture.</summary>
        private Point _RightPressOrigin;

        /// <summary>Raised when the user chooses Set Pivot Location from the viewport context menu.</summary>
        public event Action<double, double>? HaulPivotLocationSetRequested;

        /// <summary>Lazily built context menu for setting the haul pivot location.</summary>
        private ContextMenu? _PivotContextMenu;

        /// <summary>
        /// Records the right-button press location for a possible pivot context menu.
        /// </summary>
        /// <param name="Position">Pointer position in control coordinates.</param>
        /// <returns>True when the press was captured for context-menu handling.</returns>
        private bool TryBeginPivotContextMenuPress
            (
            Point Position
            )
        {
            if (_ImportInputBlocked)
                return false;

            if (ResolveContentView() == FieldContentView.Haul &&
                IsPointerOverHaulScrubChrome?.Invoke(Position) == true)
            {
                return false;
            }

            _PivotContextMenuPressPoint = Position;
            _PivotContextMenuPressValid = TryPickLatLon(
                (float)Position.X,
                (float)Position.Y,
                out _PivotContextMenuPressLat,
                out _PivotContextMenuPressLon);
            return true;
        }

        /// <summary>
        /// Opens the pivot context menu when the user right-clicks without dragging.
        /// </summary>
        /// <param name="Position">Pointer position on release.</param>
        /// <param name="Args">Pointer release arguments.</param>
        /// <returns>True when the context menu was shown.</returns>
        private bool TryShowPivotContextMenuOnRightRelease
            (
            Point Position,
            PointerReleasedEventArgs Args
            )
        {
            if (Args.InitialPressMouseButton != MouseButton.Right)
                return false;

            if (_PivotContextMenuPressPoint is not Point PressPoint)
                return false;

            double Dx = Position.X - PressPoint.X;
            double Dy = Position.Y - PressPoint.Y;
            if ((Dx * Dx) + (Dy * Dy) > CONTEXT_MENU_DRAG_THRESHOLD_PX * CONTEXT_MENU_DRAG_THRESHOLD_PX)
                return false;

            if (!_PivotContextMenuPressValid)
                return false;

            ContextMenu Menu = EnsurePivotContextMenu();
            Menu.Open(this);
            Args.Handled = true;
            return true;
        }

        /// <summary>
        /// Clears stored pivot context-menu press state.
        /// </summary>
        private void ClearPivotContextMenuPress
            (
            )
        {
            _PivotContextMenuPressPoint = null;
            _PivotContextMenuPressValid = false;
        }

        /// <summary>
        /// Creates the pivot context menu on first use.
        /// </summary>
        /// <returns>The pivot context menu instance.</returns>
        private ContextMenu EnsurePivotContextMenu
            (
            )
        {
            if (_PivotContextMenu != null)
                return _PivotContextMenu;

            MenuItem SetPivotItem = new MenuItem
            {
                Header = "Set Pivot Location",
                Icon = new Image
                {
                    Classes = { CONTEXT_MENU_ICON_STYLE_CLASS },
                    Source = new Bitmap(AssetLoader.Open(new Uri(PIVOT_CONTEXT_MENU_ICON_URI)))
                }
            };
            SetPivotItem.Click += (_, _) =>
            {
                // intent: use press-time coordinates, not the pointer position over the open menu
                if (!_PivotContextMenuPressValid)
                    return;

                Action<double, double>? Handler = HaulPivotLocationSetRequested;
                Handler?.Invoke(_PivotContextMenuPressLat, _PivotContextMenuPressLon);
            };

            _PivotContextMenu = new ContextMenu
            {
                Classes = { CONTEXT_MENU_STYLE_CLASS }
            };
            _PivotContextMenu.Items.Add(SetPivotItem);
            return _PivotContextMenu;
        }
    }
}
