# Three Blind Mice — Implementation Plan

## Problem

Remote users during video calls + screen share need a simple way to "point at things" on the host's screen. The goal is a zero-install experience for remote users (browser only), with a lightweight native overlay on the host's machine that draws remote mouse pointers above all desktop content.

## Key Technical Constraint

**A browser app cannot draw outside its own window bounds.** The browser sandbox prevents rendering beyond the viewport. Therefore this project requires a **hybrid architecture**:

- **Web app** (Azure Static Web App) — remote users join a room via link and move their mouse. Host also uses this for room management.
- **C#/WPF overlay** — runs on the host's desktop, connects to the same WebSocket room, renders transparent/topmost/click-through cursor overlays.

## Architecture

```
┌──────────────────┐     ┌──────────────────────┐     ┌──────────────────┐
│  Remote Browser   │────▶│  Azure Static Web App │────▶│  /api/negotiate  │
│  (React SPA)      │     │   (SPA + API)         │     │  (Azure Function)│
└──────┬───────────┘     └──────────────────────┘     └────────┬─────────┘
       │                                                        │
       │  WebSocket (json.webpubsub.azure.v1)                  │ Token
       │                                                        │
       ▼                                                        ▼
┌──────────────────────────────────────────────────────────────────────┐
│                     Azure Web PubSub                                 │
│  Hub: tbm    Groups: per-room    Protocol: JSON subprotocol          │
└──────────────────────────────────────┬───────────────────────────────┘
                                       │
                                       ▼
                              ┌──────────────────┐
                              │  Host Desktop     │
                              │  C#/WPF Overlay   │
                              │  (transparent,    │
                              │   topmost,        │
                              │   click-through)  │
                              └──────────────────┘
```

## Tech Stack

| Component | Technology |
|-----------|-----------|
| Web app | React + TypeScript + Vite |
| Realtime | Azure Web PubSub (Free tier) |
| Hosting | Azure Static Web App (Free tier) |
| API | Azure Functions (Node.js, serverless) |
| Mouse-receiver (shared) | C# .NET 8 class library (WebSocket, protocol, state) |
| Mouse-receiver (Windows) | C# .NET 8 console app + Win32 P/Invoke (GDI+ rendering) |
| Mouse-receiver (Linux) | C# .NET 8 console app + X11 P/Invoke (Xlib rendering) |
| Infra | Bicep + PowerShell deploy script |
| CI/CD | GitHub Actions (later) |

## Repository Structure

```
three-blind-mice/
├── .github/
│   └── copilot-instructions.md
├── web/                            # React SPA (remote + host UI)
│   ├── src/
│   │   ├── main.tsx
│   │   ├── App.tsx
│   │   ├── components/
│   │   │   ├── NameInput.tsx
│   │   │   ├── RoomPanel.tsx       # Create/join room, copy link
│   │   │   ├── ConstrainPanel.tsx  # Monitor/window/rectangle selection
│   │   │   ├── MouseCanvas.tsx     # Where remote user moves their mouse
│   │   │   └── UserList.tsx        # Online users with colours
│   │   ├── services/
│   │   │   └── pubsub.ts          # WebSocket connection + message types
│   │   └── types.ts
│   ├── index.html
│   ├── package.json
│   ├── vite.config.ts
│   ├── tsconfig.json
│   └── staticwebapp.config.json
├── api/                            # Azure Functions (serverless)
│   ├── negotiate/
│   │   ├── index.js
│   │   └── function.json
│   ├── host.json
│   └── package.json
├── mouse-receiver/                 # C# .NET 8 — headless overlay (no UI framework)
│   ├── ThreeBlindMice.Core/        # Shared class library
│   │   ├── ThreeBlindMice.Core.csproj
│   │   ├── PubSubClient.cs         # WebSocket connection + reconnect
│   │   ├── Protocol.cs             # JSON message types + serialisation
│   │   ├── CursorState.cs          # Track positions/colours per user
│   │   └── ProtocolHandler.cs      # tbm:// URI parsing
│   ├── ThreeBlindMice.Windows/     # Windows console app + Win32 P/Invoke
│   │   ├── ThreeBlindMice.Windows.csproj
│   │   ├── Program.cs
│   │   ├── Win32Overlay.cs         # CreateWindowEx (WS_EX_LAYERED|TRANSPARENT|TOPMOST)
│   │   ├── CursorRenderer.cs       # GDI+ arrow polygon + name label
│   │   ├── TrayIcon.cs             # Shell_NotifyIcon for system tray
│   │   └── ProtocolRegistrar.cs    # HKCU registry for tbm:// protocol
│   ├── ThreeBlindMice.Linux/       # Linux console app + X11 P/Invoke
│   │   ├── ThreeBlindMice.Linux.csproj
│   │   ├── Program.cs
│   │   ├── X11Overlay.cs           # XCreateWindow + XShape click-through
│   │   ├── CursorRenderer.cs       # Xlib drawing (XFillPolygon, XDrawString)
│   │   └── TrayIcon.cs             # StatusNotifierItem / XEmbed
│   ├── ThreeBlindMice.MouseReceiver.sln
│   └── publish.ps1                 # Build + sign script (per-platform)
├── infra/
│   ├── main.bicep
│   ├── main.bicepparam.json
│   └── deploy.ps1
├── .gitignore
├── .env.example
└── README.md
```

## Message Protocol (WebSocket)

Messages sent via Web PubSub group (room-scoped):

```json
// Mouse position update (remote → host overlay)
{
    "type": "cursor",
    "user_id": "abc123",
    "name": "Alice",
    "colour": "#FF6B35",
    "x": 0.45,          // normalised [0..1] within constrain region
    "y": 0.62,
    "button": 0          // 0=none, 1=left (laser), 2=right (reserved)
}

// User joined
{
    "type": "join",
    "user_id": "abc123",
    "name": "Alice",
    "colour": "#FF6B35"
}

// User left
{
    "type": "leave",
    "user_id": "abc123"
}

// Host config (host → remotes, defines the constrain region aspect ratio)
{
    "type": "host_config",
    "aspect_ratio": 1.778,
    "monitor_name": "Monitor 1"
}
```

## Todos

### Phase 1: Project Scaffolding
1. **repo-init** — Initialise git repo, .gitignore, README, copilot-instructions.md, save this plan as `docs/plan.md` in the repo
2. **web-scaffold** — Scaffold React + Vite + TypeScript project in `web/`
3. **api-scaffold** — Create negotiate Azure Function in `api/` (based on whiteboard-live pattern)
4. **overlay-scaffold** — Create C#/WPF project in `overlay/` targeting .NET 8
5. **infra-setup** — Bicep template + deploy script (adapted from whiteboard-live)

### Phase 2: Web App (Remote User Experience)
6. **web-room-mgmt** — Room creation (generate code), join by code/link, URL param `?room=xxx`
7. **web-pubsub-client** — WebSocket connection, join/leave/cursor message handling
8. **web-mouse-canvas** — Constrain region canvas where remote user moves mouse, sends normalised coords
9. **web-host-ui** — Host-side UI: name, monitor/window/rectangle selection, invite panel, user list
10. **web-styling** — Match the mockup UI styling (blue theme, cards, layout)

### Phase 3: Mouse-Receiver — Shared Core
11. **receiver-core** — .NET 8 class library `ThreeBlindMice.Core`: PubSubClient (WebSocket via `System.Net.WebSockets`, JSON via `System.Text.Json`), Protocol message types, CursorState tracker, tbm:// URI parser. Zero external NuGet dependencies.

### Phase 4: Mouse-Receiver — Windows
12. **receiver-win-overlay** — Win32 overlay window via P/Invoke: `CreateWindowEx` with `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST`, message pump, DPI-aware
13. **receiver-win-render** — GDI+ cursor rendering: coloured arrow polygon + name label text, positioned at mapped screen coordinates. Invalidate/repaint on cursor updates.
14. **receiver-win-tray** — System tray icon via `Shell_NotifyIcon`: shows room code, connection status, quit option
15. **receiver-win-protocol** — Register `tbm://` protocol under `HKCU\Software\Classes\tbm` on first run; parse `tbm://room/<code>` command-line args

### Phase 5: Mouse-Receiver — Linux
16. **receiver-linux-overlay** — X11 overlay window via P/Invoke to `libX11.so`: `XCreateWindow` with ARGB visual, `_NET_WM_STATE_ABOVE` for topmost, XShape extension for click-through input mask
17. **receiver-linux-render** — Xlib cursor rendering: `XFillPolygon` for arrow, `XDrawString` for name label, coloured via `XAllocColor`
18. **receiver-linux-tray** — System tray via StatusNotifierItem (D-Bus) or XEmbed fallback

### Phase 6: Distribution & Deployment
19. **receiver-publish** — `mouse-receiver/publish.ps1`: framework-dependent single-file publish for both `win-x64` and `linux-x64` targets. Output ~100-500 KB per platform.
20. **receiver-sign** — Code signing step in publish script: sign Windows exe with SignTool (.pfx cert, SHA256, RFC 3161 timestamp). `-CertPath` and `-CertPassword` params.
21. **web-host-download** — Web app hosts both platform binaries in `web/public/downloads/`. Detects user-agent OS and offers correct download. Download button + .NET 8 runtime instructions.
22. **web-launch-overlay** — "Start Hosting" button: if `tbm://` protocol registered, launches via `tbm://room/<code>` link; otherwise prompts download. OS-aware.

### Phase 7: Integration & Polish
23. **security-hardening** — Implement all items from the Security Checklist (see below): input validation, URI whitelist, rate limiting, SHA256 hash on download page, strict JSON deserialization, no shell execution audit.
24. **integration-test** — End-to-end: remote browser → web pubsub → mouse-receiver draws cursor on host desktop
25. **multi-monitor** — Host selects which monitor(s) to constrain to; receiver maps normalised coords to screen bounds
26. **readme-docs** — Complete README with setup, usage, architecture, per-platform instructions

### Phase 8: Enhanced Features (post-MVP)
27. **laser-pointer** — Laser pointer trail:when remote user holds left mouse button, overlay draws a fading trail behind the cursor (like a laser pointer). Web app sends `button: 1` in cursor messages while mouse is down. Overlay maintains a short position history per user and renders a glowing, fading polyline trail in the user's colour.

## Security Threat Model

### What the App Does NOT Do (inherently safe)

The mouse-receiver's attack surface is inherently tiny because it is fundamentally **one-way visual** — it receives coordinates over a WebSocket and draws pixels on a transparent overlay. It does NOT:

- ❌ Access the filesystem (no reads, no writes, no config files)
- ❌ Simulate keyboard or mouse input (overlay is visual-only, click-through)
- ❌ Capture screen content or keystrokes
- ❌ Open listening ports (only outbound WebSocket to Azure)
- ❌ Require admin/elevated privileges (runs as normal user)
- ❌ Execute code from received messages (just parses JSON floats/strings)
- ❌ Store persistent data (no database, no local storage)
- ❌ Send any local system information over the network

### Attack Surfaces & Mitigations

| # | Attack Surface | Threat | Impact | Mitigation |
|---|---------------|--------|--------|------------|
| 1 | **WebSocket message injection** | Attacker obtains room code, joins room, sends crafted messages | Spam fake cursors (annoying, not dangerous). Oversized name strings (resource exhaustion). Out-of-range coords. | **Strict input validation** in receiver: cap name to 20 chars, validate colour as `#[0-9A-Fa-f]{6}`, clamp x/y to [0,1], reject unknown message types. **Rate-limit** cursor updates per user (~30/sec max). |
| 2 | **`tbm://` protocol handler injection** | Malicious website crafts `tbm://;rm -rf /` or `tbm://--server=evil.com` link | Command injection, connecting to attacker-controlled server | **Whitelist validation**: room code must match `^[a-zA-Z0-9]{4,8}$`, reject everything else. **Hardcode** the negotiate endpoint URL — never accept server URLs from the URI. **Never** pass URI to shell/exec. Browser shows confirmation dialog before launch. |
| 3 | **Binary tampering / MITM** | Attacker replaces download with malicious binary | User runs malware | **HTTPS** delivery from Azure SWA. **Code signing** with trusted certificate. **SHA256 hash** displayed on web app for manual verification. |
| 4 | **Room code brute-force** | Attacker guesses room codes to join sessions | See cursor names, spam fake cursors | Use **6-char alphanumeric** codes (2.1 billion combinations). **Rate-limit** negotiate API (e.g., 10 requests/min per IP). Consider optional room passwords for sensitive sessions. |
| 5 | **Web PubSub token scope** | Token with overly broad permissions | Attacker could send messages to other rooms | Tokens are **room-scoped**: `webpubsub.joinLeaveGroup.{room}` + `webpubsub.sendToGroup.{room}` only. No wildcard access. |
| 6 | **Denial of service** | Flood of cursor messages | Receiver CPU spike, overlay rendering lag | **Per-user rate limit** in receiver (drop excess messages). **Max users per room** cap (e.g., 10). Cursor auto-hide after 3s inactivity acts as natural cleanup. |

### Security Implementation Checklist

- [ ] Input validation on ALL received WebSocket fields (name, colour, x, y, button, user_id)
- [ ] `tbm://` URI parsed with strict regex — room code only, no other parameters accepted
- [ ] Negotiate API hardcoded in the binary — not configurable via URI or messages
- [ ] Room codes are 6+ alphanumeric characters, generated with `RandomNumberGenerator`
- [ ] Code signing on Windows binary
- [ ] SHA256 hash displayed on download page
- [ ] System.Text.Json strict deserialization (no polymorphic, no type-name handling)
- [ ] No `Process.Start`, `System.Diagnostics`, or shell execution anywhere in receiver
- [ ] All WebSocket connections use `wss://` (TLS) — enforced by Azure Web PubSub
- [ ] Rate limiting at negotiate API level (per IP)

## Notes

- The mouse-receiver is a **headless .NET 8 console app** — no WPF, no Avalonia, no UI framework. Overlay windows and rendering use direct platform P/Invoke for minimum size (~100-500 KB framework-dependent per platform).
- **Windows overlay:** `CreateWindowEx` with `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST` + `SetLayeredWindowAttributes`. GDI+ via `System.Drawing` (or raw GDI P/Invoke) for rendering. Requires a Win32 message pump (`GetMessage`/`DispatchMessage`).
- **Linux overlay:** `XCreateWindow` with 32-bit ARGB visual + compositor transparency. `_NET_WM_STATE_ABOVE` hint for always-on-top. `XShapeCombineRectangles` (XShape extension) for click-through input mask. Xlib drawing primitives for rendering. **Known limitation: Wayland does not support click-through overlays.** Document that hosts on Wayland should use X11 session or XWayland.
- **Shared core:** `ThreeBlindMice.Core` class library — WebSocket client (`System.Net.WebSockets.ClientWebSocket`), JSON protocol (`System.Text.Json`), cursor state management. Zero external NuGet packages.
- **Web app detects OS** via user-agent string and serves the correct platform binary from `web/public/downloads/`.
- Cursor colours are auto-assigned from a palette when users join. Each cursor is an arrow SVG/path + name label.
- Mouse positions are normalised (0-1) relative to the constrain region, so the overlay can map them to actual screen coordinates regardless of resolution differences.
- The constrain region on the web app matches the host's selected monitor/window aspect ratio (sent via `host_config` message).
- The Web PubSub hub name will be `tbm` (three blind mice) to distinguish from whiteboard-live.
- Free tier Web PubSub supports 20 concurrent connections and 20K messages/day — plenty for this use case.
- The overlay should auto-hide cursors after ~3 seconds of inactivity (fade out).
- **Distribution:** Both platform exes are published as framework-dependent single-file (~100-500 KB each). Hosted in `web/public/downloads/` on the SWA. Requires .NET 8 runtime on the host machine.
- **Code signing:** `mouse-receiver/publish.ps1` signs the Windows exe with `signtool.exe` with a `.pfx` code signing certificate. Parameters: `-CertPath <path>` and `-CertPassword <password>`. Uses `/fd SHA256 /tr http://timestamp.digicert.com /td SHA256` for SHA256 Authenticode + RFC 3161 timestamping. The `.pfx` file is never committed to the repo (listed in `.gitignore`). Signing ensures Windows trusts the download (no SmartScreen warnings).
- **Custom protocol (`tbm://`):**On first run, the overlay registers `tbm://` as a custom URI protocol under `HKCU\Software\Classes\tbm` (no admin rights needed). The web app can then launch the overlay via `tbm://room/<code>`, and the overlay parses the URI to auto-join the room.
- **Transient lifecycle:** The overlay is not a persistent background service. It launches when the host clicks "Start Hosting" (or runs it manually), connects to the room, and exits when the session ends or the user quits. No installer, no Windows service, no startup entry.
- **Laser pointer trail:** When the remote user holds left mouse button, `button: 1` is sent in cursor messages. The overlay maintains a ring buffer of recent positions per user (~50 points) and renders a glowing polyline trail that fades from full opacity at the cursor to transparent at the tail. Trail colour matches the user's cursor colour. The trail clears immediately when the button is released.
