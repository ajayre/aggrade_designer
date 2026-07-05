using AgGradeDesigner.Core.Ai;
using AgGradeDesigner.Core.Ai.Chat;
using AgGradeDesigner.Core.Ai.SurfaceTools;
using AgGradeDesigner.Core.Models;
using AgGradeDesigner.Core.Units;

namespace AgGradeDesigner.Core.Ai.Chat
{
    /// <summary>
    /// Runs the normative tool-calling chat loop for one user message.
    /// </summary>
    public sealed class ChatOrchestrator
    {
        /// <summary>Surface tool dispatcher executing Phase 1 read-only tool calls.</summary>
        private readonly SurfaceToolDispatcher _ToolDispatcher;

        /// <summary>Registered LLM tool schemas exposed to the provider.</summary>
        private readonly IReadOnlyList<LlmToolDefinition> _Tools;

        /// <summary>Active tool delivery phase for the system prompt.</summary>
        private readonly AiDeliveryPhase _DeliveryPhase;

        /// <summary>
        /// Creates an orchestrator with the requested delivery phase tool catalog registered.
        /// </summary>
        /// <param name="ToolDispatcher">Surface tool dispatcher.</param>
        /// <param name="DeliveryPhase">Registered tool delivery phase.</param>
        public ChatOrchestrator
            (
            SurfaceToolDispatcher ToolDispatcher,
            AiDeliveryPhase DeliveryPhase = AiDeliveryPhase.Phase3
            )
        {
            _ToolDispatcher = ToolDispatcher;
            _DeliveryPhase = DeliveryPhase;
            _Tools = SurfaceToolSchemaRegistry.CreateDefinitions(DeliveryPhase);
        }

        /// <summary>
        /// Sends one user message through the LLM and tool loop.
        /// </summary>
        /// <param name="Client">Configured LLM client.</param>
        /// <param name="Project">Active project document.</param>
        /// <param name="SessionMessages">Existing session messages excluding the new user turn.</param>
        /// <param name="UserMessage">Latest user message text.</param>
        /// <param name="DisplayUnits">Active global display unit preference for the system prompt.</param>
        /// <param name="CancellationToken">Cancellation token.</param>
        /// <returns>Final assistant text and updated session transcript.</returns>
        public async Task<ChatTurnResult> SendUserMessageAsync
            (
            ILlmChatClient Client,
            ProjectDocument Project,
            IReadOnlyList<ChatMessage> SessionMessages,
            string UserMessage,
            DisplayUnitSystem DisplayUnits,
            CancellationToken CancellationToken
            )
        {
            if (OffTopicGate.IsObviouslyOffTopic(UserMessage))
            {
                return new ChatTurnResult
                {
                    AssistantText = SystemPromptBuilder.STANDARD_REFUSAL_TEXT,
                    SessionMessages = AppendMessages(SessionMessages, UserMessage, SystemPromptBuilder.STANDARD_REFUSAL_TEXT)
                };
            }

            if (_ToolDispatcher.EditSession.PreviewActive &&
                SurfacePreviewCancelGate.IsExplicitPreviewCancel(UserMessage))
            {
                _ToolDispatcher.DiscardPreview(Project);
                return new ChatTurnResult
                {
                    AssistantText = SystemPromptBuilder.PREVIEW_DISCARDED_TEXT,
                    SessionMessages = AppendMessages(
                        SessionMessages,
                        UserMessage,
                        SystemPromptBuilder.PREVIEW_DISCARDED_TEXT)
                };
            }

            List<ChatMessage> Working = BuildWorkingMessages(Project, SessionMessages, UserMessage, DisplayUnits);
            int Round = 0;
            int ToolStep = 0;
            string FinalText = string.Empty;
            AiToolTrace.BeginTurn(UserMessage, Project.DisplayName);

            try
            {
                while (Round < LlmProviderDefaults.MAX_TOOL_ROUNDS)
                {
                    ChatCompletionResponse Response = await Client.CompleteAsync(Working, _Tools, CancellationToken).ConfigureAwait(false);
                    if (!Response.HasToolCalls)
                    {
                        FinalText = Response.AssistantText;
                        Working.Add(new ChatMessage
                        {
                            Role = ChatRole.Assistant,
                            Content = FinalText
                        });
                        break;
                    }

                    ChatMessage AssistantMessage = new ChatMessage
                    {
                        Role = ChatRole.Assistant,
                        Content = Response.AssistantText,
                        ToolCalls = Response.ToolCalls
                    };
                    Working.Add(AssistantMessage);

                    foreach (LlmToolCall Call in Response.ToolCalls)
                    {
                        ToolStep++;
                        SurfaceToolResult ToolResult = _ToolDispatcher.Execute(Project, Call.Name, Call.ArgumentsJson);
                        AiToolTrace.LogTool(
                            Round,
                            ToolStep,
                            Call.Id,
                            Call.Name,
                            Call.ArgumentsJson,
                            ToolResult);
                        Working.Add(new ChatMessage
                        {
                            Role = ChatRole.Tool,
                            ToolCallId = Call.Id,
                            ToolName = Call.Name,
                            Content = ToolResult.Json
                        });
                    }

                    Round++;
                }

                if (Round >= LlmProviderDefaults.MAX_TOOL_ROUNDS && string.IsNullOrWhiteSpace(FinalText))
                {
                    FinalText = "The assistant stopped after the maximum number of tool rounds. Please try a simpler request.";
                    Working.Add(new ChatMessage
                    {
                        Role = ChatRole.Assistant,
                        Content = FinalText
                    });
                }
            }
            finally
            {
                AiToolTrace.EndTurn(FinalText, ToolStep);
            }

            return new ChatTurnResult
            {
                AssistantText = FinalText,
                SessionMessages = TrimSystemPrompt(Working)
            };
        }

        /// <summary>
        /// Builds the outbound message list with a fresh system prompt prepended.
        /// </summary>
        /// <param name="Project">Active project.</param>
        /// <param name="SessionMessages">Existing session messages.</param>
        /// <param name="UserMessage">Latest user message.</param>
        /// <param name="DisplayUnits">Active global display unit preference.</param>
        /// <returns>Working message list for the provider.</returns>
        private List<ChatMessage> BuildWorkingMessages
            (
            ProjectDocument Project,
            IReadOnlyList<ChatMessage> SessionMessages,
            string UserMessage,
            DisplayUnitSystem DisplayUnits
            )
        {
            List<ChatMessage> Working = new List<ChatMessage>
            {
                new ChatMessage
                {
                    Role = ChatRole.System,
                    Content = SystemPromptBuilder.Build(Project, _DeliveryPhase, DisplayUnits)
                }
            };

            foreach (ChatMessage Message in SessionMessages)
                Working.Add(CloneMessage(Message));

            Working.Add(new ChatMessage
            {
                Role = ChatRole.User,
                Content = UserMessage
            });

            return Working;
        }

        /// <summary>
        /// Removes the ephemeral system prompt from the session transcript returned to the UI.
        /// </summary>
        /// <param name="Working">Full working message list.</param>
        /// <returns>Session-visible messages.</returns>
        static private IReadOnlyList<ChatMessage> TrimSystemPrompt
            (
            List<ChatMessage> Working
            )
        {
            if (Working.Count > 0 && Working[0].Role == ChatRole.System)
                return Working.Skip(1).Select(CloneMessage).ToList();

            return Working.Select(CloneMessage).ToList();
        }

        /// <summary>
        /// Appends one user/assistant exchange to a session transcript.
        /// </summary>
        /// <param name="SessionMessages">Existing messages.</param>
        /// <param name="UserMessage">User text.</param>
        /// <param name="AssistantText">Assistant reply.</param>
        /// <returns>Updated transcript.</returns>
        static private IReadOnlyList<ChatMessage> AppendMessages
            (
            IReadOnlyList<ChatMessage> SessionMessages,
            string UserMessage,
            string AssistantText
            )
        {
            List<ChatMessage> Result = SessionMessages.Select(CloneMessage).ToList();
            Result.Add(new ChatMessage { Role = ChatRole.User, Content = UserMessage });
            Result.Add(new ChatMessage { Role = ChatRole.Assistant, Content = AssistantText });
            return Result;
        }

        /// <summary>
        /// Creates a shallow copy of a chat message and its tool calls.
        /// </summary>
        /// <param name="Message">Source message.</param>
        /// <returns>Copied message.</returns>
        static private ChatMessage CloneMessage
            (
            ChatMessage Message
            )
        {
            ChatMessage Copy = new ChatMessage
            {
                Role = Message.Role,
                Content = Message.Content,
                ToolCallId = Message.ToolCallId,
                ToolName = Message.ToolName
            };

            foreach (LlmToolCall Call in Message.ToolCalls)
            {
                Copy.ToolCalls.Add(new LlmToolCall
                {
                    Id = Call.Id,
                    Name = Call.Name,
                    ArgumentsJson = Call.ArgumentsJson
                });
            }

            return Copy;
        }
    }

    /// <summary>
    /// Result of one chat turn through the orchestrator.
    /// </summary>
    public sealed class ChatTurnResult
    {
        /// <summary>Final assistant natural-language reply.</summary>
        public string AssistantText { get; set; } = string.Empty;

        /// <summary>Updated session transcript excluding the system prompt.</summary>
        public IReadOnlyList<ChatMessage> SessionMessages { get; set; } = Array.Empty<ChatMessage>();
    }
}
