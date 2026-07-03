using Avalonia.Headless;

namespace AgGradeDesigner.Core.Tests.Integration
{
    /// <summary>
    /// Runs headless Avalonia integration tests on the official unit-test session dispatcher.
    /// </summary>
    internal static class HeadlessAvaloniaBootstrap
    {
        static private readonly Lazy<HeadlessUnitTestSession> Session = new Lazy<HeadlessUnitTestSession>(
            () => HeadlessUnitTestSession.StartNew(
                typeof(ImportHeadlessTestApp),
                AvaloniaTestIsolationLevel.PerAssembly));

        /// <summary>
        /// Runs async viewer work on the headless Avalonia UI thread.
        /// </summary>
        internal static Task<T> RunOnUiThreadAsync<T>
            (
            Func<Task<T>> Action,
            CancellationToken CancellationToken = default
            )
        {
            return Session.Value.Dispatch(Action, CancellationToken);
        }
    }
}
