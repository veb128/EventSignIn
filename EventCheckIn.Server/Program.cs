using EventCheckIn.Server;
using Microsoft.AspNetCore.Http.Features;

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection("Server").Get<ServerOptions>() ?? new ServerOptions();
if (string.IsNullOrWhiteSpace(options.Name)) options.Name = Environment.MachineName;
const long MaxUpload = 256L * 1024 * 1024;

builder.WebHost.ConfigureKestrel(k =>
{
    k.ListenAnyIP(options.Port);              // LAN only – nothing here talks to the internet
    k.Limits.MaxRequestBodySize = MaxUpload;
});
builder.Services.Configure<FormOptions>(f => f.MultipartBodyLengthLimit = MaxUpload);

builder.Services.AddSingleton(options);
builder.Services.AddSingleton<Store>();
builder.Services.AddSingleton<PairingService>();
builder.Services.AddSingleton<EventService>();
builder.Services.AddSingleton<HotspotService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<HotspotService>());

var app = builder.Build();
app.Services.GetRequiredService<Store>().Initialize();

ApiEndpoints.Map(app);
DashboardEndpoints.Map(app);

app.Lifetime.ApplicationStarted.Register(() =>
{
    var hotspot = app.Services.GetRequiredService<HotspotService>();
    var pin = app.Services.GetRequiredService<PairingService>().Pin;
    var store = app.Services.GetRequiredService<Store>();
    Console.WriteLine($"""

        ==============================================================
         EventCheckIn Host: {options.Name}
         Dashboard (this computer only):  http://localhost:{options.Port}
         Kiosk addresses: {string.Join(", ", NetworkInfo.LanAddresses().Select(a => $"{a}:{options.Port}"))}
         Pairing PIN: {pin}
         Wi-Fi: {hotspot.Ssid}  /  password: {hotspot.Password}
         {hotspot.Status}
         Data: {store.DataDir}
        ==============================================================

        """);
});

app.Run();
