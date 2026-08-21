# Touchscreen Media Kiosk — Design Spec

Date: 2026-08-21

## Problem

The user has a HAMTYSAN 10.1" 1024x600 HDMI+USB touchscreen monitor connected
directly to their Windows 11 PC as a second display. They want it to
automatically show a touch-controllable "now playing" UI whenever Spotify or
YouTube Music (played in a browser tab) is active on the PC, without needing
to configure anything at runtime.

## Constraints discovered during brainstorming

- Neither Spotify nor YouTube Music exposes a public API suitable for this:
  Spotify's Web API requires a Developer app, OAuth login, and a Premium
  account, and only covers Spotify. YouTube Music has no official playback
  control API at all.
- Windows exposes a universal "Now Playing" integration point — the System
  Media Transport Controls (SMTC), surfaced via
  `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager`
  (WinRT). Both the Spotify desktop app and a YouTube Music tab in Edge/Chrome
  already report to it (it's what powers the taskbar/lock-screen media
  flyout). No API keys or OAuth needed.
- SMTC does **not** carry volume. Volume must be controlled separately via
  Windows Core Audio per-process session volume.
- The monitor is directly attached to the PC (not a separate Raspberry Pi),
  so no client/server or networking layer is needed — this is a single
  desktop app.

## Scope

MVP only: universal now-playing display + transport control (play/pause/
next/prev/seek/volume) for whatever is currently playing (Spotify app or a
YouTube Music browser tab), auto-launched full-screen on the touch monitor.

Explicitly out of scope for this spec: browsing/searching a library or
starting new tracks from the touchscreen (would require a separate,
Spotify-only Web API integration with OAuth — a possible future subsystem,
not designed here).

## Architecture

Single WPF (.NET 8, C#) desktop application, tentatively named
`SpotiTube.Kiosk`, added to the existing empty `Youtube and spotify api
touch screen integraiton.slnx` solution.

### Components

**MediaSessionWatcher**
Wraps `GlobalSystemMediaTransportControlsSessionManager`. Subscribes to
`SessionsChanged` across all apps reporting media. Selects the "current"
session: prefer whichever session reports `Playing`; if multiple are
playing, prefer the most recently changed; otherwise no current session.
Exposes, as observable state (`INotifyPropertyChanged`):
- `MediaProperties` (title, artist, album art)
- `PlaybackInfo` (status, which controls — play/pause/next/prev/seek — the
  session actually supports)
- `TimelineProperties` (position/duration, for the seek bar)

Exposes async command methods: `TogglePlayPauseAsync`, `SkipNextAsync`,
`SkipPreviousAsync`, `SeekAsync(TimeSpan)`.

**VolumeController**
Wraps Windows Core Audio (`NAudio` or raw `IAudioSessionManager2`/
`ISimpleAudioVolume`) to get/set the volume and mute state of the audio
session belonging to the process that owns the current SMTC session
(`Spotify.exe`, or the browser process hosting the YouTube Music tab).
Falls back to master system volume if no matching per-process session is
found.

**MonitorLocator**
Enumerates displays (`System.Windows.Forms.Screen.AllScreens`). Identifies
the HAMTYSAN monitor by matching its known native resolution (1024x600). If
more than one secondary display matches, falls back to a value in a local
config file (`monitor.json`) storing the target device name. Positions a
borderless, fullscreen WPF window on the identified display. Listens for
`SystemEvents.DisplaySettingsChanged` to re-detect the monitor if it's
unplugged/replugged while the app is running.

**Kiosk Shell (WPF UI)**
The touch UI itself, with two views:
- **Idle view** — shown when there is no current media session. Clock /
  ambient background.
- **Now Playing view** — shown when a session is active. Blurred album-art
  background, large touch-friendly transport buttons (play/pause, next,
  prev), a seek bar, and a volume slider. Buttons for controls the current
  session doesn't support (per `PlaybackInfo`) are disabled rather than
  issuing calls that would silently fail.

The shell auto-switches between the two views as `MediaSessionWatcher`'s
state changes — no user action needed.

**Autostart**
A shortcut in the Windows Startup folder launches the app hidden at login.
It waits for `MonitorLocator` to confirm the touch display is present, then
positions and shows itself full-screen on it.

## Data flow

1. OS-level media session change (app starts/stops/pauses playback, track
   changes) → `MediaSessionWatcher` raises a property-changed event.
2. WPF view-model (bound to the watcher) updates.
3. UI re-renders bound elements (album art, progress bar, play/pause icon,
   view switch between Idle/Now Playing).
4. User taps a control → view-model calls the corresponding watcher/volume
   method → SMTC/Core Audio forwards the command to Spotify or the browser
   → step 1 fires again once the app acknowledges the change.

## Error handling

- No active session → Idle view.
- A control unsupported by the current session (e.g. some YouTube Music
  tabs don't support seek) → corresponding button disabled, not just
  ignored on tap.
- Touch monitor unplugged mid-session → window minimizes/parks; reappears
  full-screen on the same display automatically once
  `DisplaySettingsChanged` reports it back.
- WinRT / Core Audio calls wrapped in try/catch with a bounded retry;
  failures are logged to a local rolling log file rather than crashing the
  app.

## Testing plan

SMTC is a live OS integration with no available test double, so:
- **Unit tests** for the pure selection logic: "which session is current"
  given a set of fake sessions with various playback states, and "which
  audio session matches the current media session" given fake process
  lists.
- **Manual integration checklist**: Spotify desktop play/pause/skip/seek/
  volume; YouTube Music in Edge and in Chrome; switching which one is
  "current" when both are open; unplugging/replugging the touch monitor
  while the app is running; a full reboot to confirm autostart, monitor
  auto-detection, and correct full-screen placement without any manual
  steps.

## Open questions for a possible future subsystem

- Spotify Web API integration (OAuth + Premium) for browsing/searching/
  queueing tracks from the touchscreen — deliberately deferred out of this
  spec's scope.
