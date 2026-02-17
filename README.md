# Three Blind Mice

A lightweight remote mouse pointer overlay for screen-sharing sessions. Remote users point at things on your screen using just a browser — no install required.

**How it works:**
1. The **host** (screen-sharer) runs a tiny mouse-receiver app (~500 KB)
2. Remote users open a link in their browser and move their mouse
3. Coloured, named cursors appear on the host's desktop as a transparent overlay

Built with React + TypeScript (web), C# .NET 8 (mouse-receiver), and Azure Static Web Apps + Web PubSub (infrastructure).

## Quick Start

*Coming soon — see [docs/plan.md](docs/plan.md) for the implementation plan.*

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

## Project Structure

```
three-blind-mice/
├── web/               # React SPA (remote + host UI)
├── api/               # Azure Functions (serverless)
├── mouse-receiver/    # C# .NET 8 headless overlay (Win32/X11)
├── infra/             # Bicep + deploy script
└── docs/              # Documentation + plan
```

## License

MIT
