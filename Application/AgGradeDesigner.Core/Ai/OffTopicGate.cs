namespace AgGradeDesigner.Core.Ai
{
    /// <summary>
    /// Lightweight local gate for obviously off-topic user chat input per §6.5.3.
    /// </summary>
    static public class OffTopicGate
    {
        /// <summary>Lowercase keyword phrases that trigger local off-topic refusal.</summary>
        private static readonly string[] OFF_TOPIC_KEYWORDS =
        [
            "write me a poem",
            "tell me a joke",
            "homework",
            "python code",
            "javascript code",
            "creative writing",
            "role-play",
            "roleplay",
            "stock market",
            "medical advice",
            "legal advice"
        ];

        /// <summary>
        /// Returns true when the message should be refused locally without an LLM call.
        /// </summary>
        /// <param name="UserMessage">Latest user chat message.</param>
        /// <returns>True when the message is obviously off-topic.</returns>
        static public bool IsObviouslyOffTopic
            (
            string UserMessage
            )
        {
            if (string.IsNullOrWhiteSpace(UserMessage))
                return false;

            string Lower = UserMessage.ToLowerInvariant();
            foreach (string Keyword in OFF_TOPIC_KEYWORDS)
            {
                if (Lower.Contains(Keyword, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
