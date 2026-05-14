using System.Text.Json;
using LanguageKnowledgeTester.Models;

namespace LanguageKnowledgeTester.Services;

public class DatabaseService
{
    private readonly string _dbPath;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public DatabaseService(string dbPath) => _dbPath = dbPath;

    public Database Load()
    {
        if (!File.Exists(_dbPath)) return new Database();
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
        var json = JsonSerializer.Serialize(db, JsonOptions);
        File.WriteAllText(_dbPath, json);
    }

    // Merges freshly parsed mappings into the existing database.
    // Existing mappings keep their FrequencyMultiplier; new ones are added at default (1.0).
    public void MergeFromParsed(Database existing, List<Mapping> parsed)
    {
        foreach (var incoming in parsed)
        {
            var match = existing.Mappings.FirstOrDefault(m =>
                m.Type == incoming.Type && m.Prompt == incoming.Prompt);

            if (match != null)
                match.Answers = incoming.Answers; // refresh answers, preserve frequency
            else
                existing.Mappings.Add(incoming);
        }
    }
}
