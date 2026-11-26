<p align="center"><img src="assets/logo-256.png" width="112" alt="Quaygate logo"></p>

<h1 align="center">Quaygate</h1>

<p align="center">A fast, minimal Remote Desktop launcher for Windows. Your RDP connections in one clean window,
each one opened in its own native <code>mstsc</code> window.</p>

<p align="center"><img src="assets/screenshot.png" alt="Quaygate main window" width="860"></p>

[Castellano](#castellano)

## Why

mRemoteNG and similar tools embed every session in tabs. Quaygate does the opposite: it only organises and
launches. Each connection opens in the real Remote Desktop client, with everything Windows already does well
(multi-monitor, full screen, clipboard, smart sizing).

## Features

- **Sites sidebar** with counts, drag and drop to reorder, rename a site, "All" at the bottom.
- **Instant search** by name, host, site, user or description (all words, accent-insensitive). Ctrl+F, arrows,
  Enter to connect, Esc to clear.
- **Import from mRemoteNG** (`confCons.xml`): RDP connections, folders, inheritance, and optionally the saved
  passwords (AES-GCM, default or custom master password) copied to Windows Credential Manager.
  Re-importing keeps your local names, folders and options.
- **Credentials** stored where `mstsc` reads them (`TERMSRV/<host>` in Credential Manager), per connection or
  for many at once (all, one site, or every connection with the same user). Passwords are never written to disk
  by Quaygate.
- **Advanced RDP options** per connection with per-site defaults: resolution and smart sizing, colour depth,
  multi-monitor, drives, printers, ports, smart cards, clipboard, audio, Windows keys, visual effects, console
  session, NLA, server authentication level, RD Gateway, load balance info. Plain connections launch with
  `mstsc /v:host`; advanced ones through a generated `.rdp` file (no password inside).
- **Status**: one TCP check of the selected connection's port. Quaygate never scans or probes all your hosts.
- **Settings**: text size, themes (Carbon, Graphite, Light, High contrast) and a custom accent colour, site order,
  language (English, Spanish). Every text/background pair meets WCAG AA contrast.
- Single small `.exe` for .NET Framework 4.8, which ships with Windows 10 and 11. Nothing to install.

**No telemetry, no analytics, no update checks.** The only network traffic is that single port check and the
Remote Desktop sessions you open.

## Install

Download `Quaygate.exe` from the [latest release](https://github.com/4leX-42/Quaygate/releases/latest) and run it.
Check the SHA-256 against the `.sha256` file of the release.

Data lives in `%APPDATA%\Quaygate\` (`connections.json`, `settings.json`). Generated `.rdp` files go to
`%LOCALAPPDATA%\Quaygate\rdp\` and are cleaned at startup.

## Keyboard

| Key | Action |
|---|---|
| Enter / Ctrl+Enter / Shift+Enter | connect / console session (`/admin`) / full screen |
| Ctrl+F, arrows, Esc | search, move, clear |
| Ctrl+N, F2, Del | new, edit, delete |
| Ctrl+D, Ctrl+C | duplicate, copy host |

## Saved credentials on a PC outside the domain

If `mstsc` says your administrator does not allow saved credentials, enable the policy *Allow delegating saved
credentials with NTLM-only server authentication* for `TERMSRV/*` (Computer Configuration > Administrative
Templates > System > Credentials Delegation).

## Build

```powershell
.\build.ps1                                   # dist\Quaygate.exe
dotnet test tests\Quaygate.Tests              # unit and rendering tests
powershell -File tests\Test-QuaygateUi.ps1    # UI Automation on an isolated instance
```

Requires the .NET SDK (8 or later). `assets\Make-Logo.ps1` redraws the logo and the icon.

## License

MIT. Quaygate is not affiliated with mRemoteNG or Microsoft.

---

## Castellano

Lanzador de Escritorio remoto rápido y minimalista para Windows. Organiza tus conexiones RDP por sedes y abre
cada una en su propia ventana de `mstsc`.

- Barra de sedes que se ordena arrastrando, búsqueda instantánea y atajos de teclado.
- Importa de mRemoteNG las conexiones, las carpetas y, si quieres, las contraseñas, que pasan al Administrador
  de credenciales de Windows.
- Credencial por conexión o para muchas a la vez.
- Opciones avanzadas de RDP con valores por defecto por sede.
- Temas, color de acento y tamaño de letra, en castellano e inglés.
- Sin telemetría ni comprobación de actualizaciones. Solo comprueba el puerto de la conexión que seleccionas.

Descarga `Quaygate.exe` de la [última versión](https://github.com/4leX-42/Quaygate/releases/latest). Los datos
se guardan en `%APPDATA%\Quaygate\`.
