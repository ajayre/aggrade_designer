using System.Collections.Specialized;

using AgGradeDesigner.ViewModels;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace AgGradeDesigner.Controls
{
    /// <summary>
    /// AI chat panel with output pane, input box, and Send button.
    /// </summary>
    public partial class AiChatPanel : UserControl
    {
        private AiChatViewModel? _SubscribedViewModel;

        /// <summary>
        /// Initializes chat panel XAML and output auto-scroll wiring.
        /// </summary>
        public AiChatPanel
            (
            )
        {
            InitializeComponent();
            DataContextChanged += OnDataContextChanged;
            Loaded += OnLoaded;
        }

        /// <summary>
        /// Wires Enter-to-send handling once the input box is in the visual tree.
        /// </summary>
        /// <param name="Sender">Event source.</param>
        /// <param name="EventArgs">Loaded event arguments.</param>
        private void OnLoaded
            (
            object? Sender,
            RoutedEventArgs EventArgs
            )
        {
            InputBox.AddHandler(InputElement.KeyDownEvent, OnInputKeyDown, RoutingStrategies.Tunnel);
        }

        /// <summary>
        /// Unsubscribes from chat line updates when the panel is removed from the visual tree.
        /// </summary>
        /// <param name="EventArgs">Visual tree detachment arguments.</param>
        protected override void OnDetachedFromVisualTree
            (
            VisualTreeAttachmentEventArgs EventArgs
            )
        {
            UnsubscribeFromLines();
            base.OnDetachedFromVisualTree(EventArgs);
        }

        /// <summary>
        /// Submits the current message when Enter is pressed without Shift.
        /// </summary>
        /// <param name="Sender">Event source.</param>
        /// <param name="EventArgs">Key event arguments.</param>
        private void OnInputKeyDown
            (
            object? Sender,
            KeyEventArgs EventArgs
            )
        {
            if (!IsSubmitKey(EventArgs.Key) ||
                (EventArgs.KeyModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
            {
                return;
            }

            EventArgs.Handled = true;

            if (DataContext is AiChatViewModel ViewModel &&
                ViewModel.SendCommand.CanExecute(null))
            {
                ViewModel.SendCommand.Execute(null);
            }
        }

        /// <summary>
        /// Subscribes to chat line collection changes for the active view model.
        /// </summary>
        /// <param name="Sender">Event source.</param>
        /// <param name="EventArgs">Data context change arguments.</param>
        private void OnDataContextChanged
            (
            object? Sender,
            EventArgs EventArgs
            )
        {
            UnsubscribeFromLines();

            if (DataContext is not AiChatViewModel ViewModel)
                return;

            _SubscribedViewModel = ViewModel;
            ViewModel.Lines.CollectionChanged += OnLinesCollectionChanged;
        }

        /// <summary>
        /// Scrolls the output pane when new chat lines are appended.
        /// </summary>
        /// <param name="Sender">Lines collection.</param>
        /// <param name="EventArgs">Collection change details.</param>
        private void OnLinesCollectionChanged
            (
            object? Sender,
            NotifyCollectionChangedEventArgs EventArgs
            )
        {
            if (EventArgs.Action != NotifyCollectionChangedAction.Add &&
                EventArgs.Action != NotifyCollectionChangedAction.Reset)
            {
                return;
            }

            Dispatcher.UIThread.Post(ScrollOutputToEnd, DispatcherPriority.Loaded);
        }

        /// <summary>
        /// Stops listening to the previously bound view model.
        /// </summary>
        private void UnsubscribeFromLines
            (
            )
        {
            if (_SubscribedViewModel == null)
                return;

            _SubscribedViewModel.Lines.CollectionChanged -= OnLinesCollectionChanged;
            _SubscribedViewModel = null;
        }

        /// <summary>
        /// Scrolls the output pane to show the latest message.
        /// </summary>
        private void ScrollOutputToEnd
            (
            )
        {
            OutputScrollViewer?.ScrollToEnd();
        }

        /// <summary>
        /// Returns true when the key is the main or numpad Enter key.
        /// </summary>
        /// <param name="Key">Key reported by the input event.</param>
        /// <returns>True for Enter-style keys.</returns>
        static private bool IsSubmitKey
            (
            Key Key
            )
        {
            return Key == Key.Enter || Key == Key.Return;
        }
    }
}
