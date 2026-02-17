# Copilot Instructions — Three Blind Mice

## Project Overview

Three Blind Mice is a hybrid web + native desktop application for drawing remote mouse pointers on a host's screen during screen-sharing sessions. Remote users use a browser; the host runs a lightweight native overlay.

## Architecture

- `web/` — React + TypeScript + Vite SPA, hosted on Azure Static Web App
- `api/` — Azure Functions (Node.js) for WebSocket token negotiation
- `mouse-receiver/` — C# .NET 8 headless overlay apps (no UI framework)
  - `ThreeBlindMice.Core/` — Shared class library (WebSocket, protocol, state)
  - `ThreeBlindMice.Windows/` — Win32 P/Invoke overlay (GDI+ rendering)
  - `ThreeBlindMice.Linux/` — X11 P/Invoke overlay (Xlib rendering)
- `infra/` — Bicep IaC + PowerShell deploy script

## Naming Conventions

- Use `snake_case` for fields, member variables, and local variables
- Prefix class members with `m_`
- Do NOT use `camelCase`
- Use `PascalCase` for class names, method names, and public properties

## Coding Style

- Prefer `var`/`auto` over explicit types except when a type change is deliberate
- Use tabs for indentation in C++ and C#
- Add comments that explain "why", not "what"
- Prefer a blank line before comment lines or blocks
- Avoid code duplication

## Key Design Decisions

- Mouse-receiver has NO UI framework (no WPF, no Avalonia) — uses direct P/Invoke for smallest binary size
- WebSocket + JSON use only built-in .NET 8 APIs (zero NuGet dependencies in Core)
- All received WebSocket data must be strictly validated (see Security Threat Model in docs/plan.md)
- `tbm://` protocol handler URI must be validated with strict regex `^[a-zA-Z0-9]{4,8}$`
- The negotiate API endpoint URL is hardcoded — never configurable from external input

## Build Commands

```powershell
# Web app
cd web && npm install && npm run dev

# Mouse-receiver (Windows)
cd mouse-receiver && dotnet build ThreeBlindMice.Windows/ThreeBlindMice.Windows.csproj

# Mouse-receiver (Linux)
cd mouse-receiver && dotnet build ThreeBlindMice.Linux/ThreeBlindMice.Linux.csproj

# Infrastructure
./infra/deploy.ps1 -ResourceGroup rg-three-blind-mice -Location australiaeast
```
