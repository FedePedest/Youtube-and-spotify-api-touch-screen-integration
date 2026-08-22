# SpotiTube Kiosk

A WPF app that auto-launches full-screen on a HAMTYSAN 1024x600 touch
monitor and shows/controls whatever is currently playing via Spotify or
YouTube Music on the PC. Uses Windows' System Media Transport Controls —
no API keys, no OAuth, no Spotify/YouTube accounts to configure.


## Installing

Grab `SpotiTube.Kiosk.Setup.exe` from the [Releases](https://github.com/FedePedest/Youtube-and-spotify-api-touch-screen-integration/releases)
page (or build it yourself, see below) and run it. The installer:

- Copies the app to `Program Files\SpotiTube Kiosk`.
- Adds a Start Menu shortcut (and, optionally, a desktop shortcut).
- Offers to launch the app at the end of setup — say yes, since that
  first launch is what registers auto-start (see below).

No accounts, API keys, or extra dependencies are needed — it's a
self-contained build, so the target PC doesn't need .NET installed
separately.

To uninstall, use "Add or remove programs" like any other Windows app;
this also removes the auto-start shortcut.

### Building the installer yourself

Requires the .NET 8 SDK and [Inno Setup 6](https://jrsoftware.org/isdl.php).

```powershell
installer\build.ps1
```

This publishes a self-contained `win-x64` build and compiles it into
`installer\output\SpotiTube.Kiosk.Setup.exe`. See `installer\SpotiTube.Kiosk.iss`
for the Inno Setup script, or `installer\build.ps1 -Version 1.2.0` to stamp a
specific version.

## Running from source

```
dotnet run --project src/SpotiTube.Kiosk/SpotiTube.Kiosk.csproj
```

The app installs itself into the Windows Startup folder on first run, so
it will auto-launch on subsequent logins without needing to be started
manually again.

Autostart is installed for real the first time the app actually launches
on a machine — running it will add a shortcut to your Windows Startup
folder, so expect that to happen the first time you run it yourself.
This is also what the installer's "launch now" option triggers, pointed
at the installed copy in `Program Files` rather than your build output.

For the best experience, run the app on a touch monitor with a 1024x600 resolution and use wallpapper engines playlist feature.
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
- [ ] If your PC has more than one display, confirm the kiosk window lands
      on the smallest one (the touch monitor) and not the primary display,
      both on a fresh launch and after a monitor unplug/replug.
- [ ] Move the mouse cursor somewhere on another monitor, then tap a button
      on the touchscreen: the cursor snaps back to that other monitor
      shortly after release rather than staying on the touch monitor. Also
      drag the seek/volume sliders with touch and confirm the drag itself
      still tracks your finger normally before it snaps back on release.
- [ ] Play a YouTube video: the kiosk background should blur that video's own
      thumbnail instead of the seasonal glow. Switch to a Spotify track and
      confirm it goes back to the seasonal glow.
- [ ] Play a colorful Spotify/music track: the seek and volume bar fill
      should tint to a color pulled from that track's album art instead of
      plain green. Switch to a YouTube video: the bars should go back to
      plain green rather than tinting off the video's thumbnail.
- [ ] Pause right after switching tracks (before the vinyl art has clearly
      loaded), then check the art is correct rather than stuck blank/stale -
      pausing should force a fresh read of the artwork.
- [ ] Skip to a new track and, if the title/art briefly look wrong right at
      the switch, confirm they self-correct within a few seconds without you
      doing anything - a one-time forced re-check runs ~3s after a track
      becomes current specifically to catch that.
- [ ] With a paused/idle YouTube tab sitting in the background (not actually
      being watched) and Spotify playing or paused, confirm the kiosk keeps
      showing Spotify's title/art and that Skip Next/Previous keeps
      controlling Spotify - the idle video tab should not intermittently
      hijack "current" just because it's sitting there.
- [ ] Type into another window (e.g. Notepad) on another monitor, then tap
      Pause on the touchscreen without clicking back into that window first:
      keep typing - the keystrokes should still land in that window rather
      than needing you to click back into it.
- [ ] Watch the seek bar during normal playback (don't touch anything): the
      thumb should creep forward smoothly second-by-second on its own,
      matching real elapsed time, rather than sitting frozen until the next
      track change/seek/pause.
