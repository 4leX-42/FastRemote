<p align="center"><img src="assets/logo-256.png" width="112" alt="FastRemote logo"></p>

<h1 align="center">FastRemote</h1>

<p align="center">A fast Remote Desktop launcher for Windows. Your RDP connections grouped by site in one window,
each one opened in its own native <code>mstsc</code> window.</p>

<p align="center"><img src="assets/screenshot.png" alt="FastRemote main window" width="860"></p>

[Castellano](#castellano)

## Why

mRemoteNG and similar tools embed every session in tabs. FastRemote only organises, checks and launches: each
connection opens in the real Remote Desktop client, with everything Windows already does well (multi-monitor,
full screen, clipboard, smart sizing).

## Features

- **Desktop layout**: menu bar, toolbar, session tabs, sidebar, table grouped by site, details panel and a status bar.
- **Sidebar**: favourites, all connections, active sessions, recent, unreachable, and the sites as a tree with
  their servers. Sites are reordered by dragging (or Alt+arrows).
- **Availability**: a TCP connection to the RDP port of every connection when the app starts and every 5 minutes,
  one at a time and 250 ms apart, plus the selected connection every 60 seconds. No credentials are sent. It can
  be turned off in *Settings > Availability*; some EDR products may report repeated connections to port 3389.
- **Sessions from the real windows**: connecting, authenticating, connected or interrupted, read from the `mstsc`
  windows. Each open session gets a tab with its duration; `mstsc` windows that were already open are picked up.
  Connect on a server with an open session brings that window forward.
- **Search** with Ctrl+K by name, host, site, user, tags or description (all words, accent-insensitive), and a
  command palette with Ctrl+Shift+P.
- **Credentials** stored where `mstsc` reads them (`TERMSRV/<host>` in Windows Credential Manager). In the
  connection editor: the server's own credential, the same credential as another server (copied without turning
  the password into text), or none. A tag shows when a credential is shared and the editor lists the other
  connections it affects. Bulk credential for all, one site, or every connection with the same user. Passwords
  are never written to disk by FastRemote.
- **Import from mRemoteNG** (`confCons.xml`): RDP connections, folders, inheritance, and optionally the saved
  passwords (AES-GCM, default or custom master password) copied to Credential Manager. Re-importing keeps your
  local names, folders and options.
- **Advanced RDP options** per connection with per-site defaults: resolution and smart sizing, colour depth,
  multi-monitor, drives, printers, ports, smart cards, clipboard, audio, Windows keys, visual effects, admin
  session, NLA, server authentication level, RD Gateway, load balance info. Plain connections launch with
  `mstsc /v:host`; advanced ones through a generated `.rdp` file (no password inside).
- **Settings**: themes (Carbon, Graphite, Light, High contrast), accent colour, text size, density, columns,
  language (English, Spanish). Every text/background pair meets WCAG AA contrast.
- Single small `.exe` for .NET Framework 4.8, which ships with Windows 10 and 11. Nothing to install.

**No telemetry, no analytics, no update checks.** The only network traffic is the port checks described above
and the Remote Desktop sessions you open.

## Install

Download `FastRemote.exe` from the [latest release](https://github.com/4leX-42/FastRemote/releases/latest) and run it.
Check the SHA-256 against the `.sha256` file of the release.

Data lives in `%APPDATA%\FastRemote\` (`connections.json`, `settings.json`, `history.json`). Generated `.rdp` files
go to `%LOCALAPPDATA%\FastRemote\rdp\` and are cleaned at startup. Data from earlier versions (Quaygate, RdpDeck)
is copied over on the first start.

## Keyboard

| Key | Action |
|---|---|
| Enter / Ctrl+Enter / Shift+Enter | connect / admin session (`/admin`) / full screen |
| Ctrl+K, Ctrl+Shift+P | search, command palette |
| F5 | check all connections now |
| Ctrl+N, F2, Del | new, edit, delete |
| Ctrl+D, Ctrl+C | duplicate, copy host |
| Ctrl+Shift+F | add to or remove from favourites |

## Saved credentials on a PC outside the domain

If `mstsc` says your administrator does not allow saved credentials, enable the policy *Allow delegating saved
credentials with NTLM-only server authentication* for `TERMSRV/*` (Computer Configuration > Administrative
Templates > System > Credentials Delegation).

## Build

```powershell
.\build.ps1                                     # dist\FastRemote.exe
dotnet test tests\FastRemote.Tests              # unit, scenario and rendering tests
powershell -File tests\Test-FastRemoteUi.ps1    # UI Automation on an isolated instance
```

Requires the .NET SDK (8 or later). `assets\Make-Logo.ps1` redraws the logo and the icon.

## License

MIT. FastRemote is not affiliated with mRemoteNG or Microsoft.

---

## Castellano

Lanzador de Escritorio remoto para Windows. Agrupa tus conexiones RDP por sedes y abre cada una en su propia
ventana de `mstsc`.

- Menú, barra de herramientas, pestañas de sesión, árbol de sedes con sus servidores, tabla agrupada por sede,
  panel de detalles y barra de estado.
- Disponibilidad de cada conexión al arrancar y cada 5 minutos: una conexión TCP al puerto RDP, de una en una y
  sin enviar credenciales. Se desactiva en *Ajustes > Disponibilidad*.
- Estado real de cada sesión (conectando, conectada, interrumpida), leído de las ventanas de `mstsc`.
- Búsqueda con Ctrl+K y paleta de comandos con Ctrl+Shift+P.
- Credencial por conexión desde el editor: la propia del servidor, la misma que otro servidor o ninguna. Avisa
  si está compartida. También hay una opción para poner la misma credencial a varias conexiones a la vez.
- Importa de mRemoteNG las conexiones, las carpetas y, si quieres, las contraseñas, que pasan al Administrador
  de credenciales de Windows.
- Opciones avanzadas de RDP con valores por defecto por sede.
- Temas, color de acento, tamaño de letra y densidad, en castellano e inglés.
- Sin telemetría ni comprobación de actualizaciones.

Descarga `FastRemote.exe` de la [última versión](https://github.com/4leX-42/FastRemote/releases/latest). Los datos
se guardan en `%APPDATA%\FastRemote\`.
