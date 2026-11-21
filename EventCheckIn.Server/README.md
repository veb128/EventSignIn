# EventCheckIn Host Server

A small, private backend for EventCheckIn kiosks. It runs on **Windows, macOS or Linux**
(including Raspberry Pi), keeps every check-in on the host machine, and never contacts the internet.

```
 ┌─────────┐   Wi-Fi (host's hotspot)   ┌──────────────────────────┐
 │ iPad    │ ─────────────────────────▶ │ Host computer            │
 │ Android │      HTTP :5080, LAN only  │  • EventCheckIn.Server   │
 │ Mac     │ ◀───────────────────────── │  • SQLite + image files  │
 └─────────┘                            │  • Dashboard (localhost) │
                                        └──────────────────────────┘
```

## Run it

Requires the .NET 11 SDK (or use a self-contained build, below).

```bash
cd EventCheckIn.Server
dotnet run
```

The console prints the **pairing PIN**, the **Wi-Fi name/password** and the kiosk address.
Open **http://localhost:5080** on the host for the dashboard (it's blocked for every other device).

## The kiosk network

| Host OS | What happens | Host address kiosks will find |
|---|---|---|
| **Windows 10/11** | Starts *Mobile Hotspot* automatically (SSID `EventCheckIn`). Windows needs a Wi-Fi adapter and at least one network profile. If it can't start, turn on Settings → Network & internet → Mobile hotspot manually. | 192.168.137.1 |
| **Linux / Raspberry Pi** | Starts a hotspot with NetworkManager (`nmcli`). May need `sudo`, or add your user to the `netdev` group. Set `Hotspot:Interface` (e.g. `wlan0`) if you have more than one Wi-Fi adapter. | 10.42.0.1 |
| **macOS** | Apple doesn't let apps start a hotspot. Either turn on **System Settings → General → Sharing → Internet Sharing** (to Wi-Fi), or put the Mac and kiosks on a **private travel router** that isn't connected to the internet. | 192.168.2.1 / router subnet |

The Wi-Fi password is generated once and saved, so kiosks only need to join once.
You can also set your own password in `appsettings.json`.

> Real "ad-hoc" (peer-to-peer) Wi-Fi isn't used because iPads and current Android/macOS can't join it.
> A hotspot gives the same result: a private network created by the host, with no internet.

## Event setup

Open **http://localhost:5080/event** on the host, or click **Event setup** in the dashboard header:

- **Event name, welcome message and logo** (PNG/JPEG, max 5 MB) are shown at the top of every kiosk.
- **Questions:** the attendee's full name is always asked. Add as many questions as you need:

  | Type | On the kiosk | Settings |
  |---|---|---|
  | Free text | Text box | Required, placeholder, min/max length, validation: *any*, *email*, *phone*, *number* (with min/max), or *custom pattern* (regex, with a live tester), custom error message |
  | Single choice | Radio buttons | Required, options (one per line) |
  | Multiple choice | Checkboxes | Required (at least one), options |
  | Dropdown | Picker list | Required, options |

- **Documents to scan:** list each document by name, with instructions, marked required or optional.
  The kiosk asks for them one by one. If the list is empty, the scanning step is skipped.
- **Photo and agreement:** turn the attendee photo on or off, and edit the text shown above the signature.

**Save & publish** checks the setup and bumps the version. Kiosks download the new event within about 20 seconds.
An attendee who is mid check-in finishes with the version they started on. Each kiosk keeps the last event it
downloaded, so it keeps working if the host goes offline.

## Pairing a kiosk

1. Join the kiosk to the host's Wi-Fi.
2. In the app: **⚙ Kiosk settings → Find hosts on this network**. You can also type the address.
3. Enter the 6-digit PIN from the dashboard → **Pair**.

The kiosk gets a private token and from then on syncs on its own. Check-ins are always saved on
the kiosk first, so check-in keeps working if the host is unreachable; they upload when it's back.
Use **New PIN** / **Unpair all** on the dashboard to control which kiosks can connect.

## Firewall

Allow incoming TCP **5080** on the host:
- **Windows:** accept the firewall prompt the first time, choosing *Private* networks.
- **macOS:** accept "allow incoming connections".
- **Linux (ufw):** `sudo ufw allow 5080/tcp`

## Settings (`appsettings.json`)

```json
"Server": {
  "Port": 5080,
  "Name": "",                 // shown to kiosks; defaults to the computer name
  "DataDirectory": "",        // defaults to the user's local app-data folder
  "Hotspot": { "Enabled": true, "Ssid": "EventCheckIn", "Password": "", "Interface": "" }
}
```
Any setting can also be passed on the command line, e.g. `dotnet run -- --Server:Port=6000`.

## Data & privacy

- **Database format: SQLite 3**, a single file named `eventcheckin.db` in `DataDirectory`. It holds:
  - `checkins`: one row per attendee. Answers are stored as JSON, and images are referenced by file name.
  - `kiosks`: paired devices, with hashed tokens.
  - `settings`: the event setup as JSON, plus the hotspot password.

  Images are plain files in `checkins/{id}/photo.jpg, doc_N.jpg, signature.png`, and the logo is `logo.png` or `logo.jpg`.
  You can open the database with any SQLite tool, such as DB Browser for SQLite or `sqlite3`.
  To back up, copy the whole `DataDirectory` while the server is stopped.
  The default location is `%LOCALAPPDATA%\EventCheckInServer` on Windows,
  `~/Library/Application Support/EventCheckInServer` on macOS, and `~/.local/share/EventCheckInServer` on Linux.
- The dashboard and file viewer only answer requests from the host itself.
- Kiosk tokens are stored hashed. Wrong PINs lock out that device for 2 minutes after 5 tries.
- Traffic is HTTP inside the WPA2-encrypted hotspot. Don't run the server on a shared or public network.
- **CSV export** is on the dashboard.

## Self-contained builds (no .NET install on the host)

```bash
dotnet publish -c Release -r win-x64     --self-contained -p:PublishSingleFile=true -o publish/win-x64
dotnet publish -c Release -r osx-arm64   --self-contained -p:PublishSingleFile=true -o publish/osx-arm64
dotnet publish -c Release -r linux-x64   --self-contained -p:PublishSingleFile=true -o publish/linux-x64
dotnet publish -c Release -r linux-arm64 --self-contained -p:PublishSingleFile=true -o publish/linux-arm64   # Raspberry Pi 4/5
```
Copy the folder, including `appsettings.json`, to the host and run `EventCheckIn.Server`
(`EventCheckIn.Server.exe` on Windows).
