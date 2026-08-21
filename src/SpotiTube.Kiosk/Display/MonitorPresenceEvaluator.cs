namespace SpotiTube.Kiosk.Display;

public enum MonitorPresenceAction { Show, Hide, NoChange }

public static class MonitorPresenceEvaluator
{
    public static MonitorPresenceAction Evaluate(DisplayInfo? previous, DisplayInfo? current)
    {
        if (previous is null && current is not null) return MonitorPresenceAction.Show;
        if (previous is not null && current is null) return MonitorPresenceAction.Hide;
        if (previous is not null && current is not null && previous.DeviceName != current.DeviceName)
            return MonitorPresenceAction.Show;
        return MonitorPresenceAction.NoChange;
    }
}
