namespace SpotiTube.Kiosk.Threading;

/// <summary>
/// Marshals callbacks onto the WPF UI thread.
/// </summary>
/// <remarks>
/// The two OS integrations this app is built on both call back on threads that are never the UI
/// thread - WinRT delivers SMTC events on thread-pool/MTA threads, and
/// <c>SystemEvents.DisplaySettingsChanged</c> is raised on its own dedicated thread - while every
/// downstream consumer (view-models, the window, XAML bindings) touches DependencyObjects. Marshaling
/// at the source keeps that threading concern out of every consumer.
/// </remarks>
internal static class UiThread
{
    public static void Run(Action action)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;

        // No WPF Application (unit tests, or during teardown) or already on the UI thread: run inline.
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return;
        }

        // The app is going away; there is no UI left to update and Invoke would throw.
        if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return;

        try
        {
            dispatcher.Invoke(action);
        }
        catch (TaskCanceledException)
        {
            // Dispatcher shut down between the check above and the call; nothing to update.
        }
    }
}
