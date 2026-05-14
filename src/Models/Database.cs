namespace LanguageKnowledgeTester.Models;

public class Database
{
    public List<Mapping> Mappings { get; set; } = new();

    // Ids of the last N questions shown, used to avoid repetition
    public List<Guid> RecentlyAskedIds { get; set; } = new();
}
