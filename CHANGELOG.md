# Changelog

All notable updates and project changes to `SimMetryx-TelemetrySniff` are documented in this file.

## [2026-11-10] - SimMetryx Observer SimHub Discovery Resolution & Global SimMetryx Rebranding

### Fixed
- **SimHub Plugin Discovery & Reflection Loading (`SimMetryxObserver.dll` / `simmetryxObserver.dll`)**:
  - Resolved root cause of SimHub failing to detect the observer plugin: the assembly was previously compiling against internal stubs due to a generic type mismatch on SimHub's `GameReaderCommon.dll`.
  - Re-bound project references directly against real SimHub assemblies (`D:\Program Files\SimHub\SimHub.Plugins.dll`, `GameReaderCommon.dll`, `SimHub.Logging.dll`, `log4net.dll`, `MahApps.Metro.dll`).
  - Corrected `SniffEngine.cs` telemetry consumption signatures: updated `PopulateNormalizedData` to consume `GameReaderCommon.StatusDataBase`, mapped `data.NewData.SessionTypeName`, resolved nullable vector accelerations (`?? 0.0`), and handled non-nullable `TimeSpan` lap time validations.
  - Implemented `IWPFSettingsV2` on `TelemetrySniffPlugin` with public `PluginManager` setter and registered left-sidebar menu title `LeftMenuTitle => "SimMetryx Observer"`.
  - Verified via live CLR reflection that `SimMetryxObserver.dll` exports `IPlugin = True`, `IWPFSettingsV2 = True`, and successfully instantiates within SimHub's runtime engine.

### Changed
- **Comprehensive Platform Rebranding (`SimMetryx` Casingwise)**:
  - Enforced strict `SimMetryx` casing and zero tolerance for legacy `SimMetrics` naming across the entire codebase, project files, solutions, namespaces, and documentation.
  - Renamed projects and solution:
    - Solution: `SimMetryx-TelemetrySniff.sln`
    - Main Plugin: `src/SimMetryx.TelemetrySniff.csproj` (Assembly: `SimMetryxObserver.dll`)
    - Core Engine: `src/SimMetryx.TelemetrySniff.Core/SimMetryx.TelemetrySniff.Core.csproj`
    - Standalone CLI: `src/SimMetryx.TelemetrySniff.Cli/SimMetryx.TelemetrySniff.Cli.csproj`
    - Unit Tests: `tests/SimMetryx.TelemetrySniff.Tests/SimMetryx.TelemetrySniff.Tests.csproj`
  - Renamed all C# namespaces from `SimMetrics.TelemetrySniff.*` to `SimMetryx.TelemetrySniff.*`.
  - Relocated default telemetry artifact directory from `Documents/SimMetrics/TelemetrySniff` to `Documents/SimMetryx/TelemetrySniff`.
  - Rebranded ANSI terminal HUD, banners, and help screens in `Program.cs` to `SimMetryx Telemetry Observer`.
  - Retargeted plugin project strictly to `<TargetFramework>net48</TargetFramework>` to eliminate CLR version mismatch against SimHub's .NET Framework host.

### Verified & Deployed
- **Local Unit Test Suite**: All 6 tests in `SimMetryx.TelemetrySniff.Tests` passed (100% pass rate).
- **SimHub Production Directory**: Fresh build of `SimMetryxObserver.dll` (78,848 bytes) deployed directly to `D:\Program Files\SimHub\simmetryxObserver.dll` and `SimMetryxObserver.dll`.
- **Integrity**: Verified `spedbot` and `SimMetryxRacebot` remain untouched and functional.

## [2026-11-09] - Single-Assembly Plugin Consolidation & Zero-Dependency SimHub Build

### Changed
- **Zero-Dependency Single-DLL Build (`simmetryxObserver.dll`)**:
  - Embedded `SimMetrics.TelemetrySniff.Core` source files directly into `SimMetrics.TelemetrySniff.csproj`, eliminating the need for a separate companion `SimMetrics.TelemetrySniff.Core.dll`.
  - Configured `Newtonsoft.Json` package reference with `<ExcludeAssets>runtime</ExcludeAssets>` so SimHub's native `Newtonsoft.Json.dll` is utilized at runtime without bundling redundant copies.
  - When compiled in Release mode, outputs a 100% self-contained single `simmetryxObserver.dll` (84 KB) ready for drop-in installation into any SimHub setup.
- **Solution Compatibility Preserved**:
  - Maintained `SimMetrics.TelemetrySniff.Core.csproj` for the standalone CLI runner and unit test suite, passing all 6 unit tests with zero regressions.

## [2026-11-08] - SimMetryx Observer Universal SimHub Plugin & AI-Readable Manifest

### Added
- **Assembly Renaming to `simmetryxObserver.dll`**:
  - Configured `SimMetrics.TelemetrySniff.csproj` with assembly target `<AssemblyName>simmetryxObserver</AssemblyName>`, building `simmetryxObserver.dll` across `.NET 8 (Windows)` and `.NET Framework 4.8`.
- **Dynamic Game & Session Indicators**:
  - Implemented live game auto-detection tracking runtime transitions across all 50+ SimHub supported simulators (AMS2, Assetto Corsa, ACC, iRacing, BeamNG, etc.).
  - Registered SimHub properties: `ActiveGame`, `IsGameRunning`, `SessionType`, `CurrentVehicle`, `CurrentTrack`, `StatusIndicator`, and `LastSummaryFile`.
  - Added visual status badge to the WPF UI (`SniffPluginUI.xaml`): `🟢 SIM RUNNING`, `🔴 OBSERVING`, `⚪ STANDBY`.
- **Togglable Observer Controls**:
  - Added primary On/Off observer toggle (`BtnToggleCapture`) in `SniffPluginUI.xaml` with dynamic color transitions and text states.
  - Registered bindable SimHub action `SimMetrics.TelemetrySniff.ToggleCapture` for hardware steering wheel buttons and streamdecks.
- **AI-Readable Session Manifests (`SessionSummary`)**:
  - Added `SessionSummary.cs` in `SimMetrics.TelemetrySniff.Core.Models` to compute performance envelopes (top speed, peak RPM, completed laps, tyre temperature extremes, fuel deltas).
  - Automatically exports `sniff_<Game>_<Timestamp>.summary.md` and `sniff_<Game>_<Timestamp>.summary.json` alongside `.jsonl` and `.catalog.json` upon observation stop.
  - Provides Antigravity and AI assistants with instant readability for telemetry audits and engineering reviews.
- **Unit Testing**:
  - Added `SessionSummaryTests.cs` in `SimMetrics.TelemetrySniff.Tests` verifying derived metric calculations, Markdown manifest formatting, and JSON serialization.

### Changed
- Refactored `TelemetrySniffPlugin.cs` and `SniffEngine.cs` to integrate `SessionSummary` tracking without blocking telemetry processing loops.
- Updated `README.md` architecture diagrams, plugin naming, and output format documentation.

## [2026-11-07] - Assetto Corsa UDP Broadcast Engine & Standalone CLI

### Added
- **Core Library (`SimMetrics.TelemetrySniff.Core`)**:
  - Implemented Assetto Corsa UDP Remote Telemetry Protocol specification.
  - Added binary parser for `AcHandshakeResponse` (408 bytes), extracting Car Name, Driver Name, Track Name, and Track Configuration.
  - Added binary deserializer for real-time car physics telemetry datagrams (`AcCarInfo`) capturing speed, engine RPM, gear, throttle, brake, clutch, steering angle, 3-axis G-forces, lap timing, and 4-wheel slip angle, slip ratio, and suspension travel.
  - Added binary parser for lap completion datagrams (`AcLapInfo`).
  - Added `AssettoCorsaUdpClient`: Asynchronous UDP client managing active handshakes, subscription updates (`SUBSCRIBE_UPDATE`, `SUBSCRIBE_SPOT`), keepalive refreshes, and graceful session dismissal.
  - Added `PassiveUdpSniffer`: High-throughput socket sniffer for capturing broadcast telemetry across arbitrary UDP ports without handshakes.
  - Added `TelemetrySessionWriter`: High-throughput non-blocking disk writer with queueing to stream newline-delimited JSON (`.jsonl`) and generate schema dictionaries (`.catalog.json`).
- **Standalone Terminal HUD (`SimMetrics.TelemetrySniff.Cli`)**:
  - Created modern .NET 8 CLI application executable without SimHub dependencies.
  - Interactive terminal HUD featuring live speedometer, tachometer, bar meters for pedals, steering angle, G-force meters, 4-wheel telemetry, and streaming packet frequency (Hz).
  - Multi-mode support: `--mode active` (direct handshake), `--mode passive` (broadcast sniffer), and `--mode demo` (simulated live telemetry stream).
  - Real-time disk capture toggling with automatic session catalog generation.
- **Unit & Integration Test Suite (`SimMetrics.TelemetrySniff.Tests`)**:
  - Added xUnit test suite validating handshake parsing, physics packet decoding, and non-blocking session recording.
- **SimHub Stubs & Multi-Targeting**:
  - Added reference interface stubs (`SimHubStubs.cs`) enabling the SimHub plugin project to compile cleanly on machines without local SimHub installations.

### Changed
- Reorganized solution `SimMetrics-TelemtrySniff.sln` to include `SimMetrics.TelemetrySniff.Core`, `SimMetrics.TelemetrySniff.Cli`, and `SimMetrics.TelemetrySniff.Tests`.
- Updated `README.md` with complete Assetto Corsa UDP broadcasting configuration guide, port setup (`assetto_corsa.ini`), CLI hotkeys, and architecture diagrams.
