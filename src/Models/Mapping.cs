namespace LanguageKnowledgeTester.Models;

public class Mapping
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public MappingType Type { get; set; }
    public string Prompt { get; set; } = "";
    public List<string> Answers { get; set; } = new();

    // 1.0 = full frequency, 0.1 = minimum (shows rarely), 0.0 = never ask
    public double FrequencyMultiplier { get; set; } = 1.0;
}
