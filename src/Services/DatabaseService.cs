using System.Text.Json;
using LanguageKnowledgeTester.Models;

namespace LanguageKnowledgeTester.Services;

public class DatabaseService
{
    private readonly string _dbPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

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
        if (string.IsNullOrEmpty(_dbPath))
        {
            return;
        }
        var json = JsonSerializer.Serialize(db, JsonOptions);
        File.WriteAllText(_dbPath, json);
    }

    // Merges a parse result into the existing database.
    // Language names are always updated; existing mapping frequencies are preserved.
    public void MergeFromParsed(Database existing, ParseResult result)
    {
        existing.OtherLanguage = result.OtherLanguage;
        existing.PronunciationLanguage = result.PronunciationLanguage;
        existing.UserLanguage = result.UserLanguage;

        foreach (var incoming in result.Mappings)
        {
            // Match on Type + Prompt + first answer so that two mappings with the same
            // prompt but different answers (e.g. two words sharing the same English meaning)
            // are treated as distinct entries rather than collapsing into one.
            var match = existing.Mappings.FirstOrDefault(m =>
                m.Type == incoming.Type &&
                m.Prompt == incoming.Prompt &&
                m.Answers.Count > 0 && incoming.Answers.Count > 0 &&
                m.Answers[0] == incoming.Answers[0]);

            if (match != null)
            {
                match.Answers = incoming.Answers; // refresh answers, preserve frequency
            }
            else
            {
                existing.Mappings.Add(incoming);
            }
        }
    }
}
