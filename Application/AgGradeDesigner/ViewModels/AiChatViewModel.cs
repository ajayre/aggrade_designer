using System.Collections.ObjectModel;

using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.Chat;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.MapTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Units;
using AgGradeDesigner.Services;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AgGradeDesigner.ViewModels
{
    /// <summary>
    /// View model for the main-window AI chat panel.
    /// </summary>
    public sealed partial class AiChatViewModel : ViewModelBase
    {
        /// <summary>Chat orchestrator running the LLM tool-calling loop.</summary>
        private readonly ChatOrchestrator _Orchestrator;

        /// <summary>
        /// Surface tool dispatcher executing read-only and Phase 2 edit tools.</summary>
        private readonly SurfaceToolDispatcher _ToolDispatcher;

        /// <summary>Exposes the surface edit session for diagnostics.</summary>
        public SurfaceEditSession EditSession => _ToolDispatcher.EditSession;

        /// <summary>Returns the active project document for tool execution.</summary>
        private readonly Func<ProjectDocument> _ProjectProvider;

        /// <summary>Returns the active AI settings for provider selection.</summary>
        private readonly Func<AiSettings> _SettingsProvider;
        /// <summary>In-memory chat transcript excluding the ephemeral system prompt.</summary>
        private readonly List<ChatMessage> _SessionMessages = new List<ChatMessage>();

        /// <summary>
        /// Raised when a preview is updated, committed, or discarded so the viewer can refresh.
        /// </summary>
        public event Action<SurfaceEditLifecycleKind>? SurfaceEditLifecycleChanged;

        /// <summary>
        /// Creates the AI chat panel view model.
        /// </summary>
        /// <param name="ProjectProvider">Returns the active project document.</param>
        /// <param name="SettingsProvider">Returns the active AI settings.</param>
        /// <param name="ToolDataProvider">Returns session map-editing tool data.</param>
        public AiChatViewModel
            (
            Func<ProjectDocument> ProjectProvider,
            Func<AiSettings> SettingsProvider,
            Func<ToolDataSession> ToolDataProvider
            )
        {
            _ProjectProvider = ProjectProvider;
            _SettingsProvider = SettingsProvider;
            _ToolDispatcher = new SurfaceToolDispatcher(
                AiDeliveryPhase.Phase3,
                ToolDataProvider,
                EditSession: null,
                LifecycleCallback: OnSurfaceEditLifecycleChanged);
            _Orchestrator = new ChatOrchestrator(_ToolDispatcher);
            Lines = new ObservableCollection<ChatLineViewModel>();
        }

        /// <summary>
        /// Forwards surface edit lifecycle notifications to the main window.
        /// </summary>
        /// <param name="Kind">Lifecycle event kind.</param>
        private void OnSurfaceEditLifecycleChanged
            (
            SurfaceEditLifecycleKind Kind
            )
        {
            SurfaceEditLifecycleChanged?.Invoke(Kind);
        }

        /// <summary>
        /// Chat transcript lines shown in the output pane.
        /// </summary>
        public ObservableCollection<ChatLineViewModel> Lines { get; }

        /// <summary>
        /// Current compose-box text.
        /// </summary>
        [ObservableProperty]
        private string _InputText = string.Empty;

        /// <summary>
        /// True while an LLM request is in flight.
        /// </summary>
        [ObservableProperty]
        private bool _IsBusy;

        /// <summary>
        /// True when the Send command can run.
        /// </summary>
        public bool CanSend => !IsBusy && !string.IsNullOrWhiteSpace(InputText);

        /// <summary>
        /// Sends the compose-box message to the configured AI provider.
        /// </summary>
        /// <returns>A task that completes when the chat turn finishes.</returns>
        [RelayCommand(CanExecute = nameof(CanSend))]
        private async Task SendAsync
            (
            )
        {
            string UserText = InputText.Trim();
            if (string.IsNullOrWhiteSpace(UserText))
                return;

            InputText = string.Empty;
            OnPropertyChanged(nameof(CanSend));
            SendCommand.NotifyCanExecuteChanged();

            Lines.Add(new ChatLineViewModel("You", UserText));
            IsBusy = true;

            try
            {
                AiSettings Settings = _SettingsProvider();
                if (!LlmChatClientFactory.CanChat(Settings))
                {
                    AppendError("Configure a remote AI provider and API key in Settings before chatting.");
                    return;
                }

                ILlmChatClient Client = LlmChatClientFactory.Create(Settings);
                try
                {
                    ProjectDocument Project = _ProjectProvider();
                    DisplayUnitSystem DisplayUnits = UserSettingsService.Instance.ActiveDisplayUnitsSettings.System;
                    ChatTurnResult Result = await _Orchestrator.SendUserMessageAsync(
                        Client,
                        Project,
                        _SessionMessages,
                        UserText,
                        DisplayUnits,
                        CancellationToken.None).ConfigureAwait(true);

                    _SessionMessages.Clear();
                    _SessionMessages.AddRange(Result.SessionMessages);
                    Lines.Add(new ChatLineViewModel("AI", Result.AssistantText));
                }
                finally
                {
                    if (Client is IDisposable Disposable)
                        Disposable.Dispose();
                }
            }
            catch (Exception Ex)
            {
                AppendError(Ex.Message);
            }
            finally
            {
                IsBusy = false;
                OnPropertyChanged(nameof(CanSend));
                SendCommand.NotifyCanExecuteChanged();
            }
        }

        /// <summary>
        /// Appends an error line to the chat output pane.
        /// </summary>
        /// <param name="Message">Error text.</param>
        private void AppendError
            (
            string Message
            )
        {
            Lines.Add(new ChatLineViewModel("AI", Message, IsError: true));
        }

        /// <summary>
        /// Refreshes Send command availability when input text changes.
        /// </summary>
        /// <param name="Value">New input text.</param>
        partial void OnInputTextChanged
            (
            string Value
            )
        {
            OnPropertyChanged(nameof(CanSend));
            SendCommand.NotifyCanExecuteChanged();
        }
    }
}
