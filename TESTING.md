# Testing

## Prerequisites

- **.NET 8 SDK** (Windows x64) — [download](https://dotnet.microsoft.com/download/dotnet/8.0)
- No display server required — UI tests run on the Avalonia headless platform

## Running All Tests

From the repository root:

```bash
dotnet test
```

Or targeting the test project directly:

```bash
dotnet test trojan4win.Tests/trojan4win.Tests.csproj
```

## Verbose Output

```bash
dotnet test --logger "console;verbosity=detailed"
```

## Running Specific Tests

Filter by test class name:

```bash
dotnet test --filter FullyQualifiedName~UITests
dotnet test --filter FullyQualifiedName~FormatTests
dotnet test --filter FullyQualifiedName~SettingsServiceTests
```

Filter by single test:

```bash
dotnet test --filter "FullyQualifiedName~AddServer_ServerAppearsInViewModel"
```

## Test Architecture

**88 tests** across three categories:

| Category | Tests | Classes |
|---|---|---|
| Service & model unit tests | 55 | `SettingsServiceTests`, `ServerConfigTests`, `TrojanServiceConfigTests`, `ProxifyreServiceConfigTests`, `TrafficMonitorTests`, `PingServiceTests` |
| ViewModel unit tests | 27 | `MainViewModelFormatTests` (18 — pure static helpers), `MainViewModelCommandTests` (9 — collection commands) |
| Headless UI integration tests | 6 | `UITests` (add/delete server, switch server, connect/disconnect) |

All ViewModel and UI tests use `[AvaloniaFact]` to run on the Avalonia UI thread via `Avalonia.Headless.XUnit`.

## Known Considerations

### Parallel execution disabled

`xunit.runner.json` sets `parallelizeTestCollections: false`. Multiple test classes share the static `SettingsService._testSettingsDir` hook to redirect file I/O to temp directories. Parallel execution causes a race condition on that static field.

### Adaptive Connect test

`Connect_ThenDisconnect_StateReturnsToDisconnected` works on machines where `trojan.exe`/`proxifyre.exe` are present in the build output (dev machines) **and** where they are absent (CI). If executables exist, it verifies the full connect → disconnect round-trip; if absent, it verifies the error-handling flow.

### PingService ICMP dependency

`MeasurePingAsync_Localhost_ReturnsNonNegative` sends an ICMP echo to `127.0.0.1`. This passes on standard Windows environments but may fail if ICMP is filtered by firewall policy or in restricted CI containers.

### Culture pinning

`MainViewModelFormatTests` pins `Thread.CurrentThread.CurrentCulture` to `CultureInfo.InvariantCulture` in its constructor and restores it in `Dispose()`. This ensures format assertions (`"1.0 KB"` not `"1,0 KB"`) pass regardless of the system locale.

### Headless Avalonia limitations

- No visual rendering — assertions check ViewModel properties, not pixels
- `DispatcherTimer` ticks are not simulated — no live traffic-stat updates in tests
- Controls lack `FluentTheme` templates — visual tree traversal is shallow
- `TestApp.cs` uses `AppBuilder.Configure<TestApp>()` without `UseHeadless()` — the `HeadlessUnitTestSession` sets up the platform automatically (Avalonia.Headless.XUnit 11.3.12 API)
