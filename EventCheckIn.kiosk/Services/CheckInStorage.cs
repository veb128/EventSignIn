using System.Text.Json;
using EventCheckIn.Models;

namespace EventCheckIn.Services;

/// <summary>
/// Saves check-ins to the app data folder:
///   {AppData}/checkins/{id}/record.json, photo.jpg, doc_N.jpg, signature.png
/// </summary>
public class CheckInStorage
{
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public string RootFolder { get; } = Path.Combine(FileSystem.AppDataDirectory, "checkins");

    public async Task<CheckInRecord> SaveAsync(CheckInSession session)
    {
        var record = new CheckInRecord
        {
            FullName = session.FullName.Trim(),
            Email = session.Email.Trim(),
            EventName = session.EventName.Trim(),
            EventVersion = session.Event?.Version ?? 0,
            Answers = session.Answers,
            CheckedInAt = DateTime.Now
        };

        var folder = GetFolder(record);
        Directory.CreateDirectory(folder);

        if (session.Photo is not null)
        {
            record.PhotoFile = "photo.jpg";
            await File.WriteAllBytesAsync(Path.Combine(folder, record.PhotoFile), session.Photo);
        }

        for (int i = 0; i < session.Documents.Count; i++)
        {
            var name = $"doc_{i + 1}.jpg";
            await File.WriteAllBytesAsync(Path.Combine(folder, name), session.Documents[i].Image);
            record.DocumentFiles.Add(name);
            record.DocumentNames.Add(session.Documents[i].Name);
        }

        if (session.Signature is not null)
        {
            record.SignatureFile = "signature.png";
            await File.WriteAllBytesAsync(Path.Combine(folder, record.SignatureFile), session.Signature);
        }

        await File.WriteAllTextAsync(Path.Combine(folder, "record.json"),
            JsonSerializer.Serialize(record, JsonOptions));
        return record;
    }

    public async Task<List<CheckInRecord>> LoadAllAsync()
    {
        var list = new List<CheckInRecord>();
        if (!Directory.Exists(RootFolder)) return list;

        foreach (var dir in Directory.GetDirectories(RootFolder))
        {
            var file = Path.Combine(dir, "record.json");
            if (!File.Exists(file)) continue;
            try
            {
                var rec = JsonSerializer.Deserialize<CheckInRecord>(await File.ReadAllTextAsync(file));
                if (rec is not null) list.Add(rec);
            }
            catch (JsonException) { /* skip corrupt record */ }
        }
        return list.OrderByDescending(r => r.CheckedInAt).ToList();
    }

    public Task UpdateAsync(CheckInRecord record) =>
        File.WriteAllTextAsync(Path.Combine(GetFolder(record), "record.json"),
            JsonSerializer.Serialize(record, JsonOptions));

    public string GetFolder(CheckInRecord record) => Path.Combine(RootFolder, record.Id);

    public string? GetPath(CheckInRecord record, string? file) =>
        file is null ? null : Path.Combine(GetFolder(record), file);
}
