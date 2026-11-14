# SimMetryx-TelemetrySniff

Universal high-throughput telemetry sniffer, raw buffer dumper, and channel discovery plugin for SimHub (`SimMetryxObserver.dll` / `simmetryxObserver.dll`).

Designed specifically as an automated reverse-engineering recorder and schema extractor across all 50+ simulators supported by SimHub—including **Automobilista 2 (AMS2)**, **Assetto Corsa**, **Assetto Corsa Competizione (ACC)**, **iRacing**, **BeamNG.drive**, **F1 series**, and **Le Mans Ultimate**.

---

## 🏎️ Telemetry Architecture

```
┌─────────────────────────────────────────────────────────────┐
│              SimMetryx.TelemetrySniff.Core                  │
│   • Universal TelemetryPacket & NormalizedData Channels     │
│   • CaptureManifest Developer & AI Metadata Schema          │
└──────────────────────────────┬──────────────────────────────┘
                               │ (Zero-dependency embedded)
                               ▼
┌─────────────────────────────────────────────────────────────┐
│          SimMetryxObserver.dll / simmetryxObserver.dll       │
│                  (Universal SimHub Plugin)                  │
├─────────────────────────────────────────────────────────────┤
│ • In-Rig SimHub UI Control Panel & Master Power Switch      │
│ • Simulator Process Bound Auto-Armed State Machine          │
│ • High-Throughput Non-Blocking JSONL Telemetry Streamer     │
│ • Runtime Reflection Schema Catalog Extractor               │
│ • Technical AI & Developer Manifest Exporter                │
└─────────────────────────────────────────────────────────────┘
```

---

## 📦 Output Formats

Observation sessions write output artifacts directly to your configured directory (default: `logs/` or `Documents/SimMetryx/TelemetrySniff`):

1. **`sniff_<GameName>_<Timestamp>.jsonl`**:
   High-frequency newline-delimited JSON stream of discrete capture frames containing:
   - Synchronized UTC timestamp
   - Simulator lifecycle state (`gameRunning`, `gamePaused`, `sessionType`, `carModel`, `trackCode`)
   - Normalized physics baseline (`SpeedKmh`, `Rpms`, `Gear`, `Inputs`, `G-forces`, `Tyres`, `Fuel`)
   - Dynamic SimHub property dictionary
   - Game-specific raw memory buffer payload (`GetRawDataObject()`)

2. **`sniff_<GameName>_<Timestamp>.catalog.json`**:
   Ordered channel dictionary cataloging every telemetry property discovered during runtime, including property names, data types, and initial sample values.

3. **`sniff_<GameName>_<Timestamp>.manifest.json`**:
   Compact developer and AI manifest detailing technical metadata:
   - Active simulator, session type, vehicle, and circuit
   - Start / end timestamps, session duration, and average capture Hz
   - Total frames recorded and count of discovered properties
   - File references to the associated `.jsonl` stream and `.catalog.json` catalog

---

## ⚙️ Operating Control Panel & Controls

The plugin exposes an interactive WPF control panel within SimHub's left navigation under **SimMetryx Observer**:

* **Master Power Toggle (`BtnMasterPower`)**: Silence or activate the observer on demand without disabling the plugin globally in SimHub.
* **4-Stage State Machine**:
  - `⚪ STANDBY`: Observer active, no simulator process detected.
  - `🟡 ARMED`: Observer primed; automatically begins recording as soon as the simulator process launches.
  - `🔴 RECORDING`: Live raw packet capture actively writing to disk.
  - `⏸️ DISABLED`: Observer powered down via control panel toggle.
* **Output Folder Selector**: Directly editable path with native directory browser dialog (`Browse...`) and quick-launch explorer buttons for recent captures and catalogs.
* **Bindable Actions**:
  - `SimMetryx.TelemetrySniff.ToggleMasterPower`: Toggle master power switch.
  - `SimMetryx.TelemetrySniff.ToggleCapture`: Toggle arm/capture state via steering wheel buttons or StreamDeck.
  - `SimMetryx.TelemetrySniff.StartCapture` / `StopCapture`: Explicit capture triggers.

---

## 🛠️ Building & Testing

### Prerequisites
- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or higher
- Optional: Local SimHub installation at `D:\Program Files\SimHub` or `C:\Program Files (x86)\SimHub` (automatic fallback stubs included in `SimHubStubs/` for non-SimHub build environments)

### Build Commands
```bash
# Build the complete solution
dotnet build SimMetryx-TelemetrySniff.sln -c Release

# Execute test suite
dotnet test SimMetryx-TelemetrySniff.sln
```

When built on a system with SimHub installed, the post-build event automatically deploys `SimMetryxObserver.dll` and `simmetryxObserver.dll` directly to your SimHub directory.

---

## 📄 License
MIT License.
