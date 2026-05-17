# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Run

```powershell
# Build
dotnet build src/LanguageKnowledgeTester.csproj

# Run
dotnet run --project src/LanguageKnowledgeTester.csproj
```

There are no automated tests. Verification is done by running the app manually.

## Architecture

This is an Avalonia 11 desktop app (.NET 8) for drilling foreign language vocabulary. It uses plain `INotifyPropertyChanged` MVVM — no framework.

**Data flow:** `InputFileParser` → `DatabaseService.MergeFromParsed` → `Database` (JSON on disk) → `QuizService` selects questions → `MainViewModel` drives the UI.

### Input file format

CSV with a header line: `OtherLanguage, PronunciationLanguage, UserLanguage`

Data lines: `word, pronunciation, meaning`

- Multiple meanings/answers use `(answer1; answer2)` — semicolons inside parentheses
- Implied context (not required in answer) uses `<context>` — stripped by `Normalize()` at answer-check time
- Commas inside a field must be wrapped in `()` to avoid mis-splitting (see `SplitLineIntoThree`)

Example files are in `Test/ExampleInputs/`.

### Key design decisions

**Per-language databases:** Each language combination gets its own JSON file named `{Other}-{Pronunciation}-{User}.json` in `%APPDATA%\LanguageKnowledgeTester\`. Switching languages saves the current DB and loads (or creates) the one for the new combination.

**Six mapping directions:** Each input line generates up to 6 `Mapping` objects (all combinations of Other/Pronunciation/User as prompt and answer). `MappingType` enum names the direction.

**Frequency system:** `FrequencyMultiplier` starts at `1.0`. Correct answers step it down by `0.1` (floor `0.1`). Wrong answers step it up by `0.1` (cap `1.0`). "Very Easy" drops it to `0.1`, or to `0.0` (never asked again) if already at `0.1`. Selection is weighted-random proportional to frequency; the last 10 asked are excluded from candidates when possible.

**Answer normalisation:** `QuizService.Normalize` strips `<…>` tags, lowercases, and converts macron vowels to their doubled equivalents (`ō→ou`, `ū→uu`, etc.) so both forms are accepted.

**Question prompt display:** `MainViewModel` exposes three properties (`QuestionPrefix`, `QuestionBold`, `QuestionSuffix`) bound to inline `Run` elements in the AXAML so the prompt word renders bold with no extra spacing between runs.
