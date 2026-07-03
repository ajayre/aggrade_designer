namespace AgGradeDesigner
{
    /// <summary>
    /// Command-line launch options parsed before the Avalonia host starts.
    /// </summary>
    internal static class AppLaunchOptions
    {
        /// <summary>Whether debug logging was requested on the command line.</summary>
        public static bool DebugLog { get; private set; }

        /// <summary>
        /// Parses supported command-line switches from the process arguments.
        /// </summary>
        /// <param name="Args">Raw process arguments.</param>
        public static void Parse
            (
            string[] Args
            )
        {
            foreach (string Arg in Args)
            {
                if (string.Equals(Arg, "--debug-log", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(Arg, "--debuglog", StringComparison.OrdinalIgnoreCase))
                {
                    DebugLog = true;
                }
            }
        }
    }
}
