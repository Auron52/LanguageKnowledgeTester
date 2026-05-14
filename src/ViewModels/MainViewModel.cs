using System.ComponentModel;
using System.Runtime.CompilerServices;
using LanguageKnowledgeTester.Models;
using LanguageKnowledgeTester.Services;

namespace LanguageKnowledgeTester.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DatabaseService _dbService;
    private readonly QuizService _quizService;
    private readonly Database _database;

    private Mapping? _currentQuestion;
    private string _userAnswer = "";
    private string _feedback = "";
    private bool _showVeryEasy;
    private bool _questionAnswered;
    private bool _lastAnswerWasCorrect;

    public event PropertyChangedEventHandler? PropertyChanged;

    public MainViewModel()
    {
        var appData = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "LanguageKnowledgeTester");
        Directory.CreateDirectory(appData);

        _dbService = new DatabaseService(Path.Combine(appData, "database.json"));
        _quizService = new QuizService();
        _database = _dbService.Load();
    }

    // --- Bindable properties ---

    public string QuestionTypeLabel => _currentQuestion != null
        ? DescribeType(_currentQuestion.Type)
        : "";

    public string QuestionPrompt
    {
        get
        {
            if (_database.Mappings.Count == 0)
                return "Load an input file to begin.";
            if (_currentQuestion == null)
                return "All questions have been mastered!";
            return _currentQuestion.Prompt;
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
        var parser = new InputFileParser();
        var parsed = parser.Parse(filePath);
        _dbService.MergeFromParsed(_database, parsed);
        _dbService.Save(_database);
        NextQuestion();
    }

    public void NextQuestion()
    {
        _currentQuestion = _quizService.SelectNextQuestion(_database);

        if (_currentQuestion != null)
        {
            _quizService.RecordAsked(_database, _currentQuestion);
            _dbService.Save(_database);
        }

        UserAnswer = "";
        Feedback = "";
        ShowVeryEasy = false;
        _lastAnswerWasCorrect = false;
        QuestionAnswered = false;

        Notify(nameof(QuestionPrompt));
        Notify(nameof(QuestionTypeLabel));
        Notify(nameof(HasQuestion));
        Notify(nameof(CanSubmit));
        Notify(nameof(CanNext));
    }

    public void SubmitAnswer()
    {
        if (_currentQuestion == null || QuestionAnswered) return;

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
            var expected = string.Join("  /  ", _currentQuestion.Answers);
            Feedback = $"Incorrect. Correct answer(s): {expected}";
            ShowVeryEasy = false;
        }

        _dbService.Save(_database);
        QuestionAnswered = true;
    }

    public void MarkVeryEasy()
    {
        if (_currentQuestion == null || !_lastAnswerWasCorrect) return;

        bool wasAtMinimum = _currentQuestion.FrequencyMultiplier <= 0.1;
        _quizService.RecordVeryEasy(_currentQuestion);
        _dbService.Save(_database);

        ShowVeryEasy = false;
        Feedback = wasAtMinimum
            ? "Correct! (Marked as mastered - will not be asked again)"
            : "Correct! (Marked as very easy - will appear very rarely)";
    }

    // --- Helpers ---

    private static string DescribeType(MappingType type) => type switch
    {
        MappingType.OtherToUser          => "Other Language  →  Your Language",
        MappingType.UserToOther          => "Your Language  →  Other Language",
        MappingType.PronunciationToOther => "Pronunciation  →  Other Language",
        MappingType.PronunciationToUser  => "Pronunciation  →  Your Language",
        MappingType.OtherToPronunciation => "Other Language  →  Pronunciation",
        MappingType.UserToPronunciation  => "Your Language  →  Pronunciation",
        _                                => ""
    };

    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
