# Three Blind Mice 🐭🐭🐭

A lightweight remote mouse pointer overlay for screen-sharing sessions. Remote users point at things on your screen using just a browser — no install required for remote participants.

**How it works:**
1. The **host** (screen-sharer) downloads and runs a tiny mouse-receiver app (~200 KB)
2. Remote users open a link in their browser, join the room, and move their mouse
3. Coloured, named cursors appear on the host's desktop as a transparent, click-through overlay

## Quick Start

### For the Host (screen-sharer)

1. **Prerequisites:** [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
2. Visit the web app and create a room
3. Download the mouse-receiver for your platform (Windows or Linux)
4. Run the receiver with your room code:
   ```
   ThreeBlindMice.Windows.exe --room ABC123
   ```
5. Share the room link with remote participants
6. Cursors from remote users will appear on your desktop

### For Remote Users

1. Open the room link shared by the host
2. Enter your name and click **Join**
3. Click the mouse canvas area to lock your pointer
4. Move your mouse — the host will see your cursor on their screen
5. Press **Escape** to release your mouse

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
└──────────────────────────────────┬───────────────────────────────────┘
                                   │
                                   ▼
                          ┌──────────────────┐
                          │  Host Desktop     │
                          │  Mouse Receiver   │
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
| API | Azure Functions (Node.js) |
| Mouse-receiver | C# .NET 8 + platform P/Invoke |
| Infrastructure | Bicep + PowerShell |

## Project Structure

```
three-blind-mice/
├── web/                # React SPA (remote + host UI)
├── api/                # Azure Functions (negotiate endpoint)
├── mouse-receiver/     # C# .NET 8 headless overlay
│   ├── ThreeBlindMice.Core/      # Shared: WebSocket, protocol, state
│   ├── ThreeBlindMice.Windows/   # Win32 P/Invoke overlay (GDI+)
│   └── ThreeBlindMice.Linux/     # X11 P/Invoke overlay (Xlib)
├── infra/              # Bicep + deploy script
└── docs/               # Implementation plan
```

## Building from Source

### Web App

```powershell
cd web
npm install
npm run build     # Output: web/dist/
npm run dev       # Dev server on https://localhost:53100
```

### Mouse Receiver (Windows)

```powershell
cd mouse-receiver
dotnet publish ThreeBlindMice.Windows -c Release -r win-x64 --self-contained false -o ./publish/win-x64
```

### Mouse Receiver (Linux)

```bash
cd mouse-receiver
dotnet publish ThreeBlindMice.Linux -c Release -r linux-x64 --self-contained false -o ./publish/linux-x64
```

### Azure Infrastructure

```powershell
cd infra
./deploy.ps1 -SubscriptionId <id> -ResourceGroup rg-three-blind-mice -Location eastasia
```

## Platform Notes

- **Windows:** Uses Win32 API (`CreateWindowEx` with `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOPMOST`) and GDI+ for rendering. Works on Windows 10+.
- **Linux (X11):** Uses Xlib with ARGB visuals and XShape extension for click-through. Requires a compositor for transparency. **Wayland does not support click-through overlays** — use X11 or XWayland.
- **Remote users:** Any modern browser (Chrome, Firefox, Edge, Safari). No install needed.

## Security

The mouse-receiver is inherently low-risk — it is one-way visual only:
- ❌ No filesystem access
- ❌ No input simulation (click-through overlay)
- ❌ No screen capture
- ❌ No listening ports (outbound WebSocket only)
- ❌ No elevated privileges required
- ✅ Code-signed Windows binary
- ✅ Strict input validation on all WebSocket messages
- ✅ Room-scoped PubSub tokens

See [docs/plan.md](docs/plan.md) for the full security threat model.

## License

MIT
