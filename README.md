# trojan4win

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**trojan4win** is a Windows GUI client for the [Trojan](https://github.com/trojan-gfw/trojan)
proxy protocol. It routes system-wide traffic through a Trojan server using
[ProxiFyre](https://github.com/wiresock/proxifyre) (SOCKS5 system proxy) and the
[Windows Packet Filter (NDISAPI)](https://github.com/wiresock/ndisapi) kernel driver —
no manual proxy configuration required.

---

## Features

- **Multi-server management** — add, remove, clone, and edit server profiles; per-server
  usage statistics and auto-ping latency display
- **System-wide routing** — ProxiFyre + NDISAPI driver intercept traffic at the kernel
  level, covering apps that do not respect system proxy settings
- **TCP and UDP** — both protocols are forwarded through the Trojan tunnel
- **Per-app exclusions** — define process names that bypass the proxy
- **Traffic monitoring** — real-time upload/download speed and per-session totals
- **System tray** — minimize to tray; app stays running in the background
- **Auto-start with Windows** — optional registry-based startup entry
- **Auto-connect on launch** — reconnect to the last active server automatically
- **Log viewers** — inspect live Trojan and ProxiFyre log output from within the UI

---

## System Requirements

| Requirement | Detail |
|---|---|
| OS | Windows 10 or Windows 11 (x64) |
| Architecture | x64 only |
| .NET Runtime | .NET 8 — bundled by the installer if not already present |
| Driver | Windows Packet Filter (NDISAPI) — optional install step in the setup wizard |
| Admin rights | Required to install and load the NDISAPI kernel driver |

---

## Installation

1. Go to the [Releases](../../releases) page and download the latest
   `trojan4win-setup-x.x.x.exe` installer.
2. Run the setup wizard — it installs trojan4win and, optionally, the
   **Windows Packet Filter (NDISAPI)** kernel driver. Install the driver if you
   want system-wide routing (recommended).
3. Launch **trojan4win** from the Start Menu or desktop shortcut.

> **Note:** The installer bundles the .NET 8 Runtime, ProxiFyre, and the Trojan binary.
> No manual dependency setup is needed for regular users.

---

## Getting Started

### Add a server

1. Click **Add Server** (or the `+` button) in the server list.
2. Fill in the required fields:
   - **Remote Address** — your Trojan server hostname or IP
   - **Remote Port** — typically `443`
   - **Password** — the Trojan server password
3. Adjust optional TLS/SSL settings (SNI, certificate verification, ALPN) if your server
   requires them.
4. Click **Save**.

### Connect

1. Select the server in the list.
2. Click **Connect** — ProxiFyre and the NDISAPI driver activate automatically,
   routing all system traffic through the Trojan tunnel.
3. The status bar shows real-time upload/download speed and session totals.

### Disconnect

Click **Disconnect** at any time. The system proxy is fully restored to its previous state.

---

## Configuration

| Option | Where | Description |
|---|---|---|
| Local SOCKS port / address | Server settings | Default `127.0.0.1:1080` |
| TCP / UDP | Server settings | Enable or disable UDP forwarding |
| Excluded processes | Main window | Process names that bypass the proxy |
| Auto-start with Windows | Settings | Adds a registry startup entry |
| Auto-connect on launch | Settings | Connects to the last active server on startup |
| Minimize to tray | Title bar / tray icon | App continues running in the background |

---

## Building from Source

### Prerequisites

| Tool | Version | Notes |
|---|---|---|
| [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) | 8.0+ | Required |
| [Inno Setup](https://jrsoftware.org/isinfo.php) | 6.x | Required only to build the installer |
| Visual Studio 2022+ | 17.x | Optional — solution opens in VS or Rider |

### Tool dependencies

The three bundled binaries are **not included in the repository** and must be placed
manually before building locally. The GitHub Actions CI workflow downloads them automatically.

| Tool | Version | Download | Destination |
|---|---|---|---|
| Trojan | 1.16.0 | [trojan-1.16.0-win.zip](https://github.com/trojan-gfw/trojan/releases/tag/v1.16.0) | `trojan4win/Tools/trojan/` |
| ProxiFyre | 2.2.0 | [ProxiFyre-v2.2.0-x64-signed.zip](https://github.com/wiresock/proxifyre/releases/tag/v2.2.0) | `trojan4win/Tools/proxifyre/` |
| NDISAPI driver | 3.6.2.1 | [Windows.Packet.Filter.3.6.2.1.x64.msi](https://github.com/wiresock/ndisapi/releases) | `trojan4win/Tools/ndisapi/` |

Extract zip archives directly into the destination folder. For the MSI, place the file
itself in the destination folder (the installer wraps it).

### Build

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Or open trojan4win.slnx in Visual Studio 2022+
```

### Build the installer

Requires [Inno Setup 6](https://jrsoftware.org/isinfo.php) with `iscc` on `PATH`:

```bash
iscc installer.iss
```

Output is written to `installer_output/`.

> **Tip:** The GitHub Actions workflow (`.github/workflows/`) handles tool downloads,
> the Release build, and installer packaging end-to-end on every tagged commit.

---

## Running Tests

The test suite has **88 tests** covering services, ViewModels, and headless UI integration:

```bash
dotnet test
```

See [TESTING.md](TESTING.md) for the full breakdown — categories, filter examples,
known considerations (parallelism, ICMP, culture pinning, headless Avalonia limitations).

---

## Contributing

1. Fork the repository and create a feature branch.
2. Make your changes — the existing 88-test suite acts as a safety net.
3. Ensure `dotnet test` passes and `dotnet build -c Release` succeeds.
4. Open a pull request with a clear description of what changed and why.

Code style follows standard C# conventions. All public-facing service changes benefit
from a matching unit test in `trojan4win.Tests/`.

---

## Acknowledgements

trojan4win is built on top of three excellent open-source projects:

- **[trojan-gfw/trojan](https://github.com/trojan-gfw/trojan)** — the Trojan proxy
  client that handles the TLS tunnel to the server
- **[wiresock/proxifyre](https://github.com/wiresock/proxifyre)** — the SOCKS5
  system-wide proxy that feeds traffic from all processes into Trojan
- **[wiresock/ndisapi](https://github.com/wiresock/ndisapi)** — the Windows Packet
  Filter kernel driver that intercepts traffic at the network layer

---

## License

This project is licensed under the **MIT License** — see the [LICENSE](LICENSE) file
for details.

