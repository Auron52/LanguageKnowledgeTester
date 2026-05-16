using LanguageKnowledgeTester.Models;

namespace LanguageKnowledgeTester.Services;

public class ParseResult
{
    public List<Mapping> Mappings { get; init; } = [];
    public string OtherLanguage { get; init; } = "Other Language";
    public string PronunciationLanguage { get; init; } = "Pronunciation";
    public string UserLanguage { get; init; } = "Your Language";
}

public class InputFileParser
{
    public ParseResult Parse(string filePath)
    {
        List<Mapping> mappings = new List<Mapping>();
        string otherLanguage = "Other Language";
        string pronunciationLanguage = "Pronunciation";
        string userLanguage = "Your Language";
        bool headerRead = false;

        foreach (var line in File.ReadLines(filePath))
        {
            string trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                continue;
            }

            if (!headerRead)
            {
                // First non-empty line is the header: OtherLanguage, PronunciationLanguage, UserLanguage
                string[] headerParts = trimmed.Split(',', 3);
                if (headerParts.Length > 0) otherLanguage = headerParts[0].Trim();
                if (headerParts.Length > 1) pronunciationLanguage = headerParts[1].Trim();
                if (headerParts.Length > 2) userLanguage = headerParts[2].Trim();
                headerRead = true;
                continue;
            }

            string[] parts = SplitLineIntoThree(trimmed);

            string? otherLang = parts.Length > 0 ? parts[0].Trim() : null;
            string? pronunciationRaw = parts.Length > 1 ? parts[1].Trim() : null;
            string? userLangRaw = parts.Length > 2 ? parts[2].Trim() : null;

            if (string.IsNullOrWhiteSpace(otherLang))
            {
                continue;
            }

            List<string> pronunciationAnswers = ParseAnswers(pronunciationRaw);
            List<string> userLangAnswers = ParseAnswers(userLangRaw);

            // Use the first pronunciation form as the prompt; accept all forms as answers
            string? pronunciationPrompt = pronunciationAnswers.FirstOrDefault();

            // a. Other Language -> User's Language
            if (userLangAnswers.Count > 0)
            {
                mappings.Add(Make(MappingType.OtherToUser, otherLang, userLangAnswers));
            }

            // b. User's Language -> Other Language (one mapping per meaning)
            if (userLangAnswers.Count > 0)
            {
                foreach (var meaning in userLangAnswers)
                {
                    mappings.Add(Make(MappingType.UserToOther, meaning, [otherLang]));
                }
            }

            // c. Pronunciation -> Other Language
            if (pronunciationPrompt != null)
            {
                mappings.Add(Make(MappingType.PronunciationToOther, pronunciationPrompt, [otherLang]));
            }

            // d. Pronunciation -> User's Language
            if (pronunciationPrompt != null && userLangAnswers.Count > 0)
            {
                mappings.Add(Make(MappingType.PronunciationToUser, pronunciationPrompt, userLangAnswers));
            }

            // e. Other Language -> Pronunciation
            if (pronunciationAnswers.Count > 0)
            {
                mappings.Add(Make(MappingType.OtherToPronunciation, otherLang, pronunciationAnswers));
            }

            // f. User's Language -> Pronunciation (one mapping per meaning)
            if (pronunciationAnswers.Count > 0 && userLangAnswers.Count > 0)
            {
                foreach (var meaning in userLangAnswers)
                {
                    mappings.Add(Make(MappingType.UserToPronunciation, meaning, pronunciationAnswers));
                }
            }
        }

        return new ParseResult
        {
            Mappings = mappings,
            OtherLanguage = otherLanguage,
            PronunciationLanguage = pronunciationLanguage,
            UserLanguage = userLanguage
        };
    }

    // Splits a line on commas, respecting parentheses, into at most 3 parts.
    private static string[] SplitLineIntoThree(string line)
    {
        List<string> parts = new List<string>();
        System.Text.StringBuilder current = new System.Text.StringBuilder();
        int depth = 0;

        foreach (var c in line)
        {
            if (parts.Count == 2)
            {
                current.Append(c);
                continue;
            }

            if (c == '(')
            {
                depth++;
            }
            else if (c == ')')
            {
                depth = Math.Max(0, depth - 1);
            }

            if (c == ',' && depth == 0)
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        parts.Add(current.ToString().Trim());
        return parts.ToArray();
    }

    // Parses a field that may contain multiple answers: "(ans1; ans2)" or "single answer".
    private static List<string> ParseAnswers(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        raw = raw.Trim();

        if (raw.StartsWith('(') && raw.EndsWith(')'))
        {
            raw = raw[1..^1];
        }

        return raw.Split(';')
                  .Select(a => a.Trim())
                  .Where(a => !string.IsNullOrWhiteSpace(a))
                  .ToList();
    }

    private static Mapping Make(MappingType type, string prompt, List<string> answers) =>
        new() { Type = type, Prompt = prompt, Answers = answers };
}
