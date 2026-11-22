using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace EventCheckIn.Server;

/// <summary>
/// Admin dashboard + event setup. Only reachable from the host machine itself (loopback),
/// so kiosks and other devices on the network can't browse attendee data or change the event.
/// </summary>
public static partial class DashboardEndpoints
{
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [GeneratedRegex("^[a-f0-9]{32}$")] private static partial Regex IdPattern();
    [GeneratedRegex(@"^(photo\.jpg|signature\.png|qrcode\.png|doc_\d{1,3}\.jpg)$")] private static partial Regex FilePattern();

    public static void Map(WebApplication app)
    {
        var admin = app.MapGroup("").AddEndpointFilter(async (ctx, next) =>
        {
            var http = ctx.HttpContext;
            var ip = http.Connection.RemoteIpAddress;
            if (ip is null || !IPAddress.IsLoopback(ip))
                return Results.Text("The dashboard is only available on the host computer.", statusCode: 403);

            // Block DNS-rebinding: the browser must be addressing us as localhost.
            var host = http.Request.Host.Host;
            if (host is not ("localhost" or "127.0.0.1" or "[::1]" or "::1"))
                return Results.Text("Open the dashboard at http://localhost.", statusCode: 403);

            // Block cross-site form posts / fetches from other web pages open in the host's browser.
            if (!HttpMethods.IsGet(http.Request.Method))
            {
                var origin = http.Request.Headers.Origin.ToString();
                if (origin.Length > 0 && Uri.TryCreate(origin, UriKind.Absolute, out var o) &&
                    !(o.Host is "localhost" or "127.0.0.1" or "[::1]" or "::1" && o.Port == http.Connection.LocalPort))
                    return Results.Text("Cross-site request blocked.", statusCode: 403);
            }
            return await next(ctx);
        });

        admin.MapGet("/", (Store store, PairingService pairing, HotspotService hotspot, EventService events, ServerOptions o) =>
            Results.Content(RenderDashboard(store, pairing, hotspot, events, o), "text/html; charset=utf-8"));

        admin.MapPost("/admin/rotate-pin", (PairingService p) => { p.RotatePin(); return Results.Redirect("/"); })
             .DisableAntiforgery();
        admin.MapPost("/admin/unpair-all", (Store s) => { s.RemoveAllKiosks(); return Results.Redirect("/"); })
             .DisableAntiforgery();
        admin.MapPost("/admin/kiosk-settings", async (HttpRequest req, Store s) =>
        {
            var form = await req.ReadFormAsync();
            bool show = form["show"] == "1";
            string id = form["id"].ToString();
            if (id == "all") s.SetAllKiosksShowSettings(show);
            else if (!string.IsNullOrEmpty(id)) s.SetKioskShowSettings(id, show);
            return Results.Redirect("/");
        }).DisableAntiforgery();
        admin.MapPost("/admin/kiosk-mode", async (HttpRequest req, Store s) =>
        {
            var form = await req.ReadFormAsync();
            string mode = form["mode"].ToString(); // "kiosk_locked", "admin", "prompt"
            string id = form["id"].ToString();
            if (!string.IsNullOrEmpty(id))
            {
                if (mode == "admin") s.SetKioskAdminMode(id, isAdmin: true, modeLocked: false);
                else if (mode == "prompt") s.SetKioskAdminMode(id, isAdmin: false, modeLocked: false);
                else s.SetKioskAdminMode(id, isAdmin: false, modeLocked: true);
            }
            return Results.Redirect("/");
        }).DisableAntiforgery();
        admin.MapPost("/admin/kiosk-role", async (HttpRequest req, Store s) =>
        {
            var form = await req.ReadFormAsync();
            string id = form["id"].ToString();
            string roleId = form["roleId"].ToString();
            if (!string.IsNullOrEmpty(id)) s.SetKioskAdminRole(id, string.IsNullOrEmpty(roleId) ? null : roleId);
            return Results.Redirect("/");
        }).DisableAntiforgery();

        // ----- Admin Users (SQLite) -----
        admin.MapPost("/admin/users/create", async (HttpRequest req, Store s) =>
        {
            var form = await req.ReadFormAsync();
            var username = form["username"].ToString();
            var displayName = form["displayName"].ToString();
            var accessLevelId = form["accessLevelId"].ToString();
            var pin = form["pin"].ToString();
            var mustReset = form["mustResetPin"] == "1" || string.IsNullOrWhiteSpace(pin);
            s.CreateAdminUser(username, displayName, accessLevelId, pin, mustReset);
            return Results.Redirect("/");
        }).DisableAntiforgery();

        admin.MapPost("/admin/users/access", async (HttpRequest req, Store s) =>
        {
            var form = await req.ReadFormAsync();
            var id = form["id"].ToString();
            var accessLevelId = form["accessLevelId"].ToString();
            if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(accessLevelId))
                s.SetAdminUserAccessLevel(id, accessLevelId);
            return Results.Redirect("/");
        }).DisableAntiforgery();

        admin.MapPost("/admin/users/lock", async (HttpRequest req, Store s) =>
        {
            var form = await req.ReadFormAsync();
            var id = form["id"].ToString();
            bool locked = form["locked"] == "1";
            if (!string.IsNullOrEmpty(id)) s.SetAdminUserLocked(id, locked);
            return Results.Redirect("/");
        }).DisableAntiforgery();

        admin.MapPost("/admin/users/reset-pin", async (HttpRequest req, Store s) =>
        {
            var form = await req.ReadFormAsync();
            var id = form["id"].ToString();
            var tempPin = form["tempPin"].ToString();
            if (!string.IsNullOrEmpty(id)) s.ForceAdminUserPinReset(id, tempPin);
            return Results.Redirect("/");
        }).DisableAntiforgery();

        admin.MapPost("/admin/users/delete", async (HttpRequest req, Store s) =>
        {
            var form = await req.ReadFormAsync();
            var id = form["id"].ToString();
            if (!string.IsNullOrEmpty(id)) s.DeleteAdminUser(id);
            return Results.Redirect("/");
        }).DisableAntiforgery();

        // ----- Event setup -----
        admin.MapGet("/event", () => Results.Content(EventEditorPage.Html, "text/html; charset=utf-8"));
        admin.MapGet("/admin/event", (EventService ev) => Results.Json(ev.Get(), Json));
        admin.MapPost("/admin/event", async (HttpRequest req, EventService ev) =>
        {
            EventConfig? cfg;
            try { cfg = await JsonSerializer.DeserializeAsync<EventConfig>(req.Body, Json); }
            catch (JsonException ex) { return Results.Json(new { errors = new[] { "Invalid data: " + ex.Message } }, statusCode: 400); }
            if (cfg is null) return Results.Json(new { errors = new[] { "No data." } }, statusCode: 400);
            var (ok, errors, saved) = ev.Save(cfg);
            return ok ? Results.Json(saved, Json) : Results.Json(new { errors }, statusCode: 400);
        }).DisableAntiforgery();
        admin.MapPost("/admin/event/logo", async (HttpRequest req, EventService ev) =>
        {
            if (!req.HasFormContentType) return Results.BadRequest(new { error = "No file." });
            var file = (await req.ReadFormAsync()).Files.GetFile("logo");
            if (file is null) return Results.BadRequest(new { error = "No file." });
            var error = await ev.SaveLogoAsync(file);
            return error is null ? Results.Ok(new { ok = true }) : Results.BadRequest(new { error });
        }).DisableAntiforgery();
        admin.MapPost("/admin/event/logo/delete", (EventService ev) => { ev.DeleteLogo(); return Results.Ok(new { ok = true }); })
             .DisableAntiforgery();
        admin.MapGet("/admin/event/logo", (EventService ev) =>
            ev.LogoPath() is { } p ? Results.File(p, p.EndsWith(".png") ? "image/png" : "image/jpeg") : Results.NotFound());

        // ----- Files & export -----
        admin.MapGet("/files/{id}/{name}", (string id, string name, Store store) =>
        {
            if (!IdPattern().IsMatch(id) || !FilePattern().IsMatch(name)) return Results.NotFound();
            var path = Path.Combine(store.FolderFor(id), name);
            if (!File.Exists(path) && name == "qrcode.png" && store.Exists(id))
                ApiEndpoints.EnsureQrCode(store.FolderFor(id), id);
            return File.Exists(path)
                ? Results.File(path, name.EndsWith(".png") ? "image/png" : "image/jpeg")
                : Results.NotFound();
        });

        admin.MapGet("/export.csv", (Store store) =>
        {
            var all = store.GetCheckIns();
            // One column per question label, in the order they first appear.
            var labels = all.SelectMany(c => c.Answers ?? new()).Select(a => a.Label).Distinct().ToList();

            var sb = new StringBuilder();
            sb.AppendJoin(',', new[] { "Id", "Name", "Event", "CheckedInAt", "Kiosk" }.Concat(labels)
                .Concat(["Documents", "Folder"]).Select(Csv)).Append('\n');
            foreach (var c in all)
            {
                var answers = (c.Answers ?? new()).GroupBy(a => a.Label).ToDictionary(g => g.Key, g => string.Join("; ", g.First().Values));
                var docs = c.DocumentFiles.Select((f, i) => (c.DocumentNames?.ElementAtOrDefault(i) ?? $"Document {i + 1}") + "=" + f);
                sb.AppendJoin(',', new[] { c.Id, c.FullName, c.EventName, c.CheckedInAt.ToString("yyyy-MM-dd HH:mm:ss"), c.KioskName ?? "" }
                    .Concat(labels.Select(l => answers.GetValueOrDefault(l, "")))
                    .Concat([string.Join("; ", docs), store.FolderFor(c.Id)]).Select(Csv)).Append('\n');
            }
            return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray(),
                "text/csv", $"checkins-{DateTime.Now:yyyyMMdd-HHmm}.csv");
        });
    }

    static string Csv(string? v)
    {
        v ??= "";
        if (v.Length > 0 && "=+-@\t\r".Contains(v[0])) v = "'" + v; // block spreadsheet formula injection
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }

    static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

    static string RenderDashboard(Store store, PairingService pairing, HotspotService hotspot, EventService events, ServerOptions o)
    {
        var checkins = store.GetCheckIns();
        var kiosks = store.GetKiosks();
        var adminUsers = store.GetAdminUsers();
        var ev = events.Get();
        var addresses = NetworkInfo.LanAddresses().Select(a => $"http://{a}:{o.Port}");

        var rows = new StringBuilder();
        foreach (var c in checkins)
        {
            var photo = c.PhotoFile is null ? "<div class=nophoto>no photo</div>"
                : $"<a href='/files/{c.Id}/{c.PhotoFile}' target=_blank><img class=photo src='/files/{c.Id}/{c.PhotoFile}'></a>";
            var docs = string.Join(" ", c.DocumentFiles.Select((d, i) =>
            {
                var name = H(c.DocumentNames?.ElementAtOrDefault(i) ?? $"Document {i + 1}");
                return $"<a href='/files/{c.Id}/{d}' target=_blank class=docwrap><img class=doc src='/files/{c.Id}/{d}' title='{name}'><br><small>{name}</small></a>";
            }));
            var answers = string.Join("", (c.Answers ?? new()).Select(a =>
                $"<div><small>{H(a.Label)}:</small> {H(string.Join(", ", a.Values))}</div>"));
            if (!string.IsNullOrEmpty(c.Email) && !(c.Answers?.Any() ?? false)) answers = $"<small>{H(c.Email)}</small>";
            var sig = c.SignatureFile is null ? "" : $"<img class=sig src='/files/{c.Id}/{c.SignatureFile}'>";
            var qrFile = c.QrCodeFile ?? "qrcode.png";
            var qr = $"<a href='/files/{c.Id}/{qrFile}' target=_blank><img class=doc src='/files/{c.Id}/{qrFile}' title='QR Code'></a>";
            rows.Append($"""
                <tr><td>{photo}</td>
                <td><b>{H(c.FullName)}</b>{answers}</td>
                <td>{H(c.EventName)}</td><td>{c.CheckedInAt:g}<br><small>{H(c.KioskName)}</small></td>
                <td>{docs}</td><td>{sig}</td><td>{qr}</td></tr>
                """);
        }

        var kioskList = kiosks.Count == 0 ? "<i>none yet</i>" : string.Join("<div style='margin:6px 0'></div>", kiosks.Select(k =>
        {
            var toggleText = k.ShowSettings ? "Hide settings" : "Show settings";
            var toggleVal = k.ShowSettings ? "0" : "1";
            var toggleClass = k.ShowSettings ? "secondary" : "";
            var roleBadge = k.IsAdmin
                ? "<b style='color:#512BD4'>[Admin]</b> "
                : k.ModeLocked
                    ? "<b style='color:#16a34a'>[Check-In Locked]</b> "
                    : "<small>[Prompt for mode]</small> ";
            return $"""
                <div style='display:flex;align-items:center;justify-content:space-between;gap:8px;flex-wrap:wrap'>
                  <span>{roleBadge}{H(k.Name)} <small>(last seen {(k.LastSeen is { } t ? t.ToString("g") : "never")})</small></span>
                  <span style='display:flex;gap:6px;align-items:center;flex-wrap:wrap'>
                    <form method=post action=/admin/kiosk-mode style='margin:0;display:inline-flex;align-items:center;gap:4px'>
                      <input type=hidden name=id value='{H(k.Id)}'>
                      <small>Mode:</small>
                      <select name=mode onchange='this.form.submit()' style='font-size:12px;padding:3px 6px;border-radius:6px;border:1px solid #ccc'>
                        <option value='kiosk_locked' {(!k.IsAdmin && k.ModeLocked ? "selected" : "")}>Check-In Kiosk (Locked)</option>
                        <option value='admin' {(k.IsAdmin ? "selected" : "")}>Admin Mode (Requires Sign-In)</option>
                        <option value='prompt' {(!k.IsAdmin && !k.ModeLocked ? "selected" : "")}>Prompt on Tablet</option>
                      </select>
                    </form>
                    <form method=post action=/admin/kiosk-settings style='margin:0'>
                      <input type=hidden name=id value='{H(k.Id)}'>
                      <input type=hidden name=show value='{toggleVal}'>
                      <button class='{toggleClass}' style='padding:4px 10px;font-size:12px'>{toggleText}</button>
                    </form>
                  </span>
                </div>
                """;
        }));
        var roleOptionsHtml = string.Join("", ev.AdminRoles.Select(r => $"<option value='{H(r.Id)}'>{H(r.Name)}</option>"));
        var adminUserRows = adminUsers.Count == 0
            ? "<tr><td colspan=6><i>No admin users yet. Create one below so admins can sign in on Admin tablets.</i></td></tr>"
            : string.Join("", adminUsers.Select(u =>
            {
                var statusBadge = u.IsLocked
                    ? "<b style='color:#c33'>🔒 Locked out</b>"
                    : u.MustResetPin
                        ? "<b style='color:#d97706'>⚠ Must reset PIN on login</b>"
                        : "<span style='color:#16a34a'>✓ Active</span>";
                var roleSelect = $"""
                    <form method=post action=/admin/users/access style='margin:0'>
                      <input type=hidden name=id value='{H(u.Id)}'>
                      <select name=accessLevelId onchange='this.form.submit()' style='font-size:13px;padding:4px 8px;border-radius:6px;border:1px solid #ccc'>
                        {string.Join("", ev.AdminRoles.Select(r => $"<option value='{H(r.Id)}' {(r.Id == u.AccessLevelId ? "selected" : "")}>{H(r.Name)}</option>"))}
                      </select>
                    </form>
                    """;
                var lockBtn = u.IsLocked
                    ? $"<form method=post action=/admin/users/lock style='margin:0'><input type=hidden name=id value='{H(u.Id)}'><input type=hidden name=locked value='0'><button style='padding:4px 10px;font-size:12px'>🔓 Unlock</button></form>"
                    : $"<form method=post action=/admin/users/lock style='margin:0'><input type=hidden name=id value='{H(u.Id)}'><input type=hidden name=locked value='1'><button class='secondary' style='padding:4px 10px;font-size:12px'>🔒 Lock out</button></form>";
                return $"""
                    <tr>
                      <td><b>{H(u.DisplayName)}</b><br><small>@{H(u.Username)}</small></td>
                      <td>{roleSelect}</td>
                      <td>{statusBadge}</td>
                      <td><small>{(u.LastLoginAt is { } t ? t.ToString("g") : "never")}</small></td>
                      <td>
                        <form method=post action=/admin/users/reset-pin style='margin:0;display:flex;gap:4px;align-items:center'>
                          <input type=hidden name=id value='{H(u.Id)}'>
                          <input type=password name=tempPin placeholder='Optional temp PIN' style='font-size:12px;padding:4px 6px;width:120px;border:1px solid #ccc;border-radius:6px'>
                          <button class='secondary' style='padding:4px 10px;font-size:12px'>Force PIN Reset</button>
                        </form>
                      </td>
                      <td>
                        <div style='display:flex;gap:6px'>
                          {lockBtn}
                          <form method=post action=/admin/users/delete style='margin:0' onsubmit="return confirm('Delete admin user {H(u.Username)}?')">
                            <input type=hidden name=id value='{H(u.Id)}'>
                            <button class='danger' style='padding:4px 10px;font-size:12px'>Delete</button>
                          </form>
                        </div>
                      </td>
                    </tr>
                    """;
            }));

        var logo = ev.HasLogo ? "<img src='/admin/event/logo' style='max-height:60px;max-width:200px'>" : "";

        return $$"""
            <!doctype html><html><head><meta charset=utf-8>
            <title>EventCheckIn Host – {{H(o.Name)}}</title>
            <style>{{SharedCss}}
              .pin{font-size:42px;letter-spacing:6px;font-weight:700;color:#512BD4}
              table{width:100%;border-collapse:collapse;background:#fff;border-radius:12px;overflow:hidden}
              th,td{padding:10px;border-bottom:1px solid #eee;text-align:left;vertical-align:middle}
              .photo{width:64px;height:64px;object-fit:cover;border-radius:50%}
              .docwrap{display:inline-block;text-align:center;margin-right:6px;text-decoration:none;color:inherit}
              .doc{height:56px;border:1px solid #ddd;border-radius:4px}
              .sig{height:48px;background:#fff}
              .nophoto{width:64px;height:64px;border-radius:50%;background:#ddd;font-size:10px;display:flex;align-items:center;justify-content:center}
            </style>
            <script>
              // Auto-refresh the dashboard every 15s ONLY when the user isn't typing or interacting with a form field.
              setInterval(() => {
                const a = document.activeElement;
                if (a && ['INPUT', 'SELECT', 'TEXTAREA'].includes(a.tagName)) return;
                const hasText = Array.from(document.querySelectorAll('input[type=text], input:not([type]), input[type=password]'))
                                     .some(i => i.value.trim().length > 0);
                if (!hasText) location.reload();
              }, 15000);
            </script></head><body>
            {{Nav("dashboard")}}
            <div class=cards>
              <div class=card><div><b>Current event</b></div>{{logo}}<div style="font-size:20px;margin:6px 0">{{H(ev.EventName)}}</div>
                <small>{{ev.Fields.Count}} question(s) · {{ev.Documents.Count}} document(s) · version {{ev.Version}}</small><br><br>
                <a class=btn href=/event>Edit event</a></div>
              <div class=card><div>Pairing PIN</div><div class=pin>{{pairing.Pin}}</div>
                <form method=post action=/admin/rotate-pin><button>New PIN</button></form></div>
              <div class=card><div><b>Kiosk network</b></div>
                <div>Wi-Fi: <b>{{H(hotspot.Ssid)}}</b><br>Password: <b>{{H(hotspot.Password)}}</b></div>
                <p><small>{{H(hotspot.Status)}}</small></p>
                <div><small>Server: {{H(string.Join(", ", addresses))}}</small></div></div>
              <div class=card><div><b>Paired kiosks ({{kiosks.Count}})</b></div><div style="margin:10px 0">{{kioskList}}</div>
                <div style="display:flex;gap:8px;flex-wrap:wrap">
                  <form method=post action=/admin/kiosk-settings style="margin:0"><input type=hidden name=id value=all><input type=hidden name=show value=1><button>Show settings on all</button></form>
                  <form method=post action=/admin/kiosk-settings style="margin:0"><input type=hidden name=id value=all><input type=hidden name=show value=0><button class=secondary>Hide settings on all</button></form>
                  <form method=post action=/admin/unpair-all style="margin:0" onsubmit="return confirm('Unpair all kiosks? They will need the PIN again.')"><button class=danger>Unpair all</button></form>
                </div></div>
              <div class=card><div><b>{{checkins.Count}} check-in(s)</b></div>
                <p><small>Data folder:<br>{{H(store.DataDir)}}</small></p><a class=btn href=/export.csv>Export CSV</a></div>
            </div>

            <div class=card style="margin:0 24px 20px">
              <div style="display:flex;justify-content:space-between;align-items:center;flex-wrap:wrap;gap:10px;margin-bottom:10px">
                <div><b>Admin users ({{adminUsers.Count}})</b> — <small>Stored in SQLite (<code>admin_users</code>). Admins authenticate with their username &amp; PIN on any Admin tablet.</small></div>
                <a class=btn href="/event#roles" style="font-size:12px;padding:5px 10px">Configure Access Levels</a>
              </div>
              <table style="margin:0 0 12px;border:1px solid #eee">
                <tr><th>Admin User</th><th>Access Level</th><th>Account Status</th><th>Last Login</th><th>Forgotten PIN / Reset</th><th>Actions</th></tr>
                {{adminUserRows}}
              </table>
              <form method=post action=/admin/users/create style="display:flex;gap:10px;flex-wrap:wrap;align-items:flex-end;background:#f7f5ff;padding:12px;border-radius:8px">
                <label style="display:flex;flex-direction:column;font-size:12px;gap:3px">Username *
                  <input name=username required maxlength=40 placeholder="e.g. jsmith" style="padding:6px 8px;border:1px solid #ccc;border-radius:6px;font-size:13px"></label>
                <label style="display:flex;flex-direction:column;font-size:12px;gap:3px">Full / Display Name
                  <input name=displayName maxlength=80 placeholder="e.g. Jane Smith" style="padding:6px 8px;border:1px solid #ccc;border-radius:6px;font-size:13px"></label>
                <label style="display:flex;flex-direction:column;font-size:12px;gap:3px">Access Level
                  <select name=accessLevelId style="padding:6px 8px;border:1px solid #ccc;border-radius:6px;font-size:13px">{{roleOptionsHtml}}</select></label>
                <label style="display:flex;flex-direction:column;font-size:12px;gap:3px">Initial PIN (leave blank to set on first login)
                  <input type=password name=pin maxlength=40 placeholder="PIN / password" style="padding:6px 8px;border:1px solid #ccc;border-radius:6px;font-size:13px"></label>
                <label style="display:flex;align-items:center;gap:5px;font-size:13px;padding-bottom:6px">
                  <input type=checkbox name=mustResetPin value=1> Require PIN change on next login</label>
                <button type=submit>+ Create Admin User</button>
              </form>
            </div>

            <table><tr><th>Photo</th><th>Attendee</th><th>Event</th><th>Checked in</th><th>Documents</th><th>Signature</th><th>QR Code</th></tr>
            {{(rows.Length == 0 ? "<tr><td colspan=7><i>No check-ins yet.</i></td></tr>" : rows.ToString())}}
            </table></body></html>
            """;
    }

    public const string SharedCss = """
        body{font-family:system-ui,sans-serif;margin:0;background:#f6f6f9;color:#222}
        main,.cards,table{margin:0 24px}
        nav{background:#512BD4;color:#fff;padding:12px 24px;display:flex;gap:20px;align-items:center;margin-bottom:20px}
        nav a{color:#fff;text-decoration:none;opacity:.8} nav a.on{opacity:1;font-weight:700;border-bottom:2px solid #fff}
        nav b{margin-right:20px}
        .cards{display:flex;gap:16px;flex-wrap:wrap;margin-bottom:20px}
        .card{background:#fff;border-radius:12px;padding:16px 20px;box-shadow:0 1px 4px #0002;min-width:240px}
        button,a.btn{background:#512BD4;color:#fff;border:0;border-radius:8px;padding:8px 14px;cursor:pointer;text-decoration:none;font-size:14px;display:inline-block}
        button.secondary{background:#888} button.danger{background:#c33}
        small{color:#777}
        """;

    public static string Nav(string current) => $"""
        <nav><b>EventCheckIn Host</b>
          <a href="/" class="{(current == "dashboard" ? "on" : "")}">Check-ins</a>
          <a href="/event" class="{(current == "event" ? "on" : "")}">Event setup</a></nav>
        """;
}
