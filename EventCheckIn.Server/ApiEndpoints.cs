using System.Text.Json;
using System.Text.RegularExpressions;

namespace EventCheckIn.Server;

/// <summary>JSON API used by the kiosks.</summary>
public static partial class ApiEndpoints
{
    public const string Product = "EventCheckIn";
    static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [GeneratedRegex("^[a-f0-9]{32}$")] private static partial Regex IdPattern();

    public static void Map(WebApplication app)
    {
        var api = app.MapGroup("/api");

        // Discovery probe – no auth, reveals nothing private.
        api.MapGet("/info", (ServerOptions o) => new { product = Product, name = o.Name, version = 1 });

        api.MapPost("/pair", (PairRequest req, PairingService pairing, HttpContext ctx) =>
        {
            var (ok, token, error) = pairing.TryPair(req.Pin, req.KioskName, ctx.Connection.RemoteIpAddress);
            return ok ? Results.Ok(new { token }) : Results.Json(new { error }, statusCode: 401);
        });

        var secured = api.MapGroup("").AddEndpointFilter(async (ctx, next) =>
        {
            var header = ctx.HttpContext.Request.Headers.Authorization.ToString();
            var token = header.StartsWith("Bearer ", StringComparison.Ordinal) ? header[7..] : null;
            var store = ctx.HttpContext.RequestServices.GetRequiredService<Store>();
            var kiosk = token is null ? null : store.ValidateToken(token);
            if (kiosk is null) return Results.Json(new { error = "Kiosk is not paired." }, statusCode: 401);
            ctx.HttpContext.Items["kiosk"] = kiosk;
            return await next(ctx);
        });

        secured.MapGet("/ping", () => Results.Ok(new { ok = true }));

        // Current event set up by the admin. Kiosks poll this and re-download when Version changes.
        secured.MapGet("/event", (EventService events) => Results.Json(events.Get(), Json));
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

            var kiosk = request.HttpContext.Items["kiosk"] as string;
            var answers = (rec.Answers ?? new()).Take(100).Select(a => new FieldAnswer
            {
                FieldId = Clip(a.FieldId, 40), Label = Clip(a.Label, 120),
                Values = (a.Values ?? new()).Take(50).Select(v => Clip(v, 2000)).ToList()
            }).ToList();
            var docNames = (rec.DocumentNames ?? new()).Take(docs.Count).Select(n => Clip(n, 80)).ToList();
            store.Insert(rec with
            {
                FullName = Clip(rec.FullName, 200), Email = Clip(rec.Email, 200), EventName = Clip(rec.EventName, 120),
                KioskName = kiosk, PhotoFile = photo, DocumentFiles = docs, SignatureFile = signature,
                Answers = answers, DocumentNames = docNames
            });
            return Results.Ok(new { id = rec.Id, duplicate = false });
        }).DisableAntiforgery();
    }

    static string Clip(string? s, int max) => s is null ? "" : s.Length <= max ? s : s[..max];

    static async Task<string?> SaveAsync(IFormFile? file, string folder, string name)
    {
        if (file is null || file.Length == 0) return null;
        await using var fs = File.Create(Path.Combine(folder, name));
        await file.CopyToAsync(fs);
        return name;
    }

    public record PairRequest(string? Pin, string? KioskName);
}
