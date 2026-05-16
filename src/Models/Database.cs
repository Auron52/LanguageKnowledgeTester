namespace LanguageKnowledgeTester.Models;

public class Database
{
    public List<Mapping> Mappings { get; set; } = new();

    // Ids of the last N questions shown, used to avoid repetition
    public List<Guid> RecentlyAskedIds { get; set; } = new();

    // Language names read from the input file header line
    public string OtherLanguage { get; set; } = "Other Language";
    public string PronunciationLanguage { get; set; } = "Pronunciation";
    public string UserLanguage { get; set; } = "Your Language";
}
