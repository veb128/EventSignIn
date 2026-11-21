using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EventCheckIn.Services;

namespace EventCheckIn.ViewModels;

public partial class SettingsViewModel(ServerConnection server, SyncService sync, EventConfigService events) : ObservableObject
{
    public SyncService Sync => sync;
    public ObservableCollection<FoundServer> Servers { get; } = new();

    [ObservableProperty] string kioskName = string.Empty;
    [ObservableProperty] string manualAddress = string.Empty;
    [ObservableProperty] string pin = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanPair))] FoundServer? selectedServer;
    [ObservableProperty] bool isBusy;
    [ObservableProperty] string message = string.Empty;
    [ObservableProperty] string pairedWith = string.Empty;
    [ObservableProperty] bool isPaired;

    public bool CanPair => SelectedServer is not null;

    public void Load()
    {
        KioskName = server.KioskName;
        Refresh();
    }

    void Refresh()
    {
        IsPaired = server.IsPaired;
        PairedWith = IsPaired ? $"Paired with {server.ServerName} ({server.BaseUrl})" : "Not paired";
    }

    partial void OnKioskNameChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value)) server.KioskName = value.Trim();
    }

    [RelayCommand]
    async Task Discover()
    {
        IsBusy = true;
        Message = "Searching the local network…";
        Servers.Clear();
        try
        {
            foreach (var s in await server.DiscoverAsync()) Servers.Add(s);
            if (Servers.Count == 1) SelectedServer = Servers[0];
            Message = Servers.Count == 0
                ? "No host found. Make sure this device is on the host's Wi-Fi, or type the address shown on the host."
                : "Select the host and enter the PIN shown on its dashboard.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task AddManual()
    {
        if (string.IsNullOrWhiteSpace(ManualAddress)) return;
        IsBusy = true;
        try
        {
            var s = await server.ProbeAddressAsync(ManualAddress);
            if (s is null) { Message = $"No EventCheckIn host answered at {ManualAddress}."; return; }
            if (Servers.All(x => x.BaseUrl != s.BaseUrl)) Servers.Add(s);
            SelectedServer = s;
            Message = "Host found. Enter the PIN to pair.";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task Pair()
    {
        if (SelectedServer is null) return;
        if (Pin.Trim().Length != 6) { Message = "Enter the 6-digit PIN shown on the host."; return; }
        IsBusy = true;
        try
        {
            var (ok, error) = await server.PairAsync(SelectedServer, Pin.Trim());
            Message = ok ? "Paired! Check-ins will now sync to the host." : $"Pairing failed: {error}";
            if (ok) { Pin = string.Empty; Refresh(); _ = sync.SyncNowAsync(); }
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    async Task Unpair()
    {
        if (!await Shell.Current.DisplayAlertAsync("Unpair kiosk",
                "Stop syncing to this host? Check-ins will still be saved on this device.", "Unpair", "Cancel")) return;
        server.Unpair();
        events.ResetToDefault();
        Refresh();
        _ = sync.SyncNowAsync();
    }

    [RelayCommand]
    Task SyncNow() => sync.SyncNowAsync();
}
