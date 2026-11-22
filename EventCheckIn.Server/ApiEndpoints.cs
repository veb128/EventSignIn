using System.Text.Json;
using System.Text.RegularExpressions;
using QRCoder;

namespace EventCheckIn.Server;

/// <summary>JSON API used by the kiosks.</summary>
public static partial class ApiEndpoints
{
    public const string Product = "EventCheckIn";
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [GeneratedRegex("^[a-f0-9]{32}$")] private static partial Regex IdPattern();
    [GeneratedRegex(@"^(photo\.jpg|signature\.png|qrcode\.png|doc_\d{1,3}\.jpg)$")] private static partial Regex FilePattern();

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api");

        // Discovery probe – no auth, reveals nothing private.
        api.MapGet("/info", (ServerOptions o) => new { product = Product, name = o.Name, version = 1 });

        api.MapPost("/pair", (PairRequest req, PairingService pairing, HttpContext ctx) =>
        {
            var (ok, token, error) = pairing.TryPair(req.Pin, req.KioskName, req.IsAdmin, ctx.Connection.RemoteIpAddress);
            return ok ? Results.Ok(new { token, isAdmin = req.IsAdmin }) : Results.Json(new { error }, statusCode: 401);
        });

        var secured = api.MapGroup("").AddEndpointFilter(async (ctx, next) =>
        {
            var header = ctx.HttpContext.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..] : null;
            var store = ctx.HttpContext.RequestServices.GetRequiredService<Store>();
            var kiosk = token is null ? null : store.ValidateToken(token);
            if (kiosk is null) return Results.Json(new { error = "Kiosk is not paired." }, statusCode: 401);
            ctx.HttpContext.Items["kiosk"] = kiosk.Value.Name;
            ctx.HttpContext.Items["kioskHash"] = Store.Hash(token!);
            ctx.HttpContext.Items["showSettings"] = kiosk.Value.ShowSettings;
            ctx.HttpContext.Items["isAdmin"] = kiosk.Value.IsAdmin;
            ctx.HttpContext.Items["adminRoleId"] = kiosk.Value.AdminRoleId;
            ctx.HttpContext.Items["modeLocked"] = kiosk.Value.ModeLocked;
            return await next(ctx);
        });

        secured.MapGet("/ping", (HttpContext ctx, Store store, EventService events) =>
        {
            bool isAdmin = ctx.Items["isAdmin"] is true;
            bool modeLocked = ctx.Items["modeLocked"] is true;
            var kioskHash = ctx.Items["kioskHash"] as string ?? "";
            var sessionToken = ctx.Request.Headers["X-Admin-Session"].ToString();
            var user = isAdmin ? store.ValidateAdminSession(sessionToken, kioskHash) : null;
            var role = user is not null ? ResolveAdminRole(events.Get(), user.AccessLevelId) : null;
            return Results.Ok(new
            {
                ok = true,
                showSettings = ctx.Items["showSettings"] is true,
                isAdmin,
                modeLocked,
                adminAuthenticated = user is not null,
                adminUserName = user?.DisplayName ?? "",
                adminRoleName = role?.Name ?? ""
            });
        });

        secured.MapPost("/kiosk/select-mode", (SelectModeRequest req, HttpContext ctx, Store store) =>
        {
            var kioskHash = ctx.Items["kioskHash"] as string ?? "";
            bool modeLocked = ctx.Items["modeLocked"] is true;
            bool currentlyAdmin = ctx.Items["isAdmin"] is true;

            // Once locked in non-admin (check-in) mode, the kiosk cannot change modes on its own; only the host dashboard can unlock/change it.
            if (modeLocked && !currentlyAdmin && req.IsAdmin)
                return Results.Json(new { error = "This kiosk is locked in attendee check-in mode. Change its mode from the host dashboard." }, statusCode: 403);

            bool lockNow = !req.IsAdmin; // Starting in non-admin mode locks the kiosk in non-admin mode.
            store.SetKioskAdminMode(kioskHash, req.IsAdmin, lockNow);
            return Results.Ok(new { ok = true, isAdmin = req.IsAdmin, modeLocked = lockNow });
        });

        // ----- Admin User Authentication (SQLite) -----
        secured.MapPost("/admin/login", (AdminLoginRequest req, HttpContext ctx, Store store, EventService events) =>
        {
            var kioskHash = ctx.Items["kioskHash"] as string ?? "";
            bool modeLocked = ctx.Items["modeLocked"] is true;
            bool currentlyAdmin = ctx.Items["isAdmin"] is true;
            if (modeLocked && !currentlyAdmin)
                return Results.Json(new { error = "This kiosk is locked in attendee check-in mode. Change its mode from the host dashboard." }, statusCode: 403);

            var username = req.Username?.Trim() ?? "";
            var pin = req.Pin?.Trim() ?? "";
            if (string.IsNullOrEmpty(username))
                return Results.Json(new { error = "Enter your username." }, statusCode: 400);

            var user = store.GetAdminUserByUsername(username);
            if (user is null)
                return Results.Json(new { error = "Invalid username or PIN." }, statusCode: 401);

            if (user.IsLocked)
                return Results.Json(new { error = "Account is locked. Ask the host administrator to unlock your account or reset your PIN." }, statusCode: 403);

            // If a PIN is set on the user, verify it first.
            if (!string.IsNullOrEmpty(user.PinHash))
            {
                var suppliedHash = string.IsNullOrEmpty(pin) ? "" : Store.Hash(pin);
                if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(suppliedHash),
                        System.Text.Encoding.UTF8.GetBytes(user.PinHash)))
                {
                    store.RecordAdminLoginFailure(user.Id, maxAttempts: 5);
                    var updated = store.GetAdminUserById(user.Id);
                    var msg = updated?.IsLocked == true
                        ? "Too many failed attempts. Your account is now locked — contact the host administrator."
                        : "Invalid username or PIN.";
                    return Results.Json(new { error = msg }, statusCode: 401);
                }
            }

            if (user.MustResetPin || string.IsNullOrEmpty(user.PinHash))
            {
                return Results.Ok(new
                {
                    mustResetPin = true,
                    displayName = user.DisplayName
                });
            }

            store.SetKioskAdminMode(kioskHash, isAdmin: true, modeLocked: false);
            store.RecordAdminLoginSuccess(user.Id);
            var sessionToken = store.CreateAdminSession(user.Id, kioskHash);
            var role = ResolveAdminRole(events.Get(), user.AccessLevelId);
            return Results.Ok(new
            {
                mustResetPin = false,
                sessionToken,
                displayName = user.DisplayName,
                roleName = role?.Name ?? ""
            });
        });

        secured.MapPost("/admin/reset-pin", (AdminResetPinRequest req, HttpContext ctx, Store store, EventService events) =>
        {
            var kioskHash = ctx.Items["kioskHash"] as string ?? "";
            bool modeLocked = ctx.Items["modeLocked"] is true;
            bool currentlyAdmin = ctx.Items["isAdmin"] is true;
            if (modeLocked && !currentlyAdmin)
                return Results.Json(new { error = "This kiosk is locked in attendee check-in mode. Change its mode from the host dashboard." }, statusCode: 403);

            var username = req.Username?.Trim() ?? "";
            var currentPin = req.CurrentPin?.Trim() ?? "";
            var newPin = req.NewPin?.Trim() ?? "";
            if (string.IsNullOrEmpty(username))
                return Results.Json(new { error = "Username is required." }, statusCode: 400);
            if (newPin.Length < 4 || newPin.Length > 40)
                return Results.Json(new { error = "New PIN must be at least 4 characters." }, statusCode: 400);

            var user = store.GetAdminUserByUsername(username);
            if (user is null)
                return Results.Json(new { error = "Invalid username." }, statusCode: 401);
            if (user.IsLocked)
                return Results.Json(new { error = "Account is locked. Ask the host administrator to unlock your account." }, statusCode: 403);

            if (!string.IsNullOrEmpty(user.PinHash))
            {
                var suppliedHash = string.IsNullOrEmpty(currentPin) ? "" : Store.Hash(currentPin);
                if (!System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                        System.Text.Encoding.UTF8.GetBytes(suppliedHash),
                        System.Text.Encoding.UTF8.GetBytes(user.PinHash)))
                {
                    store.RecordAdminLoginFailure(user.Id, maxAttempts: 5);
                    return Results.Json(new { error = "Current/temporary PIN is incorrect." }, statusCode: 401);
                }
            }
            else if (!user.MustResetPin)
            {
                return Results.Json(new { error = "PIN reset is not enabled for this account." }, statusCode: 403);
            }

            store.UpdateAdminUserPin(user.Id, newPin);
            store.SetKioskAdminMode(kioskHash, isAdmin: true, modeLocked: false);
            store.RecordAdminLoginSuccess(user.Id);
            var sessionToken = store.CreateAdminSession(user.Id, kioskHash);
            var role = ResolveAdminRole(events.Get(), user.AccessLevelId);
            return Results.Ok(new
            {
                sessionToken,
                displayName = user.DisplayName,
                roleName = role?.Name ?? ""
            });
        });

        secured.MapPost("/admin/logout", (HttpContext ctx, Store store) =>
        {
            var sessionToken = ctx.Request.Headers["X-Admin-Session"].ToString();
            if (!string.IsNullOrWhiteSpace(sessionToken))
                store.DeleteAdminSession(sessionToken.Trim());
            return Results.Ok(new { ok = true });
        });

        // Admin tablets can view all check-ins and their files (filtered by the authenticated Admin User's access level).
        secured.MapGet("/checkins", (HttpContext ctx, Store store, EventService events) =>
        {
            if (ctx.Items["isAdmin"] is not true)
                return Results.Json(new { error = "Admin mode required." }, statusCode: 403);

            var kioskHash = ctx.Items["kioskHash"] as string ?? "";
            var sessionToken = ctx.Request.Headers["X-Admin-Session"].ToString();
            var user = store.ValidateAdminSession(sessionToken, kioskHash);
            if (user is null)
                return Results.Json(new { error = "Admin authentication required." }, statusCode: 401);

            var ev = events.Get();
            var role = ResolveAdminRole(ev, user.AccessLevelId);
            var checkins = store.GetCheckIns().Select(c =>
            {
                var accessList = c.DocumentFiles.Select((_, i) =>
                    GetDocumentAccess(ev, role, c.DocumentIds?.ElementAtOrDefault(i), c.DocumentNames?.ElementAtOrDefault(i))).ToList();
                return new
                {
                    c.Id, c.FullName, c.Email, c.EventName, c.CheckedInAt,
                    c.KioskName, c.PhotoFile, c.DocumentFiles, c.SignatureFile,
                    c.ReceivedAt, c.Answers, c.DocumentNames, c.EventVersion,
                    c.QrCodeFile, c.DocumentIds,
                    DocumentAccess = accessList,
                    AdminRoleName = $"{user.DisplayName} ({role?.Name ?? "Admin"})"
                };
            });
            return Results.Json(checkins, Json);
        });

        secured.MapGet("/files/{id}/{name}", (string id, string name, HttpContext ctx, Store store, EventService events, PairingService pairing) =>
        {
            if (ctx.Items["isAdmin"] is not true) return Results.Json(new { error = "Admin mode required." }, statusCode: 403);
            if (!IdPattern().IsMatch(id) || !FilePattern().IsMatch(name)) return Results.NotFound();

            var kioskHash = ctx.Items["kioskHash"] as string ?? "";
            var sessionToken = ctx.Request.Headers["X-Admin-Session"].ToString();
            var user = store.ValidateAdminSession(sessionToken, kioskHash);
            if (user is null)
                return Results.Json(new { error = "Admin authentication required." }, statusCode: 401);

            if (name.StartsWith("doc_", StringComparison.Ordinal))
            {
                var rec = store.GetCheckIn(id);
                if (rec is null) return Results.NotFound();
                int idx = rec.DocumentFiles.IndexOf(name);
                if (idx < 0) return Results.NotFound();

                var ev = events.Get();
                var role = ResolveAdminRole(ev, user.AccessLevelId);
                var access = GetDocumentAccess(ev, role, rec.DocumentIds?.ElementAtOrDefault(idx), rec.DocumentNames?.ElementAtOrDefault(idx));
                if (access == "restricted")
                    return Results.Json(new { error = "Your access level does not have permission to view this document." }, statusCode: 403);

                if (access == "pin_required")
                {
                    var suppliedPin = ctx.Request.Headers["X-Admin-Pin"].ToString();
                    var expectedHash = !string.IsNullOrEmpty(user.PinHash) ? user.PinHash : role?.PinHash;
                    var (pinOk, pinErr) = pairing.VerifyAdminRolePin(suppliedPin, expectedHash, ctx.Connection.RemoteIpAddress);
                    if (!pinOk)
                        return Results.Json(new { error = pinErr ?? "Invalid PIN." }, statusCode: 401);
                }
            }

            var path = Path.Combine(store.FolderFor(id), name);
            if (!File.Exists(path) && name == "qrcode.png" && store.Exists(id))
                EnsureQrCode(store.FolderFor(id), id);
            return File.Exists(path)
                ? Results.File(path, name.EndsWith(".png") ? "image/png" : "image/jpeg")
                : Results.NotFound();
        });

        // Current event set up by the admin. Kiosks poll this and re-download when Version changes.
        secured.MapGet("/event", (EventService events) => Results.Json(events.GetForKiosk(), Json));
        secured.MapGet("/event/logo", (EventService events) =>
            events.LogoPath() is { } p
                ? Results.File(p, p.EndsWith(".png") ? "image/png" : "image/jpeg")
                : Results.NotFound());

        // Upload one check-in: multipart with "record" (JSON) + "photo", "documents" (0..n), "signature".
        secured.MapPost("/checkins", async (HttpRequest request, Store store) =>
        {
            if (!request.HasFormContentType) return Results.BadRequest(new { error = "Expected multipart form." });
            var form = await request.ReadFormAsync();

            CheckInDto? rec;
            try { rec = JsonSerializer.Deserialize<CheckInDto>(form["record"].ToString(), Json); }
            catch (JsonException) { rec = null; }
            if (rec is null || !IdPattern().IsMatch(rec.Id) || string.IsNullOrWhiteSpace(rec.FullName))
                return Results.BadRequest(new { error = "Invalid record." });

            // Idempotent: a kiosk retrying after a dropped connection gets OK.
            if (store.Exists(rec.Id)) return Results.Ok(new { id = rec.Id, duplicate = true });

            var folder = store.FolderFor(rec.Id);
            Directory.CreateDirectory(folder);

            // File names are chosen by the server, never taken from the client.
            string? photo = await SaveAsync(form.Files.GetFile("photo"), folder, "photo.jpg");
            var docs = new List<string>();
            int n = 0;
            foreach (var f in form.Files.GetFiles("documents"))
                if (await SaveAsync(f, folder, $"doc_{++n}.jpg") is { } name) docs.Add(name);
            string? signature = await SaveAsync(form.Files.GetFile("signature"), folder, "signature.png");
            string? qrcode = await SaveAsync(form.Files.GetFile("qrcode"), folder, "qrcode.png") ?? EnsureQrCode(folder, rec.Id);

            var kiosk = request.HttpContext.Items["kiosk"] as string;
            var answers = (rec.Answers ?? new()).Take(100).Select(a => new FieldAnswer
            {
                FieldId = Clip(a.FieldId, 40), Label = Clip(a.Label, 120),
                Values = (a.Values ?? new()).Take(50).Select(v => Clip(v, 2000)).ToList()
            }).ToList();
            var docNames = (rec.DocumentNames ?? new()).Take(docs.Count).Select(n => Clip(n, 80)).ToList();
            var docIds = (rec.DocumentIds ?? new()).Take(docs.Count).Select(n => Clip(n, 40)).ToList();
            store.Insert(rec with
            {
                FullName = Clip(rec.FullName, 200), Email = Clip(rec.Email, 200), EventName = Clip(rec.EventName, 120),
                KioskName = kiosk, PhotoFile = photo, DocumentFiles = docs, SignatureFile = signature, QrCodeFile = qrcode,
                Answers = answers, DocumentNames = docNames, DocumentIds = docIds
            });
            return Results.Ok(new { id = rec.Id, duplicate = false });
        }).DisableAntiforgery();
    }

    static AdminRoleDef? ResolveAdminRole(EventConfig ev, string? roleId)
    {
        if (!string.IsNullOrEmpty(roleId) && ev.AdminRoles.FirstOrDefault(r => r.Id == roleId) is { } r)
            return r;
        return ev.AdminRoles.FirstOrDefault();
    }

    static string GetDocumentAccess(EventConfig ev, AdminRoleDef? role, string? docId, string? docName)
    {
        if (role is null) return "restricted";
        DocumentDef? def = null;
        if (!string.IsNullOrEmpty(docId))
            def = ev.Documents.FirstOrDefault(d => d.Id == docId);
        if (def is null && !string.IsNullOrEmpty(docName))
            def = ev.Documents.FirstOrDefault(d => string.Equals(d.Name, docName, StringComparison.OrdinalIgnoreCase));

        if (def is null) return "restricted";
        if (!role.AllowedDocumentIds.Contains(def.Id)) return "restricted";
        return def.RequirePinToView ? "pin_required" : "allowed";
    }

    static string Clip(string? s, int max) => s is null ? "" : s.Length <= max ? s : s[..max];

    static async Task<string?> SaveAsync(IFormFile? file, string folder, string name)
    {
        if (file is null || file.Length == 0) return null;
        await using var fs = File.Create(Path.Combine(folder, name));
        await file.CopyToAsync(fs);
        return name;
    }

    public static string EnsureQrCode(string folder, string id)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "qrcode.png");
        if (!File.Exists(path))
        {
            using var gen = new QRCodeGenerator();
            using var data = gen.CreateQrCode($"EVENTCHECKIN:{id}", QRCodeGenerator.ECCLevel.M);
            using var qr = new PngByteQRCode(data);
            File.WriteAllBytes(path, qr.GetGraphic(12));
        }
        return "qrcode.png";
    }

    public record PairRequest(string? Pin, string? KioskName, bool IsAdmin = false);
    public record SelectModeRequest(bool IsAdmin);
    public record AdminLoginRequest(string? Username, string? Pin);
    public record AdminResetPinRequest(string? Username, string? CurrentPin, string? NewPin);
}
