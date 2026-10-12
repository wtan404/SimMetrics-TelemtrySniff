# SimMetrics-TelemtrySniff

A unified **SimHub C# plugin** designed to sniff, capture, and catalog telemetry streams across multiple racing simulators—including **Automobilista 2 (AMS2)**, **iRacing**, **Assetto Corsa / ACC**, and **BeamNG.drive**.

The operational intent of this tool is to act as an automated telemetry reverse-engineering recorder and schema generator for downstream data analytics, ML modeling, and dashboard development.

---

## 🏎️ Telemetry Architecture: One Size Fits All vs. Variations

When building telemetry applications across simulators, you face two levels of integration:

```
┌─────────────────────────────────────────────────────────────┐
│                       SimHub Core                           │
│  (Aggregates game processes, shared memory, and UDP ports)  │
└──────────────────────────────┬──────────────────────────────┘
                               │
            ┌──────────────────┴──────────────────┐
            ▼                                     ▼
┌───────────────────────────────┐   ┌───────────────────────────────┐
│     Tier 1: Normalized        │   │    Tier 2: Game-Specific      │
│     (One-Size-Fits-All)       │   │    (Variations & Raw)         │
├───────────────────────────────┤   ├───────────────────────────────┤
│ • Speed, RPM, Gear            │   │ • iRacing: VarHeader Dict     │
│ • Throttle, Brake, Clutch     │   │ • AMS2: $pcars2$ SharedMem    │
│ • Orientation & G-Forces      │   │ • AC: acpmf_physics / static  │
│ • Standard Lap Timing         │   │ • BeamNG: OutGauge / OutSim   │
└───────────────────────────────┘   └───────────────────────────────┘
            │                                     │
            └──────────────────┬──────────────────┘
                               ▼
        ┌──────────────────────────────────────────────┐
        │       SimMetrics.TelemetrySniff.dll          │
        │   • Dynamic Property Reflection              │
        │   • Non-blocking Thread-safe Disk Queue      │
        │   • Session .jsonl & .catalog.json Generator │
        └──────────────────────────────────────────────┘
```

1. **SimHub Integration (Single DLL)**: You only need **one single `.dll`**. SimHub hosts the plugin for all games without requiring per-game executables.
2. **Telemetry Payload (Two-Tier Model)**:
   - **Tier 1 (Universal)**: Standardized `GameData.NewData` fields provide a consistent baseline across 50+ simulators.
   - **Tier 2 (Game Variations)**: Each sim exposes game-exclusive channels (e.g. iRacing hybrid deployment modes and incident points; AMS2 turbo boost and brake rotor temps; AC tire temperature curves; BeamNG chassis node stress).

`SimMetrics-TelemtrySniff` bridges this gap through **Automated Schema Discovery**: instead of manually hand-coding field maps, the sniffer dynamically inspects SimHub's runtime property catalog and serializes both the normalized baseline and raw game payloads.

---

## 📦 Output Formats

Each capture session produces two artifacts in your documents folder (`Documents/SimMetrics/TelemetrySniff`):

1. **`sniff_<GameName>_<Timestamp>.jsonl`**: A stream of newline-delimited JSON frames captured at your selected sampling rate (1–60 Hz).
2. **`sniff_<GameName>_<Timestamp>.catalog.json`**: An ordered dictionary of every single telemetry property exposed by SimHub for that specific car, track, and simulator, with current data types and sample values.

---

## 🛠️ Building & Installing

### Prerequisites
- Windows 10/11
- [.NET Framework 4.8 Developer Pack](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48) (or modern .NET SDK)
- [SimHub](https://www.simhubdash.com/) installed (default: `C:\Program Files (x86)\SimHub`)

### Build Steps
1. Clone the repository:
   ```bash
   git clone https://github.com/wtan404/SimMetrics-TelemtrySniff.git
   cd SimMetrics-TelemtrySniff
   ```
2. Build the Release configuration:
   ```bash
   dotnet build src/SimMetrics.TelemetrySniff.csproj -c Release
   ```
   *(On Windows, the post-build event will automatically attempt to copy `SimMetrics.TelemetrySniff.dll` directly to your SimHub folder if detected).*

3. Manual Installation (if preferred):
   - Copy `bin/Release/SimMetrics.TelemetrySniff.dll` into `C:\Program Files (x86)\SimHub\`.

4. Enable in SimHub:
   - Launch SimHub.
   - Navigate to **Settings** > **Plugins**.
   - Enable **SimMetrics Telemetry Sniff**.
   - Open the new **Telemetry Sniff** tab in SimHub's left menu.

---

## 🎮 Game Setup Notes

| Simulator | Telemetry Protocol | SimHub Requirement |
|---|---|---|
| **iRacing** | Windows Memory-Mapped File (`Local\IRSDKMemMapFileName`) | Starts automatically when session loads. SimHub auto-detects. |
| **Automobilista 2 (AMS2)** | Project CARS 2 Shared Memory (`$pcars2$`) | In AMS2: *Options > System > Shared Memory > Project CARS 2*. |
| **Assetto Corsa / ACC** | Kunos Shared Memory (`acpmf_physics`, `acpmf_static`) | Enabled by default in AC / ACC. SimHub connects directly. |
| **BeamNG.drive** | UDP OutGauge / OutSim | In BeamNG: *Options > Other > OutGauge Support*, set IP `127.0.0.1` and Port `4444`. |

---

## 🧭 Steering Wheel / Button Box Bindings

The plugin registers native SimHub actions that you can bind to wheel buttons or keyboard shortcuts:
- `SimMetrics.TelemetrySniff.ToggleCapture`
- `SimMetrics.TelemetrySniff.StartCapture`
- `SimMetrics.TelemetrySniff.StopCapture`

---

## 📄 License
MIT License.
