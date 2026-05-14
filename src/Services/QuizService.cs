using System.Text.RegularExpressions;
using LanguageKnowledgeTester.Models;

namespace LanguageKnowledgeTester.Services;

public class QuizService
{
    private const int RecentlyAskedLimit = 10;
    private const double FrequencyStepDown = 0.1;   // reduction per correct answer
    private const double MinFrequency = 0.1;         // floor before "very easy" zeroes it
    private const double VeryEasyFrequency = 0.1;    // target when marking very easy

    private readonly Random _random = new();

    public Mapping? SelectNextQuestion(Database db)
    {
        // Prefer questions not in the recent list; fall back to all eligible if needed
        var candidates = db.Mappings
            .Where(m => m.FrequencyMultiplier > 0 && !db.RecentlyAskedIds.Contains(m.Id))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = db.Mappings.Where(m => m.FrequencyMultiplier > 0).ToList();
        }

        if (candidates.Count == 0)
        {
            return null;
        }

        // Weighted random selection based on FrequencyMultiplier
        double totalWeight = candidates.Sum(m => m.FrequencyMultiplier);
        double roll = _random.NextDouble() * totalWeight;
        double cumulative = 0;

        foreach (var mapping in candidates)
        {
            cumulative += mapping.FrequencyMultiplier;
            if (roll <= cumulative)
            {
                return mapping;
            }
        }

        return candidates[^1];
    }

    public void RecordAsked(Database db, Mapping mapping)
    {
        db.RecentlyAskedIds.Add(mapping.Id);
        while (db.RecentlyAskedIds.Count > RecentlyAskedLimit)
        {
            db.RecentlyAskedIds.RemoveAt(0);
        }
    }

    public bool CheckAnswer(Mapping mapping, string userAnswer)
    {
        var normalized = Normalize(userAnswer);
        return mapping.Answers.Any(a => Normalize(a) == normalized);
    }

    // Reduce frequency by one step on each correct answer, down to the minimum.
    public void RecordCorrect(Mapping mapping)
    {
        mapping.FrequencyMultiplier = Math.Max(
            Math.Round(mapping.FrequencyMultiplier - FrequencyStepDown, 10),
            MinFrequency);
    }

    // At minimum frequency: disable the question entirely.
    // Otherwise: drop straight to the minimum so it shows up very rarely.
    public void RecordVeryEasy(Mapping mapping)
    {
        if (mapping.FrequencyMultiplier <= MinFrequency)
        {
            mapping.FrequencyMultiplier = 0.0;
        }
        else
        {
            mapping.FrequencyMultiplier = VeryEasyFrequency;
        }
    }

    // Strip <implied context>, collapse whitespace, lowercase for comparison.
    private static string Normalize(string text) =>
        Regex.Replace(text, @"<[^>]*>", "").Trim().ToLowerInvariant();
}
