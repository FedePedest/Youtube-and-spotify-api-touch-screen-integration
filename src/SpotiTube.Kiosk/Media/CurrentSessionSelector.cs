namespace SpotiTube.Kiosk.Media;

public static class CurrentSessionSelector
{
    /// <summary>
    /// Picks the session the kiosk should show and control: the most recently updated
    /// <see cref="PlaybackStatus.Playing"/> session if any is playing, otherwise the most recently
    /// updated session that isn't <see cref="PlaybackStatus.Closed"/>.
    /// </summary>
    /// <remarks>
    /// The non-closed fallback matters: without it, pausing was a one-way door - the paused session
    /// stopped being "current", the UI fell back to the idle view, and the Play button that would
    /// have resumed it disappeared with it. Only "no sessions at all" or "every session closed"
    /// resolves to no current session (idle).
    /// </remarks>
    public static MediaSessionState? SelectCurrent(IReadOnlyList<MediaSessionState> sessions)
    {
        var playing = sessions.Where(s => s.Status == PlaybackStatus.Playing).ToList();
        if (playing.Count > 0)
        {
            return playing.OrderByDescending(s => s.LastUpdated).First();
        }

        var resumable = sessions.Where(s => s.Status != PlaybackStatus.Closed).ToList();
        if (resumable.Count > 0)
        {
            return resumable.OrderByDescending(s => s.LastUpdated).First();
        }

        return null;
    }
}
