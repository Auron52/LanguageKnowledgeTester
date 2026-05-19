using System.Text.Json;
using LanguageKnowledgeTester.Models;

namespace LanguageKnowledgeTester.Services;

public class DatabaseService
{
    private readonly string _dbPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };
    private readonly SemaphoreSlim _writeLock = new(1, 1);

    public DatabaseService(string dbPath) => _dbPath = dbPath;

    // Returns the database file path for a given language combination.
    // Language names are sanitised so they can be used safely as a filename.
    public static string GetPath(string appDataFolder, string other, string pronunciation, string user)
    {
        static string Sanitize(string s) =>
            string.Concat(s.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        return Path.Combine(appDataFolder, $"{Sanitize(other)}-{Sanitize(pronunciation)}-{Sanitize(user)}.json");
    }

    public Database Load()
    {
        if (string.IsNullOrEmpty(_dbPath) || !File.Exists(_dbPath))
        {
            return new Database();
        }
        try
        {
            var json = File.ReadAllText(_dbPath);
            return JsonSerializer.Deserialize<Database>(json, JsonOptions) ?? new Database();
        }
        catch
        {
            return new Database();
        }
    }

    public void Save(Database db)
    {
        if (string.IsNullOrEmpty(_dbPath)) return;
        var json = JsonSerializer.Serialize(db, JsonOptions);
        _writeLock.Wait();
        try { File.WriteAllText(_dbPath, json); }
        finally { _writeLock.Release(); }
    }

    // Blocks until any in-flight QueueSave has finished writing. Call on app shutdown.
    public void WaitForPendingSave()
    {
        _writeLock.Wait();
        _writeLock.Release();
    }

    // Serializes on the calling thread (consistent snapshot), then writes to disk on a
    // background thread. Use for frequent saves where blocking the UI is undesirable.
    public void QueueSave(Database db)
    {
        if (string.IsNullOrEmpty(_dbPath)) return;
        var json = JsonSerializer.Serialize(db, JsonOptions);
        _ = Task.Run(async () =>
        {
            await _writeLock.WaitAsync();
            try { await File.WriteAllTextAsync(_dbPath, json); }
            finally { _writeLock.Release(); }
        });
    }

    // Merges a parse result into the existing database.
    // Language names are always updated; existing mapping frequencies are preserved.
    public void MergeFromParsed(Database existing, ParseResult result)
    {
        existing.OtherLanguage = result.OtherLanguage;
        existing.PronunciationLanguage = result.PronunciationLanguage;
        existing.UserLanguage = result.UserLanguage;

        // Index existing mappings by (Type, Prompt, first answer) for O(1) lookup, avoiding O(n²) behaviour over large imports.
        var existingIndex = existing.Mappings
            .ToDictionary(m => (m.Type, m.Prompt, m.Answers.Count > 0 ? m.Answers[0] : ""));

        foreach (var incoming in result.Mappings)
        {
            var incomingKey = (incoming.Type, incoming.Prompt, incoming.Answers.Count > 0 ? incoming.Answers[0] : "");
            if (existingIndex.TryGetValue(incomingKey, out var match))
            {
                match.Answers = incoming.Answers; // refresh answers, preserve frequency
            }
            else
            {
                existing.Mappings.Add(incoming);
                existingIndex[incomingKey] = incoming;
            }
        }
    }
}
