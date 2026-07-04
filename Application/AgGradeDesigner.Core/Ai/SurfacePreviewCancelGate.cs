namespace AgGradeDesigner.Core.Ai
{
    /// <summary>
    /// Detects explicit user chat instructions to cancel a pending surface edit preview per §10.2.4.
    /// </summary>
    static public class SurfacePreviewCancelGate
    {
        /// <summary>Multi-word phrases that clearly cancel a pending preview.</summary>
        private static readonly string[] EXPLICIT_CANCEL_PHRASES =
        [
            "don't commit",
            "do not commit",
            "dont commit",
            "don't apply",
            "do not apply",
            "dont apply",
            "cancel the change",
            "cancel the edit",
            "cancel the preview",
            "discard the preview",
            "discard the change",
            "discard the edit",
            "never mind",
            "nevermind",
            "revert the change",
            "restore the original",
            "keep the original",
            "leave it as is",
            "leave it as it was",
            "don't save",
            "do not save"
        ];

        /// <summary>Exact short replies that decline commit when a preview is pending.</summary>
        private static readonly string[] EXACT_DECLINE_REPLIES =
        [
            "no",
            "nope",
            "nah",
            "no thanks",
            "no thank you",
            "decline",
            "cancel"
        ];

        /// <summary>
        /// Returns true when the user message explicitly cancels a pending surface preview.
        /// </summary>
        /// <param name="UserMessage">Latest user chat message.</param>
        /// <returns>True when the preview should be discarded locally.</returns>
        static public bool IsExplicitPreviewCancel
            (
            string UserMessage
            )
        {
            if (string.IsNullOrWhiteSpace(UserMessage))
                return false;

            string Trimmed = UserMessage.Trim().TrimEnd('.', '!', '?');
            string Lower = Trimmed.ToLowerInvariant();

            foreach (string Phrase in EXPLICIT_CANCEL_PHRASES)
            {
                if (Lower.Contains(Phrase, StringComparison.Ordinal))
                    return true;
            }

            foreach (string Exact in EXACT_DECLINE_REPLIES)
            {
                if (string.Equals(Lower, Exact, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }
    }
}
