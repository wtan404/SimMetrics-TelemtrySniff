# Changelog

All notable updates and project changes to `SimMetrics-TelemtrySniff` are documented in this file.

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
