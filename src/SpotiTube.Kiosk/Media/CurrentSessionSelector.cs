namespace SpotiTube.Kiosk.Media;

public static class CurrentSessionSelector
{
    /// <summary>
    /// Picks the session the kiosk should show and control: the most recently updated
    /// <see cref="PlaybackStatus.Playing"/> session if any is playing, otherwise the most recently
    /// updated non-video session that isn't <see cref="PlaybackStatus.Closed"/>, falling back to a
    /// video session only if that's all there is.
    /// </summary>
    /// <remarks>
    /// The non-closed fallback matters: without it, pausing was a one-way door - the paused session
    /// stopped being "current", the UI fell back to the idle view, and the Play button that would
    /// have resumed it disappeared with it. Only "no sessions at all" or "every session closed"
    /// resolves to no current session (idle).
    ///
    /// The fallback prefers non-video (music) sessions over video ones: a paused/idle video tab
    /// (e.g. a YouTube tab that isn't actually being watched) sits around reporting a session
    /// indefinitely, and without this it can casually outrank the music session the kiosk is
    /// actually meant to be showing just because it happened to be touched most recently. A
    /// video that's genuinely <see cref="PlaybackStatus.Playing"/> is unaffected by this - the
    /// "playing" bucket above is intentionally type-agnostic, since a playing video is exactly as
    /// current as playing music.
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
            return resumable.OrderBy(s => s.IsVideo).ThenByDescending(s => s.LastUpdated).First();
        }

        return null;
    }
}
