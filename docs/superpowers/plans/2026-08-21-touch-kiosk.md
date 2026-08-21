# Touchscreen Media Kiosk Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a WPF kiosk app that auto-launches full-screen on the HAMTYSAN touch monitor and shows/controls whatever is currently playing via Spotify or YouTube Music on the PC.

**Architecture:** A single WPF (.NET 8) desktop app wraps Windows' System Media Transport Controls (SMTC) for universal now-playing state/control, Windows Core Audio for per-app volume, and `Screen.AllScreens` to locate and fullscreen onto the 1024x600 touch monitor. Pure selection/matching logic is separated from the OS-integration wrappers so it can be unit-tested; the wrappers themselves are verified with a manual checklist since there's no test double for live OS media sessions.

**Tech Stack:** C#, WPF, .NET 8 (TFM `net8.0-windows10.0.19041.0` for WinRT projections), `Windows.Media.Control` (GSMTC, built into the Windows TFM), NAudio (`NAudio.CoreAudioApi` for per-process volume), `System.Windows.Forms.Screen` (via `UseWindowsForms`) for monitor enumeration, `WScript.Shell` COM (via `dynamic`) for Startup-folder shortcut creation, xUnit for tests.

**Spec:** `docs/superpowers/specs/2026-08-21-touch-kiosk-design.md`

## Global Constraints

- Windows-only app; no cross-platform concerns.
- No OAuth, no API keys, no external network calls — everything is local OS integration (SMTC + Core Audio + Screen enumeration).
- TargetFramework: `net8.0-windows10.0.19041.0` for both the app and test projects.
- Controls not supported by the current media session (per SMTC's reported capabilities) must be disabled in the UI, never silently issue a call that will fail.
- No monitor found matching the touch display → app stays hidden/idle rather than guessing a monitor.

---

## File Structure

```
src/SpotiTube.Kiosk/
  SpotiTube.Kiosk.csproj
  App.xaml / App.xaml.cs                     (composition root, startup wiring)
  MainWindow.xaml / MainWindow.xaml.cs        (hosts Idle/NowPlaying views, monitor placement)
  Media/
    MediaSessionState.cs                      (model)
    CurrentSessionSelector.cs                 (pure logic)
    IMediaSessionWatcher.cs                   (interface)
    MediaSessionWatcher.cs                    (GSMTC wrapper)
  Audio/
    AudioSessionInfo.cs                       (model)
    AudioSessionMatcher.cs                    (pure logic)
    IVolumeController.cs                      (interface)
    VolumeController.cs                       (NAudio wrapper)
  Display/
    DisplayInfo.cs                            (model)
    MonitorSelector.cs                        (pure logic)
    MonitorLocator.cs                         (Screen.AllScreens + config wrapper)
    MonitorPresenceEvaluator.cs                (pure logic)
    DisplayWatcherService.cs                  (SystemEvents wrapper)
  ViewModels/
    MainViewModel.cs
  Views/
    IdleView.xaml / IdleView.xaml.cs
    NowPlayingView.xaml / NowPlayingView.xaml.cs
  Resilience/
    RetryPolicy.cs                            (pure logic)
  Logging/
    FileLogger.cs
  Startup/
    AutostartInstaller.cs

tests/SpotiTube.Kiosk.Tests/
  SpotiTube.Kiosk.Tests.csproj
  CurrentSessionSelectorTests.cs
  AudioSessionMatcherTests.cs
  MonitorSelectorTests.cs
  MonitorPresenceEvaluatorTests.cs
  MainViewModelTests.cs
  RetryPolicyTests.cs
  FileLoggerTests.cs
  AutostartInstallerTests.cs
  Fakes/
    FakeMediaSessionWatcher.cs
    FakeVolumeController.cs

Youtube and spotify api touch screen integraiton.slnx   (modified: add both projects)
README.md                                                (new: setup + manual test checklist)
```

---

### Task 1: Solution & Project Scaffolding

**Files:**
- Create: `src/SpotiTube.Kiosk/SpotiTube.Kiosk.csproj`
- Create: `src/SpotiTube.Kiosk/App.xaml`
- Create: `src/SpotiTube.Kiosk/App.xaml.cs`
- Create: `src/SpotiTube.Kiosk/MainWindow.xaml`
- Create: `src/SpotiTube.Kiosk/MainWindow.xaml.cs`
- Create: `tests/SpotiTube.Kiosk.Tests/SpotiTube.Kiosk.Tests.csproj`
- Create: `tests/SpotiTube.Kiosk.Tests/SanityTests.cs`
- Modify: `Youtube and spotify api touch screen integraiton.slnx`

**Interfaces:**
- Produces: a buildable, runnable blank WPF window; a test project that `dotnet test` picks up.

- [ ] **Step 1: Scaffold the WPF project**

```bash
dotnet new wpf -n SpotiTube.Kiosk -o "src/SpotiTube.Kiosk"
```

- [ ] **Step 2: Edit the csproj for WinRT projections, WinForms (Screen), and required packages**

Replace the contents of `src/SpotiTube.Kiosk/SpotiTube.Kiosk.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>SpotiTube.Kiosk</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="NAudio" Version="2.2.1" />
    <Reference Include="Microsoft.CSharp" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Scaffold the test project**

```bash
dotnet new xunit -n SpotiTube.Kiosk.Tests -o "tests/SpotiTube.Kiosk.Tests"
```

Replace the contents of `tests/SpotiTube.Kiosk.Tests/SpotiTube.Kiosk.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\SpotiTube.Kiosk\SpotiTube.Kiosk.csproj" />
  </ItemGroup>

</Project>
```

Delete the default `tests/SpotiTube.Kiosk.Tests/UnitTest1.cs` generated by the template, and create `tests/SpotiTube.Kiosk.Tests/SanityTests.cs`:

```csharp
namespace SpotiTube.Kiosk.Tests;

public class SanityTests
{
    [Fact]
    public void TestProjectRuns()
    {
        Assert.True(true);
    }
}
```

- [ ] **Step 4: Register both projects in the .slnx**

Replace the contents of `Youtube and spotify api touch screen integraiton.slnx` with:

```xml
<Solution>
  <Project Path="src/SpotiTube.Kiosk/SpotiTube.Kiosk.csproj" />
  <Project Path="tests/SpotiTube.Kiosk.Tests/SpotiTube.Kiosk.Tests.csproj" />
</Solution>
```

- [ ] **Step 5: Build and test**

```bash
dotnet build "Youtube and spotify api touch screen integraiton.slnx"
dotnet test "tests/SpotiTube.Kiosk.Tests/SpotiTube.Kiosk.Tests.csproj"
```

Expected: both succeed, `SanityTests.TestProjectRuns` passes. Fix any compile errors from the template scaffold (namespaces, generated `MainWindow.xaml.cs` class name) before moving on.

- [ ] **Step 6: Commit**

```bash
git add src/SpotiTube.Kiosk tests/SpotiTube.Kiosk.Tests "Youtube and spotify api touch screen integraiton.slnx"
git commit -m "Scaffold WPF kiosk app and test project"
```

---

### Task 2: Media session model + pure "current session" selection logic

**Files:**
- Create: `src/SpotiTube.Kiosk/Media/MediaSessionState.cs`
- Create: `src/SpotiTube.Kiosk/Media/CurrentSessionSelector.cs`
- Test: `tests/SpotiTube.Kiosk.Tests/CurrentSessionSelectorTests.cs`

**Interfaces:**
- Produces: `MediaSessionState` record (fields: `SourceAppId`, `Title`, `Artist`, `AlbumArt`, `Status`, `CanPlay`, `CanPause`, `CanSkipNext`, `CanSkipPrevious`, `CanSeek`, `Position`, `Duration`, `LastUpdated`); `PlaybackStatus` enum; `CurrentSessionSelector.SelectCurrent(IReadOnlyList<MediaSessionState>) : MediaSessionState?`. Consumed by Task 3 and Task 6.

- [ ] **Step 1: Write the failing test**

Create `tests/SpotiTube.Kiosk.Tests/CurrentSessionSelectorTests.cs`:

```csharp
using SpotiTube.Kiosk.Media;

namespace SpotiTube.Kiosk.Tests;

public class CurrentSessionSelectorTests
{
    private static MediaSessionState Session(string id, PlaybackStatus status, DateTimeOffset lastUpdated) =>
        new(
            SourceAppId: id,
            Title: "Title-" + id,
            Artist: "Artist-" + id,
            AlbumArt: null,
            Status: status,
            CanPlay: true,
            CanPause: true,
            CanSkipNext: true,
            CanSkipPrevious: true,
            CanSeek: true,
            Position: TimeSpan.Zero,
            Duration: TimeSpan.FromMinutes(3),
            LastUpdated: lastUpdated);

    [Fact]
    public void NoSessions_ReturnsNull()
    {
        var result = CurrentSessionSelector.SelectCurrent(Array.Empty<MediaSessionState>());
        Assert.Null(result);
    }

    [Fact]
    public void NoPlayingSessions_ReturnsNull_EvenIfPaused()
    {
        var sessions = new[] { Session("Spotify.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow) };
        var result = CurrentSessionSelector.SelectCurrent(sessions);
        Assert.Null(result);
    }

    [Fact]
    public void OnePlayingSession_ReturnsIt()
    {
        var sessions = new[]
        {
            Session("Spotify.exe", PlaybackStatus.Paused, DateTimeOffset.UtcNow),
            Session("msedge.exe", PlaybackStatus.Playing, DateTimeOffset.UtcNow),
        };
        var result = CurrentSessionSelector.SelectCurrent(sessions);
        Assert.Equal("msedge.exe", result!.SourceAppId);
    }

    [Fact]
    public void MultiplePlayingSessions_ReturnsMostRecentlyUpdated()
    {
        var older = Session("Spotify.exe", PlaybackStatus.Playing, DateTimeOffset.UtcNow.AddSeconds(-10));
        var newer = Session("msedge.exe", PlaybackStatus.Playing, DateTimeOffset.UtcNow);
        var result = CurrentSessionSelector.SelectCurrent(new[] { older, newer });
        Assert.Equal("msedge.exe", result!.SourceAppId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter CurrentSessionSelectorTests
```

Expected: FAIL (compile error — `SpotiTube.Kiosk.Media` namespace doesn't exist yet).

- [ ] **Step 3: Write the model and the pure selector**

Create `src/SpotiTube.Kiosk/Media/MediaSessionState.cs`:

```csharp
namespace SpotiTube.Kiosk.Media;

public enum PlaybackStatus { Closed, Stopped, Paused, Playing, Changing }

public sealed record MediaSessionState(
    string SourceAppId,
    string Title,
    string Artist,
    byte[]? AlbumArt,
    PlaybackStatus Status,
    bool CanPlay,
    bool CanPause,
    bool CanSkipNext,
    bool CanSkipPrevious,
    bool CanSeek,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset LastUpdated);
```

Create `src/SpotiTube.Kiosk/Media/CurrentSessionSelector.cs`:

```csharp
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
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter CurrentSessionSelectorTests
```

Expected: all 4 tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/SpotiTube.Kiosk/Media tests/SpotiTube.Kiosk.Tests/CurrentSessionSelectorTests.cs
git commit -m "Add media session model and current-session selection logic"
```

---

### Task 3: SMTC-backed MediaSessionWatcher

**Files:**
- Create: `src/SpotiTube.Kiosk/Media/IMediaSessionWatcher.cs`
- Create: `src/SpotiTube.Kiosk/Media/MediaSessionWatcher.cs`

**Interfaces:**
- Consumes: `MediaSessionState`, `PlaybackStatus`, `CurrentSessionSelector.SelectCurrent` from Task 2.
- Produces: `IMediaSessionWatcher` (properties: `MediaSessionState? Current`; events: `PropertyChanged`; methods: `Task<bool> TogglePlayPauseAsync()`, `Task<bool> SkipNextAsync()`, `Task<bool> SkipPreviousAsync()`, `Task<bool> SeekAsync(TimeSpan position)`) and its concrete `MediaSessionWatcher` implementation with `Task StartAsync()`. Consumed by Task 6 (via the interface) and Task 11 (composition root, concrete class).

No automated test is possible here — it requires a live Windows media session, which cannot be faked. Task 2 already covers the pure selection logic; this task is verified manually.

- [ ] **Step 1: Define the interface**

Create `src/SpotiTube.Kiosk/Media/IMediaSessionWatcher.cs`:

```csharp
using System.ComponentModel;

namespace SpotiTube.Kiosk.Media;

public interface IMediaSessionWatcher : INotifyPropertyChanged
{
    MediaSessionState? Current { get; }
    Task<bool> TogglePlayPauseAsync();
    Task<bool> SkipNextAsync();
    Task<bool> SkipPreviousAsync();
    Task<bool> SeekAsync(TimeSpan position);
}
```

- [ ] **Step 2: Implement the GSMTC wrapper**

Create `src/SpotiTube.Kiosk/Media/MediaSessionWatcher.cs`:

```csharp
using System.ComponentModel;
using Windows.Media.Control;

namespace SpotiTube.Kiosk.Media;

public sealed class MediaSessionWatcher : IMediaSessionWatcher, IDisposable
{
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private readonly Dictionary<GlobalSystemMediaTransportControlsSession, MediaSessionState> _states = new();
    private MediaSessionState? _current;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MediaSessionState? Current
    {
        get => _current;
        private set
        {
            if (!Equals(_current, value))
            {
                _current = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));
            }
        }
    }

    public async Task StartAsync()
    {
        _manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
        _manager.SessionsChanged += async (s, e) => await RefreshAllAsync();
        await RefreshAllAsync();
    }

    private async Task RefreshAllAsync()
    {
        if (_manager is null) return;

        var sessions = _manager.GetSessions();
        _states.Clear();
        foreach (var session in sessions)
        {
            _states[session] = await ReadStateAsync(session);
            session.MediaPropertiesChanged += async (s, e) => await OnSessionChangedAsync(session);
            session.PlaybackInfoChanged += async (s, e) => await OnSessionChangedAsync(session);
            session.TimelinePropertiesChanged += async (s, e) => await OnSessionChangedAsync(session);
        }
        Current = CurrentSessionSelector.SelectCurrent(_states.Values.ToList());
    }

    private async Task OnSessionChangedAsync(GlobalSystemMediaTransportControlsSession session)
    {
        if (!_states.ContainsKey(session)) return;
        _states[session] = await ReadStateAsync(session);
        Current = CurrentSessionSelector.SelectCurrent(_states.Values.ToList());
    }

    private static async Task<MediaSessionState> ReadStateAsync(GlobalSystemMediaTransportControlsSession session)
    {
        var props = await session.TryGetMediaPropertiesAsync();
        var playback = session.GetPlaybackInfo();
        var timeline = session.GetTimelineProperties();

        byte[]? art = null;
        if (props?.Thumbnail is not null)
        {
            using var stream = await props.Thumbnail.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.AsStreamForRead().CopyToAsync(ms);
            art = ms.ToArray();
        }

        var status = playback.PlaybackStatus switch
        {
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackStatus.Playing,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackStatus.Paused,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Stopped => PlaybackStatus.Stopped,
            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Changing => PlaybackStatus.Changing,
            _ => PlaybackStatus.Closed,
        };

        var controls = playback.Controls;

        return new MediaSessionState(
            SourceAppId: session.SourceAppUserModelId,
            Title: props?.Title ?? string.Empty,
            Artist: props?.Artist ?? string.Empty,
            AlbumArt: art,
            Status: status,
            CanPlay: controls.IsPlayEnabled,
            CanPause: controls.IsPauseEnabled,
            CanSkipNext: controls.IsNextEnabled,
            CanSkipPrevious: controls.IsPreviousEnabled,
            CanSeek: controls.IsPlaybackPositionEnabled,
            Position: timeline.Position,
            Duration: timeline.EndTime - timeline.StartTime,
            LastUpdated: DateTimeOffset.UtcNow);
    }

    public Task<bool> TogglePlayPauseAsync() => WithCurrentSessionAsync(s => s.TryTogglePlayPauseAsync());
    public Task<bool> SkipNextAsync() => WithCurrentSessionAsync(s => s.TrySkipNextAsync());
    public Task<bool> SkipPreviousAsync() => WithCurrentSessionAsync(s => s.TrySkipPreviousAsync());
    public Task<bool> SeekAsync(TimeSpan position) => WithCurrentSessionAsync(s => s.TryChangePlaybackPositionAsync(position.Ticks));

    private Task<bool> WithCurrentSessionAsync(Func<GlobalSystemMediaTransportControlsSession, IAsyncOperation<bool>> action)
    {
        var session = GetCurrentSession();
        return session is null ? Task.FromResult(false) : action(session).AsTask();
    }

    private GlobalSystemMediaTransportControlsSession? GetCurrentSession()
    {
        if (_current is null) return null;
        return _states.Keys.FirstOrDefault(s => s.SourceAppUserModelId == _current.SourceAppId);
    }

    public void Dispose()
    {
        _manager = null;
    }
}
```

Add `using Windows.Foundation;` at the top if `IAsyncOperation<bool>` doesn't resolve.

- [ ] **Step 3: Build**

```bash
dotnet build "Youtube and spotify api touch screen integraiton.slnx"
```

Expected: builds clean. Fix any WinRT projection issues (namespace/type name mismatches against the installed Windows SDK) until it does — the `net8.0-windows10.0.19041.0` TFM projects `Windows.Media.Control` automatically, no extra NuGet package should be required.

- [ ] **Step 4: Manual verification**

Add a temporary call from `App.xaml.cs`'s `OnStartup` (removed again in Task 11 once the real composition root exists): construct a `MediaSessionWatcher`, call `StartAsync()`, subscribe to `PropertyChanged`, and `Debug.WriteLine(watcher.Current?.Title)`. Play a song in Spotify and in a YouTube Music browser tab; confirm the console shows title/artist changes and status transitions.

- [ ] **Step 5: Commit**

```bash
git add src/SpotiTube.Kiosk/Media
git commit -m "Add SMTC-backed media session watcher"
```

---

### Task 4: Audio session matching + volume control

**Files:**
- Create: `src/SpotiTube.Kiosk/Audio/AudioSessionInfo.cs`
- Create: `src/SpotiTube.Kiosk/Audio/AudioSessionMatcher.cs`
- Create: `src/SpotiTube.Kiosk/Audio/IVolumeController.cs`
- Create: `src/SpotiTube.Kiosk/Audio/VolumeController.cs`
- Test: `tests/SpotiTube.Kiosk.Tests/AudioSessionMatcherTests.cs`

**Interfaces:**
- Produces: `AudioSessionInfo(int ProcessId, string ProcessName)`; `AudioSessionMatcher.FindMatch(IReadOnlyList<AudioSessionInfo>, string sourceAppId) : AudioSessionInfo?`; `IVolumeController` (`float GetVolume(string sourceAppId)`, `void SetVolume(string sourceAppId, float level)`, `bool GetMute(string sourceAppId)`, `void SetMute(string sourceAppId, bool mute)`) and its `VolumeController` implementation. Consumed by Task 6.

- [ ] **Step 1: Write the failing test for the pure matcher**

Create `tests/SpotiTube.Kiosk.Tests/AudioSessionMatcherTests.cs`:

```csharp
using SpotiTube.Kiosk.Audio;

namespace SpotiTube.Kiosk.Tests;

public class AudioSessionMatcherTests
{
    [Fact]
    public void EmptySourceAppId_ReturnsNull()
    {
        var sessions = new[] { new AudioSessionInfo(100, "Spotify.exe") };
        Assert.Null(AudioSessionMatcher.FindMatch(sessions, ""));
    }

    [Fact]
    public void ExactExeMatch_ReturnsIt()
    {
        var sessions = new[]
        {
            new AudioSessionInfo(100, "Spotify.exe"),
            new AudioSessionInfo(200, "msedge.exe"),
        };
        var result = AudioSessionMatcher.FindMatch(sessions, "Spotify.exe");
        Assert.Equal(100, result!.ProcessId);
    }

    [Fact]
    public void AumidWithBangSeparator_MatchesExeNamePrefix()
    {
        var sessions = new[] { new AudioSessionInfo(200, "msedge.exe") };
        var result = AudioSessionMatcher.FindMatch(sessions, "msedge.exe!App");
        Assert.Equal(200, result!.ProcessId);
    }

    [Fact]
    public void NoMatch_ReturnsNull()
    {
        var sessions = new[] { new AudioSessionInfo(100, "Spotify.exe") };
        Assert.Null(AudioSessionMatcher.FindMatch(sessions, "chrome.exe"));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter AudioSessionMatcherTests
```

Expected: FAIL (namespace doesn't exist yet).

- [ ] **Step 3: Implement the model and pure matcher**

Create `src/SpotiTube.Kiosk/Audio/AudioSessionInfo.cs`:

```csharp
namespace SpotiTube.Kiosk.Audio;

public sealed record AudioSessionInfo(int ProcessId, string ProcessName);
```

Create `src/SpotiTube.Kiosk/Audio/AudioSessionMatcher.cs`:

```csharp
namespace SpotiTube.Kiosk.Audio;

public static class AudioSessionMatcher
{
    public static AudioSessionInfo? FindMatch(IReadOnlyList<AudioSessionInfo> sessions, string sourceAppId)
    {
        if (string.IsNullOrEmpty(sourceAppId)) return null;

        var exeName = sourceAppId.Contains('!') ? sourceAppId.Split('!')[0] : sourceAppId;

        return sessions.FirstOrDefault(s =>
            string.Equals(s.ProcessName, exeName, StringComparison.OrdinalIgnoreCase));
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter AudioSessionMatcherTests
```

Expected: all 4 tests PASS.

- [ ] **Step 5: Implement the volume controller (manual verification only)**

Create `src/SpotiTube.Kiosk/Audio/IVolumeController.cs`:

```csharp
namespace SpotiTube.Kiosk.Audio;

public interface IVolumeController
{
    float GetVolume(string sourceAppId);
    void SetVolume(string sourceAppId, float level);
    bool GetMute(string sourceAppId);
    void SetMute(string sourceAppId, bool mute);
}
```

Create `src/SpotiTube.Kiosk/Audio/VolumeController.cs`:

```csharp
using NAudio.CoreAudioApi;

namespace SpotiTube.Kiosk.Audio;

public sealed class VolumeController : IVolumeController
{
    private readonly MMDeviceEnumerator _enumerator = new();

    public float GetVolume(string sourceAppId)
    {
        var session = FindSession(sourceAppId);
        return session?.SimpleAudioVolume.Volume ?? GetMasterDevice().AudioEndpointVolume.MasterVolumeLevelScalar;
    }

    public void SetVolume(string sourceAppId, float level)
    {
        level = Math.Clamp(level, 0f, 1f);
        var session = FindSession(sourceAppId);
        if (session is not null)
        {
            session.SimpleAudioVolume.Volume = level;
        }
        else
        {
            GetMasterDevice().AudioEndpointVolume.MasterVolumeLevelScalar = level;
        }
    }

    public bool GetMute(string sourceAppId)
    {
        var session = FindSession(sourceAppId);
        return session?.SimpleAudioVolume.Mute ?? GetMasterDevice().AudioEndpointVolume.Mute;
    }

    public void SetMute(string sourceAppId, bool mute)
    {
        var session = FindSession(sourceAppId);
        if (session is not null)
        {
            session.SimpleAudioVolume.Mute = mute;
        }
        else
        {
            GetMasterDevice().AudioEndpointVolume.Mute = mute;
        }
    }

    private AudioSessionControl? FindSession(string sourceAppId)
    {
        var sessions = GetMasterDevice().AudioSessionManager.Sessions;
        var candidates = new List<(AudioSessionInfo Info, AudioSessionControl Control)>();

        for (int i = 0; i < sessions.Count; i++)
        {
            var control = sessions[i];
            try
            {
                using var process = System.Diagnostics.Process.GetProcessById((int)control.GetProcessID);
                candidates.Add((new AudioSessionInfo((int)control.GetProcessID, process.ProcessName + ".exe"), control));
            }
            catch (ArgumentException)
            {
                // Process exited between enumeration and lookup; skip it.
            }
        }

        var match = AudioSessionMatcher.FindMatch(candidates.Select(c => c.Info).ToList(), sourceAppId);
        return match is null ? null : candidates.First(c => c.Info.ProcessId == match.ProcessId).Control;
    }

    private MMDevice GetMasterDevice() => _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
}
```

- [ ] **Step 6: Build and manually verify**

```bash
dotnet build "Youtube and spotify api touch screen integraiton.slnx"
```

Manually verify (temporary console call, same pattern as Task 3 Step 4): with Spotify playing, call `GetVolume("Spotify.exe")` and `SetVolume("Spotify.exe", 0.3f)`, confirm Spotify's actual volume changes in the Windows volume mixer.

- [ ] **Step 7: Commit**

```bash
git add src/SpotiTube.Kiosk/Audio tests/SpotiTube.Kiosk.Tests/AudioSessionMatcherTests.cs
git commit -m "Add audio session matching and per-app volume control"
```

---

### Task 5: Monitor location logic

**Files:**
- Create: `src/SpotiTube.Kiosk/Display/DisplayInfo.cs`
- Create: `src/SpotiTube.Kiosk/Display/MonitorSelector.cs`
- Create: `src/SpotiTube.Kiosk/Display/MonitorLocator.cs`
- Test: `tests/SpotiTube.Kiosk.Tests/MonitorSelectorTests.cs`

**Interfaces:**
- Produces: `DisplayInfo(string DeviceName, int WidthPx, int HeightPx, bool IsPrimary)`; `MonitorSelector.SelectTouchMonitor(IReadOnlyList<DisplayInfo>, string? configuredDeviceName) : DisplayInfo?`; `MonitorLocator` with `DisplayInfo? Locate()` and `void SaveConfiguredDeviceName(string)`. Consumed by Task 9 and Task 11.

- [ ] **Step 1: Write the failing test**

Create `tests/SpotiTube.Kiosk.Tests/MonitorSelectorTests.cs`:

```csharp
using SpotiTube.Kiosk.Display;

namespace SpotiTube.Kiosk.Tests;

public class MonitorSelectorTests
{
    [Fact]
    public void NoDisplaysMatchResolution_ReturnsNull()
    {
        var displays = new[] { new DisplayInfo("\\\\.\\DISPLAY1", 1920, 1080, true) };
        Assert.Null(MonitorSelector.SelectTouchMonitor(displays, configuredDeviceName: null));
    }

    [Fact]
    public void OneDisplayMatchesResolution_ReturnsIt()
    {
        var displays = new[]
        {
            new DisplayInfo("\\\\.\\DISPLAY1", 1920, 1080, true),
            new DisplayInfo("\\\\.\\DISPLAY2", 1024, 600, false),
        };
        var result = MonitorSelector.SelectTouchMonitor(displays, configuredDeviceName: null);
        Assert.Equal("\\\\.\\DISPLAY2", result!.DeviceName);
    }

    [Fact]
    public void MultipleMatch_PrefersNonPrimary()
    {
        var displays = new[]
        {
            new DisplayInfo("\\\\.\\DISPLAY1", 1024, 600, true),
            new DisplayInfo("\\\\.\\DISPLAY2", 1024, 600, false),
        };
        var result = MonitorSelector.SelectTouchMonitor(displays, configuredDeviceName: null);
        Assert.Equal("\\\\.\\DISPLAY2", result!.DeviceName);
    }

    [Fact]
    public void ConfiguredDeviceName_TakesPriorityOverResolutionMatch()
    {
        var displays = new[]
        {
            new DisplayInfo("\\\\.\\DISPLAY1", 1920, 1080, true),
            new DisplayInfo("\\\\.\\DISPLAY2", 1024, 600, false),
        };
        var result = MonitorSelector.SelectTouchMonitor(displays, configuredDeviceName: "\\\\.\\DISPLAY1");
        Assert.Equal("\\\\.\\DISPLAY1", result!.DeviceName);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter MonitorSelectorTests
```

Expected: FAIL (namespace doesn't exist yet).

- [ ] **Step 3: Implement the model and pure selector**

Create `src/SpotiTube.Kiosk/Display/DisplayInfo.cs`:

```csharp
namespace SpotiTube.Kiosk.Display;

public sealed record DisplayInfo(string DeviceName, int WidthPx, int HeightPx, bool IsPrimary);
```

Create `src/SpotiTube.Kiosk/Display/MonitorSelector.cs`:

```csharp
namespace SpotiTube.Kiosk.Display;

public static class MonitorSelector
{
    public const int TargetWidth = 1024;
    public const int TargetHeight = 600;

    public static DisplayInfo? SelectTouchMonitor(IReadOnlyList<DisplayInfo> displays, string? configuredDeviceName)
    {
        if (!string.IsNullOrEmpty(configuredDeviceName))
        {
            var configured = displays.FirstOrDefault(d => d.DeviceName == configuredDeviceName);
            if (configured is not null) return configured;
        }

        var matches = displays.Where(d => d.WidthPx == TargetWidth && d.HeightPx == TargetHeight).ToList();
        return matches.Count switch
        {
            0 => null,
            1 => matches[0],
            _ => matches.FirstOrDefault(d => !d.IsPrimary) ?? matches[0],
        };
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter MonitorSelectorTests
```

Expected: all 4 tests PASS.

- [ ] **Step 5: Implement the real locator (manual verification)**

Create `src/SpotiTube.Kiosk/Display/MonitorLocator.cs`:

```csharp
using System.Text.Json;
using System.Windows.Forms;

namespace SpotiTube.Kiosk.Display;

public sealed class MonitorLocator
{
    private readonly string _configPath;

    public MonitorLocator(string configPath)
    {
        _configPath = configPath;
    }

    public DisplayInfo? Locate()
    {
        var displays = Screen.AllScreens
            .Select(s => new DisplayInfo(s.DeviceName, s.Bounds.Width, s.Bounds.Height, s.Primary))
            .ToList();

        return MonitorSelector.SelectTouchMonitor(displays, ReadConfiguredDeviceName());
    }

    public void SaveConfiguredDeviceName(string deviceName)
    {
        File.WriteAllText(_configPath, JsonSerializer.Serialize(new MonitorConfig(deviceName)));
    }

    private string? ReadConfiguredDeviceName()
    {
        if (!File.Exists(_configPath)) return null;
        var config = JsonSerializer.Deserialize<MonitorConfig>(File.ReadAllText(_configPath));
        return config?.DeviceName;
    }

    private sealed record MonitorConfig(string DeviceName);
}
```

- [ ] **Step 6: Build and manually verify**

```bash
dotnet build "Youtube and spotify api touch screen integraiton.slnx"
```

With the HAMTYSAN monitor connected as a second display, temporarily call `new MonitorLocator(path).Locate()` from `App.xaml.cs` `OnStartup` and confirm it returns the correct `DeviceName` (cross-check against Windows Display Settings). Unplug the monitor and confirm it returns `null`.

- [ ] **Step 7: Commit**

```bash
git add src/SpotiTube.Kiosk/Display/DisplayInfo.cs src/SpotiTube.Kiosk/Display/MonitorSelector.cs src/SpotiTube.Kiosk/Display/MonitorLocator.cs tests/SpotiTube.Kiosk.Tests/MonitorSelectorTests.cs
git commit -m "Add touch monitor location logic"
```

---

### Task 6: MainViewModel

**Files:**
- Create: `src/SpotiTube.Kiosk/ViewModels/MainViewModel.cs`
- Create: `tests/SpotiTube.Kiosk.Tests/Fakes/FakeMediaSessionWatcher.cs`
- Create: `tests/SpotiTube.Kiosk.Tests/Fakes/FakeVolumeController.cs`
- Test: `tests/SpotiTube.Kiosk.Tests/MainViewModelTests.cs`

**Interfaces:**
- Consumes: `IMediaSessionWatcher`, `IVolumeController` (Tasks 3 & 4), `MediaSessionState`/`PlaybackStatus` (Task 2).
- Produces: `MainViewModel` with bindable properties `IsIdle`, `Title`, `Artist`, `AlbumArt`, `IsPlaying`, `CanSkipNext`, `CanSkipPrevious`, `CanSeek`, `Position`, `Duration`, `Volume`, and methods `TogglePlayPauseAsync()`, `SkipNextAsync()`, `SkipPreviousAsync()`, `SeekAsync(TimeSpan)`, `SetVolume(float)`. Consumed by Task 7 (XAML bindings) and Task 11 (composition root).

- [ ] **Step 1: Write the fakes**

Create `tests/SpotiTube.Kiosk.Tests/Fakes/FakeMediaSessionWatcher.cs`:

```csharp
using System.ComponentModel;
using SpotiTube.Kiosk.Media;

namespace SpotiTube.Kiosk.Tests.Fakes;

public sealed class FakeMediaSessionWatcher : IMediaSessionWatcher
{
    public MediaSessionState? Current { get; set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void RaiseChanged() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Current)));

    public Task<bool> TogglePlayPauseAsync() => Task.FromResult(true);
    public Task<bool> SkipNextAsync() => Task.FromResult(true);
    public Task<bool> SkipPreviousAsync() => Task.FromResult(true);
    public Task<bool> SeekAsync(TimeSpan position) => Task.FromResult(true);
}
```

Create `tests/SpotiTube.Kiosk.Tests/Fakes/FakeVolumeController.cs`:

```csharp
using SpotiTube.Kiosk.Audio;

namespace SpotiTube.Kiosk.Tests.Fakes;

public sealed class FakeVolumeController : IVolumeController
{
    public float VolumeLevel = 0.5f;
    public bool Muted;

    public float GetVolume(string sourceAppId) => VolumeLevel;
    public void SetVolume(string sourceAppId, float level) => VolumeLevel = level;
    public bool GetMute(string sourceAppId) => Muted;
    public void SetMute(string sourceAppId, bool mute) => Muted = mute;
}
```

- [ ] **Step 2: Write the failing test**

Create `tests/SpotiTube.Kiosk.Tests/MainViewModelTests.cs`:

```csharp
using SpotiTube.Kiosk.Media;
using SpotiTube.Kiosk.Tests.Fakes;
using SpotiTube.Kiosk.ViewModels;

namespace SpotiTube.Kiosk.Tests;

public class MainViewModelTests
{
    private static MediaSessionState PlayingSession(bool canSkipNext = true) => new(
        SourceAppId: "Spotify.exe",
        Title: "Song",
        Artist: "Artist",
        AlbumArt: null,
        Status: PlaybackStatus.Playing,
        CanPlay: true,
        CanPause: true,
        CanSkipNext: canSkipNext,
        CanSkipPrevious: true,
        CanSeek: true,
        Position: TimeSpan.Zero,
        Duration: TimeSpan.FromMinutes(3),
        LastUpdated: DateTimeOffset.UtcNow);

    [Fact]
    public void IsIdle_WhenNoCurrentSession()
    {
        var watcher = new FakeMediaSessionWatcher { Current = null };
        var vm = new MainViewModel(watcher, new FakeVolumeController());
        Assert.True(vm.IsIdle);
    }

    [Fact]
    public void ShowsNowPlaying_WhenSessionActive()
    {
        var watcher = new FakeMediaSessionWatcher();
        var vm = new MainViewModel(watcher, new FakeVolumeController());

        watcher.Current = PlayingSession();
        watcher.RaiseChanged();

        Assert.False(vm.IsIdle);
        Assert.Equal("Song", vm.Title);
        Assert.True(vm.IsPlaying);
    }

    [Fact]
    public void DisablesSkipNext_WhenSessionDoesNotSupportIt()
    {
        var watcher = new FakeMediaSessionWatcher();
        var vm = new MainViewModel(watcher, new FakeVolumeController());

        watcher.Current = PlayingSession(canSkipNext: false);
        watcher.RaiseChanged();

        Assert.False(vm.CanSkipNext);
    }

    [Fact]
    public void SetVolume_UpdatesVolumeControllerAndProperty()
    {
        var watcher = new FakeMediaSessionWatcher();
        var volume = new FakeVolumeController();
        var vm = new MainViewModel(watcher, volume);

        watcher.Current = PlayingSession();
        watcher.RaiseChanged();

        vm.SetVolume(0.8f);

        Assert.Equal(0.8f, volume.VolumeLevel);
        Assert.Equal(0.8f, vm.Volume);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter MainViewModelTests
```

Expected: FAIL (namespace doesn't exist yet).

- [ ] **Step 4: Implement MainViewModel**

Create `src/SpotiTube.Kiosk/ViewModels/MainViewModel.cs`:

```csharp
using System.ComponentModel;
using SpotiTube.Kiosk.Audio;
using SpotiTube.Kiosk.Media;

namespace SpotiTube.Kiosk.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IMediaSessionWatcher _watcher;
    private readonly IVolumeController _volume;

    public MainViewModel(IMediaSessionWatcher watcher, IVolumeController volume)
    {
        _watcher = watcher;
        _volume = volume;
        _watcher.PropertyChanged += (s, e) => Refresh();
        Refresh();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsIdle { get; private set; } = true;
    public string Title { get; private set; } = string.Empty;
    public string Artist { get; private set; } = string.Empty;
    public byte[]? AlbumArt { get; private set; }
    public bool IsPlaying { get; private set; }
    public bool CanSkipNext { get; private set; }
    public bool CanSkipPrevious { get; private set; }
    public bool CanSeek { get; private set; }
    public TimeSpan Position { get; private set; }
    public TimeSpan Duration { get; private set; }
    public float Volume { get; private set; }

    private void Refresh()
    {
        var current = _watcher.Current;
        IsIdle = current is null;

        if (current is not null)
        {
            Title = current.Title;
            Artist = current.Artist;
            AlbumArt = current.AlbumArt;
            IsPlaying = current.Status == PlaybackStatus.Playing;
            CanSkipNext = current.CanSkipNext;
            CanSkipPrevious = current.CanSkipPrevious;
            CanSeek = current.CanSeek;
            Position = current.Position;
            Duration = current.Duration;
            Volume = _volume.GetVolume(current.SourceAppId);
        }

        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    public Task<bool> TogglePlayPauseAsync() => _watcher.TogglePlayPauseAsync();
    public Task<bool> SkipNextAsync() => _watcher.SkipNextAsync();
    public Task<bool> SkipPreviousAsync() => _watcher.SkipPreviousAsync();
    public Task<bool> SeekAsync(TimeSpan position) => _watcher.SeekAsync(position);

    public void SetVolume(float level)
    {
        var current = _watcher.Current;
        if (current is null) return;

        _volume.SetVolume(current.SourceAppId, level);
        Volume = level;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Volume)));
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter MainViewModelTests
```

Expected: all 4 tests PASS.

- [ ] **Step 6: Commit**

```bash
git add src/SpotiTube.Kiosk/ViewModels tests/SpotiTube.Kiosk.Tests/Fakes tests/SpotiTube.Kiosk.Tests/MainViewModelTests.cs
git commit -m "Add MainViewModel binding media/volume state to the UI"
```

---

### Task 7: Kiosk UI (Idle + Now Playing views)

**Files:**
- Create: `src/SpotiTube.Kiosk/Views/IdleView.xaml`
- Create: `src/SpotiTube.Kiosk/Views/IdleView.xaml.cs`
- Create: `src/SpotiTube.Kiosk/Views/NowPlayingView.xaml`
- Create: `src/SpotiTube.Kiosk/Views/NowPlayingView.xaml.cs`
- Modify: `src/SpotiTube.Kiosk/MainWindow.xaml`
- Modify: `src/SpotiTube.Kiosk/MainWindow.xaml.cs`

**Interfaces:**
- Consumes: `MainViewModel` (Task 6) as `DataContext`.
- Produces: `MainWindow` that switches between `IdleView` and `NowPlayingView` based on `IsIdle`. Consumed by Task 11 (composition root shows this window).

No automated test — this is a visual/touch UI, verified manually per the spec's testing plan.

- [ ] **Step 1: Create IdleView**

Create `src/SpotiTube.Kiosk/Views/IdleView.xaml`:

```xml
<UserControl x:Class="SpotiTube.Kiosk.Views.IdleView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Background="#FF101010">
        <TextBlock x:Name="ClockText"
                   Foreground="White"
                   FontSize="72"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center" />
    </Grid>
</UserControl>
```

Create `src/SpotiTube.Kiosk/Views/IdleView.xaml.cs`:

```csharp
using System.Windows.Controls;
using System.Windows.Threading;

namespace SpotiTube.Kiosk.Views;

public partial class IdleView : UserControl
{
    private readonly DispatcherTimer _timer;

    public IdleView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => ClockText.Text = DateTime.Now.ToString("t");
        _timer.Start();
        ClockText.Text = DateTime.Now.ToString("t");
    }
}
```

- [ ] **Step 2: Create NowPlayingView**

Create `src/SpotiTube.Kiosk/Views/NowPlayingView.xaml`:

```xml
<UserControl x:Class="SpotiTube.Kiosk.Views.NowPlayingView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
    <Grid Background="#FF101010">
        <Grid.RowDefinitions>
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <StackPanel Grid.Row="0" VerticalAlignment="Center" HorizontalAlignment="Center">
            <TextBlock Text="{Binding Title}" Foreground="White" FontSize="32" HorizontalAlignment="Center" />
            <TextBlock Text="{Binding Artist}" Foreground="#FFAAAAAA" FontSize="20" HorizontalAlignment="Center" />
        </StackPanel>

        <Slider Grid.Row="1"
                 Minimum="0"
                 Maximum="{Binding Duration.TotalSeconds}"
                 Value="{Binding Position.TotalSeconds, Mode=OneWay}"
                 Margin="24,0,24,8"
                 Height="40" />

        <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Center" Margin="0,0,0,24">
            <Button x:Name="PreviousButton" Content="⏮" FontSize="28" Width="72" Height="72"
                    IsEnabled="{Binding CanSkipPrevious}" Click="OnPreviousClick" />
            <Button x:Name="PlayPauseButton" Content="⏯" FontSize="28" Width="72" Height="72"
                    Margin="16,0" Click="OnPlayPauseClick" />
            <Button x:Name="NextButton" Content="⏭" FontSize="28" Width="72" Height="72"
                    IsEnabled="{Binding CanSkipNext}" Click="OnNextClick" />
            <Slider x:Name="VolumeSlider" Minimum="0" Maximum="1" Value="{Binding Volume, Mode=OneWay}"
                    Width="160" Margin="24,0,0,0" VerticalAlignment="Center"
                    ValueChanged="OnVolumeChanged" />
        </StackPanel>
    </Grid>
</UserControl>
```

Create `src/SpotiTube.Kiosk/Views/NowPlayingView.xaml.cs`:

```csharp
using System.Windows.Controls;
using SpotiTube.Kiosk.ViewModels;

namespace SpotiTube.Kiosk.Views;

public partial class NowPlayingView : UserControl
{
    public NowPlayingView()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private async void OnPlayPauseClick(object sender, System.Windows.RoutedEventArgs e) =>
        await (Vm?.TogglePlayPauseAsync() ?? Task.CompletedTask);

    private async void OnNextClick(object sender, System.Windows.RoutedEventArgs e) =>
        await (Vm?.SkipNextAsync() ?? Task.CompletedTask);

    private async void OnPreviousClick(object sender, System.Windows.RoutedEventArgs e) =>
        await (Vm?.SkipPreviousAsync() ?? Task.CompletedTask);

    private void OnVolumeChanged(object sender, System.Windows.RoutedPropertyChangedEventArgs<double> e) =>
        Vm?.SetVolume((float)e.NewValue);
}
```

- [ ] **Step 3: Wire both views into MainWindow**

Replace `src/SpotiTube.Kiosk/MainWindow.xaml` contents:

```xml
<Window x:Class="SpotiTube.Kiosk.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:views="clr-namespace:SpotiTube.Kiosk.Views"
        Title="SpotiTube Kiosk"
        WindowStyle="None"
        ResizeMode="NoResize"
        Topmost="True"
        Background="Black">
    <Grid>
        <views:IdleView x:Name="Idle" Visibility="Visible" />
        <views:NowPlayingView x:Name="NowPlaying" Visibility="Collapsed" />
    </Grid>
</Window>
```

Replace `src/SpotiTube.Kiosk/MainWindow.xaml.cs` contents:

```csharp
using System.ComponentModel;
using System.Windows;
using SpotiTube.Kiosk.ViewModels;

namespace SpotiTube.Kiosk;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    public void Bind(MainViewModel viewModel)
    {
        NowPlaying.DataContext = viewModel;
        viewModel.PropertyChanged += (s, e) => UpdateVisibility(viewModel);
        UpdateVisibility(viewModel);
    }

    private void UpdateVisibility(MainViewModel viewModel)
    {
        Idle.Visibility = viewModel.IsIdle ? Visibility.Visible : Visibility.Collapsed;
        NowPlaying.Visibility = viewModel.IsIdle ? Visibility.Collapsed : Visibility.Visible;
    }

    public void PlaceOnDisplay(System.Windows.Forms.Screen screen)
    {
        Left = screen.Bounds.Left;
        Top = screen.Bounds.Top;
        Width = screen.Bounds.Width;
        Height = screen.Bounds.Height;
        WindowState = WindowState.Normal;
    }
}
```

- [ ] **Step 4: Build**

```bash
dotnet build "Youtube and spotify api touch screen integraiton.slnx"
```

Expected: builds clean.

- [ ] **Step 5: Manual verification**

Temporarily wire a `MainViewModel` backed by fakes into `MainWindow` from `App.xaml.cs` and run the app. Confirm the Idle view shows a clock, and flipping the fake's `Current` to a playing session swaps in the Now Playing view with working buttons (against the fakes — real OS wiring happens in Task 11).

- [ ] **Step 6: Commit**

```bash
git add src/SpotiTube.Kiosk/Views src/SpotiTube.Kiosk/MainWindow.xaml src/SpotiTube.Kiosk/MainWindow.xaml.cs
git commit -m "Add Idle and Now Playing touch UI views"
```

---

### Task 8: Retry policy and rolling file logger

**Files:**
- Create: `src/SpotiTube.Kiosk/Resilience/RetryPolicy.cs`
- Create: `src/SpotiTube.Kiosk/Logging/FileLogger.cs`
- Test: `tests/SpotiTube.Kiosk.Tests/RetryPolicyTests.cs`
- Test: `tests/SpotiTube.Kiosk.Tests/FileLoggerTests.cs`

**Interfaces:**
- Produces: `RetryPolicy.RunWithRetryAsync<T>(Func<Task<T>> action, int maxAttempts, Action<Exception>? onError = null) : Task<T?>`; `FileLogger` with `FileLogger(string path, long maxBytes = 1_000_000)` and `void Log(string message)`. Consumed by Task 11 to wrap the composition root's calls into `MediaSessionWatcher`/`VolumeController`.

- [ ] **Step 1: Write the failing tests for RetryPolicy**

Create `tests/SpotiTube.Kiosk.Tests/RetryPolicyTests.cs`:

```csharp
using SpotiTube.Kiosk.Resilience;

namespace SpotiTube.Kiosk.Tests;

public class RetryPolicyTests
{
    [Fact]
    public async Task RetriesUntilSuccess()
    {
        int calls = 0;
        var result = await RetryPolicy.RunWithRetryAsync(() =>
        {
            calls++;
            if (calls < 3) throw new InvalidOperationException("boom");
            return Task.FromResult(42);
        }, maxAttempts: 5);

        Assert.Equal(42, result);
        Assert.Equal(3, calls);
    }

    [Fact]
    public async Task ReturnsDefault_WhenAllAttemptsFail()
    {
        var result = await RetryPolicy.RunWithRetryAsync<int?>(
            () => throw new InvalidOperationException("boom"),
            maxAttempts: 2);

        Assert.Null(result);
    }

    [Fact]
    public async Task InvokesOnErrorForEachFailure()
    {
        var errors = new List<string>();
        await RetryPolicy.RunWithRetryAsync<int?>(
            () => throw new InvalidOperationException("boom"),
            maxAttempts: 3,
            onError: ex => errors.Add(ex.Message));

        Assert.Equal(3, errors.Count);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter RetryPolicyTests
```

Expected: FAIL (namespace doesn't exist yet).

- [ ] **Step 3: Implement RetryPolicy**

Create `src/SpotiTube.Kiosk/Resilience/RetryPolicy.cs`:

```csharp
namespace SpotiTube.Kiosk.Resilience;

public static class RetryPolicy
{
    public static async Task<T?> RunWithRetryAsync<T>(Func<Task<T>> action, int maxAttempts, Action<Exception>? onError = null)
    {
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex);
                if (attempt < maxAttempts)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt));
                }
            }
        }
        return default;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter RetryPolicyTests
```

Expected: all 3 tests PASS.

- [ ] **Step 5: Write the failing tests for FileLogger**

Create `tests/SpotiTube.Kiosk.Tests/FileLoggerTests.cs`:

```csharp
using SpotiTube.Kiosk.Logging;

namespace SpotiTube.Kiosk.Tests;

public class FileLoggerTests
{
    [Fact]
    public void Log_AppendsMessage()
    {
        var path = Path.Combine(Path.GetTempPath(), $"log-{Guid.NewGuid()}.txt");
        try
        {
            var logger = new FileLogger(path);
            logger.Log("hello");
            Assert.Contains("hello", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Log_TrimsWhenOverLimit()
    {
        var path = Path.Combine(Path.GetTempPath(), $"log-{Guid.NewGuid()}.txt");
        try
        {
            var logger = new FileLogger(path, maxBytes: 100);
            for (int i = 0; i < 20; i++)
            {
                logger.Log($"line {i} padding padding padding");
            }
            Assert.True(new FileInfo(path).Length < 2000);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
```

- [ ] **Step 6: Run test to verify it fails**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter FileLoggerTests
```

Expected: FAIL (namespace doesn't exist yet).

- [ ] **Step 7: Implement FileLogger**

Create `src/SpotiTube.Kiosk/Logging/FileLogger.cs`:

```csharp
namespace SpotiTube.Kiosk.Logging;

public sealed class FileLogger
{
    private readonly string _path;
    private readonly long _maxBytes;
    private readonly object _lock = new();

    public FileLogger(string path, long maxBytes = 1_000_000)
    {
        _path = path;
        _maxBytes = maxBytes;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public void Log(string message)
    {
        lock (_lock)
        {
            File.AppendAllText(_path, $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}");
            TrimIfTooLarge();
        }
    }

    private void TrimIfTooLarge()
    {
        var info = new FileInfo(_path);
        if (info.Exists && info.Length > _maxBytes)
        {
            var lines = File.ReadAllLines(_path);
            File.WriteAllLines(_path, lines.Skip(lines.Length / 2));
        }
    }
}
```

- [ ] **Step 8: Run test to verify it passes**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter FileLoggerTests
```

Expected: both tests PASS.

- [ ] **Step 9: Commit**

```bash
git add src/SpotiTube.Kiosk/Resilience src/SpotiTube.Kiosk/Logging tests/SpotiTube.Kiosk.Tests/RetryPolicyTests.cs tests/SpotiTube.Kiosk.Tests/FileLoggerTests.cs
git commit -m "Add retry policy and rolling file logger"
```

---

### Task 9: Monitor presence evaluation and reconnect handling

**Files:**
- Create: `src/SpotiTube.Kiosk/Display/MonitorPresenceEvaluator.cs`
- Create: `src/SpotiTube.Kiosk/Display/DisplayWatcherService.cs`
- Test: `tests/SpotiTube.Kiosk.Tests/MonitorPresenceEvaluatorTests.cs`

**Interfaces:**
- Consumes: `DisplayInfo`, `MonitorLocator` (Task 5).
- Produces: `MonitorPresenceAction` enum (`Show`, `Hide`, `NoChange`); `MonitorPresenceEvaluator.Evaluate(DisplayInfo? previous, DisplayInfo? current) : MonitorPresenceAction`; `DisplayWatcherService(MonitorLocator locator, Action onShow, Action onHide)` with `void CheckNow()`. Consumed by Task 11.

- [ ] **Step 1: Write the failing test**

Create `tests/SpotiTube.Kiosk.Tests/MonitorPresenceEvaluatorTests.cs`:

```csharp
using SpotiTube.Kiosk.Display;

namespace SpotiTube.Kiosk.Tests;

public class MonitorPresenceEvaluatorTests
{
    private static readonly DisplayInfo Display = new("\\\\.\\DISPLAY2", 1024, 600, false);
    private static readonly DisplayInfo OtherDisplay = new("\\\\.\\DISPLAY3", 1024, 600, false);

    [Fact]
    public void MonitorAppears_ReturnsShow()
    {
        Assert.Equal(MonitorPresenceAction.Show, MonitorPresenceEvaluator.Evaluate(null, Display));
    }

    [Fact]
    public void MonitorDisappears_ReturnsHide()
    {
        Assert.Equal(MonitorPresenceAction.Hide, MonitorPresenceEvaluator.Evaluate(Display, null));
    }

    [Fact]
    public void MonitorUnchanged_ReturnsNoChange()
    {
        Assert.Equal(MonitorPresenceAction.NoChange, MonitorPresenceEvaluator.Evaluate(Display, Display));
    }

    [Fact]
    public void MonitorSwapsToDifferentDevice_ReturnsShow()
    {
        Assert.Equal(MonitorPresenceAction.Show, MonitorPresenceEvaluator.Evaluate(Display, OtherDisplay));
    }

    [Fact]
    public void NeitherPresent_ReturnsNoChange()
    {
        Assert.Equal(MonitorPresenceAction.NoChange, MonitorPresenceEvaluator.Evaluate(null, null));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter MonitorPresenceEvaluatorTests
```

Expected: FAIL (namespace doesn't exist yet).

- [ ] **Step 3: Implement the evaluator**

Create `src/SpotiTube.Kiosk/Display/MonitorPresenceEvaluator.cs`:

```csharp
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
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter MonitorPresenceEvaluatorTests
```

Expected: all 5 tests PASS.

- [ ] **Step 5: Implement the real watcher service (manual verification)**

Create `src/SpotiTube.Kiosk/Display/DisplayWatcherService.cs`:

```csharp
using Microsoft.Win32;

namespace SpotiTube.Kiosk.Display;

public sealed class DisplayWatcherService : IDisposable
{
    private readonly MonitorLocator _locator;
    private readonly Action _onShow;
    private readonly Action _onHide;
    private DisplayInfo? _lastKnown;

    public DisplayWatcherService(MonitorLocator locator, Action onShow, Action onHide)
    {
        _locator = locator;
        _onShow = onShow;
        _onHide = onHide;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public void CheckNow() => OnDisplaySettingsChanged(this, EventArgs.Empty);

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    {
        var current = _locator.Locate();
        var action = MonitorPresenceEvaluator.Evaluate(_lastKnown, current);
        _lastKnown = current;

        if (action == MonitorPresenceAction.Show) _onShow();
        else if (action == MonitorPresenceAction.Hide) _onHide();
    }

    public void Dispose()
    {
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
```

- [ ] **Step 6: Build**

```bash
dotnet build "Youtube and spotify api touch screen integraiton.slnx"
```

Expected: builds clean.

- [ ] **Step 7: Commit**

```bash
git add src/SpotiTube.Kiosk/Display/MonitorPresenceEvaluator.cs src/SpotiTube.Kiosk/Display/DisplayWatcherService.cs tests/SpotiTube.Kiosk.Tests/MonitorPresenceEvaluatorTests.cs
git commit -m "Add monitor presence evaluation and reconnect handling"
```

---

### Task 10: Autostart installer

**Files:**
- Create: `src/SpotiTube.Kiosk/Startup/AutostartInstaller.cs`
- Test: `tests/SpotiTube.Kiosk.Tests/AutostartInstallerTests.cs`

**Interfaces:**
- Produces: `AutostartInstaller.GetShortcutPath(string startupFolder, string appName) : string`, `AutostartInstaller.Install(string startupFolder, string appName, string targetExePath) : void`, `AutostartInstaller.IsInstalled(string startupFolder, string appName) : bool`. Consumed by Task 11.

- [ ] **Step 1: Write the failing test**

Create `tests/SpotiTube.Kiosk.Tests/AutostartInstallerTests.cs`:

```csharp
using SpotiTube.Kiosk.Startup;

namespace SpotiTube.Kiosk.Tests;

public class AutostartInstallerTests
{
    [Fact]
    public void Install_CreatesShortcutFile()
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), $"startup-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempFolder);
        try
        {
            Assert.False(AutostartInstaller.IsInstalled(tempFolder, "SpotiTube.Kiosk"));

            AutostartInstaller.Install(tempFolder, "SpotiTube.Kiosk", @"C:\fake\SpotiTube.Kiosk.exe");

            Assert.True(AutostartInstaller.IsInstalled(tempFolder, "SpotiTube.Kiosk"));
        }
        finally
        {
            Directory.Delete(tempFolder, recursive: true);
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter AutostartInstallerTests
```

Expected: FAIL (namespace doesn't exist yet).

- [ ] **Step 3: Implement AutostartInstaller**

Create `src/SpotiTube.Kiosk/Startup/AutostartInstaller.cs`:

```csharp
using System.Runtime.InteropServices;

namespace SpotiTube.Kiosk.Startup;

public static class AutostartInstaller
{
    public static string GetShortcutPath(string startupFolder, string appName) =>
        Path.Combine(startupFolder, $"{appName}.lnk");

    public static void Install(string startupFolder, string appName, string targetExePath)
    {
        var shortcutPath = GetShortcutPath(startupFolder, appName);
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("WScript.Shell COM component is not available.");
        dynamic shell = Activator.CreateInstance(shellType)!;
        try
        {
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = targetExePath;
            shortcut.WorkingDirectory = Path.GetDirectoryName(targetExePath);
            shortcut.WindowStyle = 7; // minimized
            shortcut.Save();
        }
        finally
        {
            Marshal.ReleaseComObject(shell);
        }
    }

    public static bool IsInstalled(string startupFolder, string appName) =>
        File.Exists(GetShortcutPath(startupFolder, appName));
}
```

- [ ] **Step 4: Run test to verify it passes**

```bash
dotnet test tests/SpotiTube.Kiosk.Tests --filter AutostartInstallerTests
```

Expected: PASS. This genuinely creates a `.lnk` via COM, so it only passes on Windows — consistent with this being a Windows-only app.

- [ ] **Step 5: Commit**

```bash
git add src/SpotiTube.Kiosk/Startup tests/SpotiTube.Kiosk.Tests/AutostartInstallerTests.cs
git commit -m "Add Startup-folder autostart installer"
```

---

### Task 11: Composition root, autostart wiring, and manual test checklist

**Files:**
- Modify: `src/SpotiTube.Kiosk/App.xaml.cs`
- Create: `README.md`

**Interfaces:**
- Consumes: everything from Tasks 2–10 (`MediaSessionWatcher`, `VolumeController`, `MonitorLocator`, `DisplayWatcherService`, `MainViewModel`, `MainWindow`, `RetryPolicy`, `FileLogger`, `AutostartInstaller`).
- Produces: the fully wired, runnable kiosk app. Terminal task — nothing downstream.

- [ ] **Step 1: Wire the composition root**

Replace `src/SpotiTube.Kiosk/App.xaml.cs` contents:

```csharp
using System.Windows;
using SpotiTube.Kiosk.Audio;
using SpotiTube.Kiosk.Display;
using SpotiTube.Kiosk.Logging;
using SpotiTube.Kiosk.Media;
using SpotiTube.Kiosk.Resilience;
using SpotiTube.Kiosk.Startup;
using SpotiTube.Kiosk.ViewModels;

namespace SpotiTube.Kiosk;

public partial class App : Application
{
    private FileLogger? _logger;
    private MediaSessionWatcher? _watcher;
    private DisplayWatcherService? _displayWatcher;
    private MainWindow? _window;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var appDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpotiTube.Kiosk");
        _logger = new FileLogger(Path.Combine(appDataDir, "kiosk.log"));

        InstallAutostartIfNeeded();

        _watcher = new MediaSessionWatcher();
        await RetryPolicy.RunWithRetryAsync(
            async () => { await _watcher.StartAsync(); return true; },
            maxAttempts: 3,
            onError: ex => _logger.Log($"MediaSessionWatcher start failed: {ex}"));

        var volumeController = new VolumeController();
        var viewModel = new MainViewModel(_watcher, volumeController);

        _window = new MainWindow();
        _window.Bind(viewModel);

        var monitorConfigPath = Path.Combine(appDataDir, "monitor.json");
        var locator = new MonitorLocator(monitorConfigPath);

        _displayWatcher = new DisplayWatcherService(
            locator,
            onShow: () => ShowOnTouchMonitor(locator),
            onHide: () => _window.Hide());

        _displayWatcher.CheckNow();
    }

    private void ShowOnTouchMonitor(MonitorLocator locator)
    {
        var display = locator.Locate();
        if (display is null || _window is null) return;

        var screen = System.Windows.Forms.Screen.AllScreens
            .FirstOrDefault(s => s.DeviceName == display.DeviceName);
        if (screen is null) return;

        _window.PlaceOnDisplay(screen);
        _window.Show();
    }

    private void InstallAutostartIfNeeded()
    {
        var startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        const string appName = "SpotiTube.Kiosk";
        if (AutostartInstaller.IsInstalled(startupFolder, appName)) return;

        var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (string.IsNullOrEmpty(exePath)) return;

        AutostartInstaller.Install(startupFolder, appName, exePath);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _displayWatcher?.Dispose();
        _watcher?.Dispose();
        base.OnExit(e);
    }
}
```

Remove `StartupUri="MainWindow.xaml"` from `src/SpotiTube.Kiosk/App.xaml` if the WPF template added it (the window is now created and shown explicitly from `OnStartup`), leaving:

```xml
<Application x:Class="SpotiTube.Kiosk.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             StartupUri="">
</Application>
```

- [ ] **Step 2: Build and run**

```bash
dotnet build "Youtube and spotify api touch screen integraiton.slnx"
dotnet run --project "src/SpotiTube.Kiosk/SpotiTube.Kiosk.csproj"
```

Expected: builds and runs without exceptions. With the touch monitor connected, the window appears full-screen on it within a couple seconds (idle clock view if nothing is playing).

- [ ] **Step 3: Write the manual test checklist**

Create `README.md`:

```markdown
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
- [ ] Pause everything: kiosk falls back to the Idle clock view.
- [ ] Unplug the touch monitor while the kiosk is showing Now Playing:
      window disappears without crashing.
- [ ] Replug the touch monitor: window reappears full-screen on it,
      correctly positioned, without needing an app restart.
- [ ] Reboot the PC: app auto-launches, finds the touch monitor, and
      shows the correct view without any manual steps.
```

- [ ] **Step 4: Run the full test suite**

```bash
dotnet test "tests/SpotiTube.Kiosk.Tests/SpotiTube.Kiosk.Tests.csproj"
```

Expected: all tests across all tasks PASS.

- [ ] **Step 5: Work through the manual checklist in README.md**

Go through every item above on the real setup (Spotify + YouTube Music + the physical HAMTYSAN monitor) and check them off.

- [ ] **Step 6: Commit**

```bash
git add src/SpotiTube.Kiosk/App.xaml.cs src/SpotiTube.Kiosk/App.xaml README.md
git commit -m "Wire composition root, autostart, and manual test checklist"
```
