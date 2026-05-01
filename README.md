# trojan4win

[![License: AGPL-3.0](https://img.shields.io/badge/License-AGPL%20v3-blue.svg)](LICENSE)

**trojan4win** is a Windows GUI client powered by [trojan-go](https://github.com/kis1yi/trojan-go),
an extended Trojan proxy implementation. It routes system-wide traffic through a Trojan server using
[ProxiFyre](https://github.com/wiresock/proxifyre) (SOCKS5 system proxy) and the
[Windows Packet Filter (NDISAPI)](https://github.com/wiresock/ndisapi) kernel driver —
no manual proxy configuration required.

---

## Features

- **Multi-server management** — add, remove, clone, and edit server profiles; per-server
  usage statistics and auto-ping latency display
- **System-wide routing** — ProxiFyre + NDISAPI driver intercept traffic at the kernel
  level, covering apps that do not respect system proxy settings
- **TCP and UDP** — both protocols are forwarded through the trojan-go tunnel
- **Process filter modes** — exclude listed apps from the proxy (default) or route only
  listed apps through the proxy; switchable per-session (new in v1.1.0)
- **Mux multiplexing** — reduce TLS handshake overhead with trojan-go's built-in
  connection multiplexer (smux / yamux / mplex)
- **WebSocket transport** — tunnel traffic over WebSocket for CDN and reverse-proxy
  compatibility
- **Shadowsocks AEAD secondary encryption** — optional AES-GCM or ChaCha20 layer
  wrapping the Trojan stream
- **Structured router rules** — domain / IP / CIDR / GeoIP / GeoSite rules with
  per-rule bypass, proxy, or block policy
- **Forward proxy** — chain trojan-go through an upstream SOCKS5 proxy
- **ECH and uTLS fingerprinting** — Encrypted ClientHello support and browser TLS
  fingerprint spoofing (Firefox, Chrome, and others)
- **Traffic monitoring** — real-time upload/download speed and per-session totals
- **System tray** — minimize to tray; app stays running in the background
- **Auto-start with Windows** — optional registry-based startup entry
- **Auto-connect on launch** — reconnect to the last active server automatically
- **Log viewers** — inspect live trojan-go and ProxiFyre log output from within the UI

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

> **Note:** The installer bundles the .NET 8 Runtime, ProxiFyre, the trojan-go client,
> and V2Fly geo data files. No manual dependency setup is needed for regular users.

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
| trojan-go | 1.2.0 | [trojan-go-windows-amd64.zip](https://github.com/kis1yi/trojan-go/releases/tag/v1.2.0) | `trojan4win/Tools/trojan/` |
| geoip.dat | latest | [geoip.dat](https://github.com/v2fly/geoip/releases/latest) — from v2fly/geoip; placed next to trojan-go.exe by CI | `trojan4win/Tools/trojan/` |
| geosite.dat | latest | [dlc.dat](https://github.com/v2fly/domain-list-community/releases/latest) — from v2fly/domain-list-community, saved as geosite.dat by CI | `trojan4win/Tools/trojan/` |
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

The test suite has **126 tests** covering services, ViewModels, and headless UI integration:

```bash
dotnet test
```

See [TESTING.md](TESTING.md) for the full breakdown — categories, filter examples,
known considerations (parallelism, ICMP, culture pinning, headless Avalonia limitations).

---

## Contributing

1. Fork the repository and create a feature branch.
2. Make your changes — the existing 126-test suite acts as a safety net.
3. Ensure `dotnet test` passes and `dotnet build -c Release` succeeds.
4. Open a pull request with a clear description of what changed and why.

Code style follows standard C# conventions. All public-facing service changes benefit
from a matching unit test in `trojan4win.Tests/`.

---

## Acknowledgements

trojan4win is built on top of three excellent open-source projects:

- **[kis1yi/trojan-go](https://github.com/kis1yi/trojan-go)** — the extended Trojan proxy
  client that handles the TLS tunnel to the server
- **[wiresock/proxifyre](https://github.com/wiresock/proxifyre)** — the SOCKS5
  system-wide proxy that feeds traffic from all processes into Trojan
- **[wiresock/ndisapi](https://github.com/wiresock/ndisapi)** — the Windows Packet
  Filter kernel driver that intercepts traffic at the network layer

---

## License

The trojan4win source code is licensed under the
[GNU Affero General Public License v3.0](LICENSE).

This project downloads and bundles the following third-party tools,
each governed by its own license:

| Tool | License |
|------|---------|
| [trojan-go](https://github.com/kis1yi/trojan-go) | [GPL-3.0](https://github.com/kis1yi/trojan-go/blob/master/LICENSE) |
| [geoip.dat](https://github.com/v2fly/geoip) | [see upstream](https://github.com/v2fly/geoip/blob/release/LICENSE) |
| [geosite.dat](https://github.com/v2fly/domain-list-community) | [MIT](https://github.com/v2fly/domain-list-community/blob/master/LICENSE) |
| [ProxiFyre](https://github.com/wiresock/proxifyre) | [AGPL-3.0](https://github.com/wiresock/proxifyre/blob/main/LICENSE) |
| [NDISAPI](https://github.com/wiresock/ndisapi) | [MIT](https://github.com/wiresock/ndisapi/blob/master/LICENSE) |

The distributed installer includes these binaries.
Their respective licenses apply to those components.

