# SimMetryx-TelemetrySniff

A multi-mode telemetry capture, packet sniffing, and cataloging suite designed for virtual motorsports simulators—featuring a standalone **Assetto Corsa UDP Broadcast Listener & Protocol Sniffer** (`SimMetryx.TelemetrySniff.Cli`) alongside a unified **SimHub C# plugin** (`SimMetryxObserver.dll`).

The operational intent of this tool is to act as an automated telemetry reverse-engineering recorder and schema generator for downstream data analytics, ML modeling, and dashboard development across **Assetto Corsa**, **Automobilista 2 (AMS2)**, **iRacing**, and **BeamNG.drive**.

---

## 🏎️ Telemetry Architecture: Dual-Mode Operation

```
┌─────────────────────────────────────────────────────────────┐
│              SimMetryx.TelemetrySniff.Core                  │
│   • Universal TelemetryPacket & NormalizedData Channels     │
│   • Assetto Corsa Binary Datagram Parser (RT_CAR_INFO, Lap) │
│   • Non-blocking JSONL Streamer & Schema Catalog Generator  │
└──────────────────────────────┬──────────────────────────────┘
                               │
            ┌──────────────────┴──────────────────┐
            ▼                                     ▼
┌───────────────────────────────┐   ┌───────────────────────────────┐
│ SimMetryx.TelemetrySniff.Cli  │   │ SimMetryxObserver.dll         │
│ (.NET 8 Standalone App)       │   │ (SimHub Observer Plugin)      │
├───────────────────────────────┤   ├───────────────────────────────┤
│ • Interactive Terminal HUD    │   │ • SimHub In-App Capture       │
│ • Active AC UDP Handshake     │   │ • 50+ SimHub Supported Titles │
│ • Passive UDP Port Sniffing   │   │ • Dynamic Property Reflection │
│ • Zero External Dependencies  │   │ • WPF Configuration Card      │
└───────────────────────────────┘   └───────────────────────────────┘
```

1. **Standalone CLI Mode (`SimMetryx.TelemetrySniff.Cli`)**:
   - Zero dependency on SimHub or external host software.
   - Actively connects to Assetto Corsa via UDP port `9996` with full handshake negotiation.
   - Passively sniffs incoming broadcast UDP packets across any specified port.
   - Provides an interactive ANSI terminal HUD with real-time tachometer, speedometer, input meters, and tyre telemetry.
2. **SimHub Observer Plugin Mode (`simmetryxObserver.dll`)**:
   - Hosted directly inside SimHub for integrated in-rig capture.
   - Dynamic reflection on SimHub's runtime property catalog for automated schema dumps.
   - Live simulator status indicator and on/off observation toggle.

---

## 📦 Output Formats

Each observation session produces artifacts in your documents folder (`Documents/SimMetryx/TelemetrySniff`) or custom output directory:

1. **`sniff_<GameName>_<Timestamp>.summary.md`**: An AI/engineer-readable Markdown session manifest summarizing vehicle, circuit, laps, top speed, tyre thermal envelope, fuel deltas, and discovered channels.
2. **`sniff_<GameName>_<Timestamp>.summary.json`**: Compact JSON metadata representation of the session performance envelope.
3. **`sniff_<GameName>_<Timestamp>.jsonl`**: A continuous stream of newline-delimited JSON frames captured at high frequency (1–60+ Hz).
4. **`sniff_<GameName>_<Timestamp>.catalog.json`**: An ordered dictionary of every telemetry channel discovered during the capture session, complete with data types and sample values.

---

## 🏁 Assetto Corsa UDP Setup & Quickstart

Assetto Corsa features a dedicated remote telemetry protocol that broadcasts high-resolution physics and lap timings over UDP.

### 1. In-Game Configuration
By default, Assetto Corsa enables remote telemetry on UDP port `9996`.
To verify or customize your configuration:
1. Navigate to your Assetto Corsa installation directory: `steamapps\common\assettocorsa\system\cfg\assetto_corsa.ini`.
2. Confirm the `[REMOTE_TELEMETRY]` section:
   ```ini
   [REMOTE_TELEMETRY]
   PORT=9996
   ```

### 2. Running the Standalone Sniffer

From the repository root:

#### Mode A: Active UDP Handshake (Default)
Connects directly to Assetto Corsa, completes the binary handshake, and streams real-time vehicle telemetry:
```bash
dotnet run --project src/SimMetryx.TelemetrySniff.Cli -- --ip 127.0.0.1 --port 9996
```

#### Mode B: Passive Broadcast Sniffing
Listens on a local UDP port for broadcast or forwarded packets without issuing handshake datagrams:
```bash
dotnet run --project src/SimMetryx.TelemetrySniff.Cli -- --mode passive --port 9996
```

#### Mode C: Simulated Demo Mode
Previews the real-time ANSI terminal HUD and verifies disk recording without launching the game:
```bash
dotnet run --project src/SimMetryx.TelemetrySniff.Cli -- --mode demo
```

### CLI Command Options
```
Options:
  --mode <active|passive|demo>  Operation mode (Default: active)
  --ip <ip_address>             Target simulator host IP (Default: 127.0.0.1)
  --port <port_number>          UDP telemetry port (Default: 9996)
  --out <directory_path>        Custom directory for output .jsonl and .catalog.json files
  --no-record                   Disable saving capture frames to disk
  --help, -h                    Show help screen
```

### Hotkeys in Terminal HUD
- `[Q]` or `[Esc]`: Gracefully stops the capture, flushes pending queues, and exports the `.catalog.json` schema.
- `[H]`: Triggers a manual handshake packet to Assetto Corsa.

---

## 🛠️ Building the Entire Solution

### Prerequisites
- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) or higher
- Optional: SimHub (for plugin build against local SimHub installation)

### Build Commands
1. Clone the repository:
   ```bash
   git clone https://github.com/wtan404/SimMetrics-TelemtrySniff.git
   cd SimMetrics-TelemtrySniff
   ```
2. Build the solution:
   ```bash
   dotnet build SimMetryx-TelemetrySniff.sln -c Release
   ```
3. Run tests:
   ```bash
   dotnet test SimMetryx-TelemetrySniff.sln
   ```

---

## 🎮 Simulator Protocol Compatibility Matrix

| Simulator | Protocol | Supported In CLI | Supported In SimHub Plugin |
|---|---|:---:|:---:|
| **Assetto Corsa** | UDP Remote Telemetry (Port `9996`) | ✅ Native Active + Passive | ✅ via Shared Memory |
| **Assetto Corsa Competizione (ACC)** | UDP Broadcast Protocol (Port `9231`) | ✅ Passive Sniffer | ✅ via Shared Memory |
| **Automobilista 2 (AMS2)** | Project CARS 2 Shared Memory (`$pcars2$`) | — | ✅ Full Capture |
| **iRacing** | Windows Memory-Mapped File (`Local\IRSDKMemMapFileName`) | — | ✅ Full Capture |
| **BeamNG.drive** | UDP OutGauge / OutSim (Port `4444`) | ✅ Passive Sniffer | ✅ OutGauge Parser |

---

## 📄 License
MIT License.
