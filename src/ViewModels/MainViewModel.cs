using System.ComponentModel;
using System.Runtime.CompilerServices;
using LanguageKnowledgeTester.Models;
using LanguageKnowledgeTester.Services;

namespace LanguageKnowledgeTester.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private DatabaseService _dbService;
    private readonly QuizService _quizService;
    private Database _database;
    private readonly string _appDataFolder;
    private string _currentDbPath;

    private Mapping? _currentQuestion;
    private Mapping? _pendingFollowUp;
    private bool _isFollowUp;
    private readonly HashSet<Guid> _usedAlternateIds = new();
    private string _userAnswer = "";
    private string _feedback = "";
    private bool _showVeryEasy;
    private bool _questionAnswered;
    private bool _lastAnswerWasCorrect;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        _appDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LanguageKnowledgeTester");
        Directory.CreateDirectory(_appDataFolder);
        _quizService = new QuizService();

        // Auto-load the most recently used language database if one exists.
        _currentDbPath = Directory.GetFiles(_appDataFolder, "*.json")
                                  .OrderByDescending(File.GetLastWriteTime)
                                  .FirstOrDefault() ?? string.Empty;

        _dbService = new DatabaseService(_currentDbPath);
        _database = _dbService.Load();

        if (_database.Mappings.Count > 0)
        {
            NextQuestion();
        }
    }

    // --- Bindable properties ---

    public string QuestionTypeLabel => _currentQuestion != null
        ? DescribeType(_currentQuestion.Type)
        : "";

    public string QuestionPrefix
    {
        get
        {
            if (_database.Mappings.Count == 0)
                return "No questions have been loaded. Please select an input file to begin.";
            if (_currentQuestion == null)
                return "All questions have been mastered!";
            return _isFollowUp ? "What else is " : "What is ";
        }
    }

    public string QuestionBold => (_currentQuestion != null && _database.Mappings.Count > 0)
        ? _currentQuestion.Prompt
        : "";

    public string QuestionSuffix
    {
        get
        {
            if (_currentQuestion == null || _database.Mappings.Count == 0)
            {
                return "";
            }
            string target = _currentQuestion.Type switch
            {
                MappingType.OtherToUser or MappingType.PronunciationToUser        => _database.UserLanguage,
                MappingType.UserToOther or MappingType.PronunciationToOther       => _database.OtherLanguage,
                MappingType.OtherToPronunciation or MappingType.UserToPronunciation => _database.PronunciationLanguage,
                _ => "?"
            };
            return $" in {target}?";
        }
    }

    public string UserAnswer
    {
        get => _userAnswer;
        set { _userAnswer = value; Notify(); }
    }

    public string Feedback
    {
        get => _feedback;
        set { _feedback = value; Notify(); }
    }

    public bool ShowVeryEasy
    {
        get => _showVeryEasy;
        set { _showVeryEasy = value; Notify(); }
    }

    public bool QuestionAnswered
    {
        get => _questionAnswered;
        set { _questionAnswered = value; Notify(); Notify(nameof(CanSubmit)); Notify(nameof(CanNext)); }
    }

    public bool HasQuestion => _currentQuestion != null;
    public bool CanSubmit => HasQuestion && !QuestionAnswered;
    public bool CanNext => QuestionAnswered;

    // --- Actions called by the view ---

    public void LoadInputFile(string filePath)
    {
        var result = new InputFileParser().Parse(filePath);
        var newPath = DatabaseService.GetPath(_appDataFolder, result.OtherLanguage, result.PronunciationLanguage, result.UserLanguage);

        if (newPath != _currentDbPath)
        {
            // Different language combination: persist the current database and switch to the new one.
            _dbService.Save(_database);
            _currentDbPath = newPath;
            _dbService = new DatabaseService(_currentDbPath);
            _database = _dbService.Load();
        }

        _dbService.MergeFromParsed(_database, result);
        _dbService.Save(_database);
        _pendingFollowUp = null;
        _isFollowUp = false;
        NextQuestion();
    }

    public void NextQuestion()
    {
        if (_pendingFollowUp != null)
        {
            _currentQuestion = _pendingFollowUp;
            _pendingFollowUp = null;
            _isFollowUp = true;
        }
        else
        {
            _isFollowUp = false;
            _usedAlternateIds.Clear();
            _currentQuestion = _quizService.SelectNextQuestion(_database);

            if (_currentQuestion != null)
            {
                _quizService.RecordAsked(_database, _currentQuestion);
                _dbService.Save(_database);
            }
        }

        UserAnswer = "";
        Feedback = "";
        ShowVeryEasy = false;
        _lastAnswerWasCorrect = false;
        QuestionAnswered = false;

        Notify(nameof(QuestionPrefix));
        Notify(nameof(QuestionBold));
        Notify(nameof(QuestionSuffix));
        Notify(nameof(QuestionTypeLabel));
        Notify(nameof(HasQuestion));
        Notify(nameof(CanSubmit));
        Notify(nameof(CanNext));
    }

    public void SubmitAnswer()
    {
        if (_currentQuestion == null || QuestionAnswered)
        {
            return;
        }

        bool correct = _quizService.CheckAnswer(_currentQuestion, UserAnswer);
        _lastAnswerWasCorrect = correct;

        if (correct)
        {
            _quizService.RecordCorrect(_currentQuestion);
            Feedback = "Correct!";
            ShowVeryEasy = true;
        }
        else
        {
            var alternate = _quizService.FindAlternateMapping(_database, _currentQuestion, UserAnswer);
            if (alternate != null && _usedAlternateIds.Contains(alternate.Id))
            {
                // User repeated an answer already given in this chain — refuse without penalty.
                UserAnswer = "";
                Feedback = "You've already given that answer — try a different one.";
                return;
            }
            else if (alternate != null)
            {
                // User gave a valid answer for a different mapping with the same prompt.
                // Record that mapping as correct and queue the original as a follow-up.
                _usedAlternateIds.Add(alternate.Id);
                _pendingFollowUp = _currentQuestion;
                _currentQuestion = alternate;
                _quizService.RecordCorrect(alternate);
                _lastAnswerWasCorrect = true;
                Feedback = "Correct!";
                ShowVeryEasy = true;
            }
            else
            {
                _quizService.RecordIncorrect(_currentQuestion);
                var expected = string.Join("  /  ", _currentQuestion.Answers);
                Feedback = $"Incorrect. Correct answer(s): {expected}";
                ShowVeryEasy = false;
            }
        }

        _dbService.Save(_database);
        QuestionAnswered = true;
    }

    public void MarkVeryEasy()
    {
        if (_currentQuestion == null || !_lastAnswerWasCorrect)
        {
            return;
        }

        bool wasAtMinimum = _currentQuestion.FrequencyMultiplier <= 0.1;
        _quizService.RecordVeryEasy(_currentQuestion);
        _dbService.Save(_database);

        ShowVeryEasy = false;
        Feedback = wasAtMinimum
            ? "Correct! (Marked as mastered - will not be asked again)"
            : "Correct! (Marked as very easy - will appear very rarely)";
    }

    // --- Helpers ---

    private string DescribeType(MappingType type) => type switch
    {
        MappingType.OtherToUser          => $"{_database.OtherLanguage}  →  {_database.UserLanguage}",
        MappingType.UserToOther          => $"{_database.UserLanguage}  →  {_database.OtherLanguage}",
        MappingType.PronunciationToOther => $"{_database.PronunciationLanguage}  →  {_database.OtherLanguage}",
        MappingType.PronunciationToUser  => $"{_database.PronunciationLanguage}  →  {_database.UserLanguage}",
        MappingType.OtherToPronunciation => $"{_database.OtherLanguage}  →  {_database.PronunciationLanguage}",
        MappingType.UserToPronunciation  => $"{_database.UserLanguage}  →  {_database.PronunciationLanguage}",
        _                                => ""
    };

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
