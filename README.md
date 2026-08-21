# SpotiTube Kiosk

A WPF app that auto-launches full-screen on a HAMTYSAN 1024x600 touch
monitor and shows/controls whatever is currently playing via Spotify or
YouTube Music on the PC. Uses Windows' System Media Transport Controls —
no API keys, no OAuth, no Spotify/YouTube accounts to configure.

See `docs/superpowers/specs/2026-08-21-touch-kiosk-design.md` for the
design and `docs/superpowers/plans/2026-08-21-touch-kiosk.md` for how it
was built.

## Running

```
dotnet run --project src/SpotiTube.Kiosk/SpotiTube.Kiosk.csproj
```

The app installs itself into the Windows Startup folder on first run, so
it will auto-launch on subsequent logins without needing to be started
manually again.

Autostart is installed for real the first time the app actually launches
on a machine — running it will add a shortcut to your Windows Startup
folder, so expect that to happen the first time you run it yourself.

## Manual test checklist

Run through this after any change touching `Media/`, `Audio/`, `Display/`,
or `Startup/` — these have no automated test coverage since they wrap live
OS state.

- [ ] Spotify desktop app: play, pause, skip next, skip previous, seek,
      volume — all reflected on the touchscreen and vice versa.
- [ ] YouTube Music in a Microsoft Edge tab: same checks as above.
- [ ] YouTube Music in a Google Chrome tab: same checks as above.
- [ ] Play Spotify and YouTube Music at the same time, confirm the kiosk
      follows whichever one is actually playing.
- [ ] Pause everything: kiosk stays on the paused session (Now Playing view,
      Play button re-enabled) rather than falling back to idle — the Idle
      clock view only appears once every session is fully closed.
- [ ] Unplug the touch monitor while the kiosk is showing Now Playing:
      window disappears without crashing.
- [ ] Replug the touch monitor: window reappears full-screen on it,
      correctly positioned, without needing an app restart.
- [ ] Reboot the PC: app auto-launches, finds the touch monitor, and
      shows the correct view without any manual steps.
- [ ] If your PC has more than one display at noticeably different DPI
      scaling (e.g. a high-DPI primary next to the 100%-scale touch
      monitor), confirm the kiosk window lands on the touch monitor and
      not the primary display — see "Known limitations" below.

## Known limitations

- **Seek bar doesn't animate during playback.** It updates correctly on
  track changes, pause/resume, and manual seeks, but doesn't creep forward
  second-by-second while a track plays — most media sessions (Spotify
  included) don't push per-second position updates over SMTC, and the
  kiosk doesn't yet poll for one. Dragging the seek bar to jump to a
  position still works normally.
- **Window placement on unusual multi-monitor DPI setups.** The kiosk
  reliably fills the touch monitor correctly-*sized* regardless of DPI
  scaling, but on a specific arrangement — a very wide, heavily-scaled
  primary display with the touch monitor placed to its right — the window
  could in theory seed itself onto the wrong monitor before maximizing.
  Not expected to affect a typical single-primary + touch-monitor setup.
