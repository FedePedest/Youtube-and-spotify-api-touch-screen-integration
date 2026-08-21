namespace SpotiTube.Kiosk.Media;

public static class CurrentSessionSelector
{
    public static MediaSessionState? SelectCurrent(IReadOnlyList<MediaSessionState> sessions)
    {
        var playing = sessions.Where(s => s.Status == PlaybackStatus.Playing).ToList();
        if (playing.Count == 0) return null;
        return playing.OrderByDescending(s => s.LastUpdated).First();
    }
}
