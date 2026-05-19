using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Platform.Storage;
using LanguageKnowledgeTester.ViewModels;

namespace LanguageKnowledgeTester.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
    }

    private async void LoadInputFile_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Input File",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Text files") { Patterns = ["*.txt", "*.csv"] },
                new FilePickerFileType("All files") { Patterns = ["*"] }
            ]
        });

        if (files.Count > 0)
        {
            _vm.LoadInputFile(files[0].Path.LocalPath);
            FocusAnswerBox();
        }
    }

    private void Submit_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.SubmitAnswer();
    }

    private void Next_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.NextQuestion();
        FocusAnswerBox();
    }

    private void VeryEasy_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        _vm.MarkVeryEasy();
    }

    // Enter submits when unanswered, advances when answered
    private void Window_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        if (_vm.CanSubmit)
        {
            _vm.SubmitAnswer();
        }
        else if (_vm.CanNext)
        {
            _vm.NextQuestion();
            FocusAnswerBox();
        }
    }

    protected override void OnClosing(Avalonia.Controls.WindowClosingEventArgs e)
    {
        _vm.Flush();
        base.OnClosing(e);
    }

    private void FocusAnswerBox() => AnswerBox.Focus();
}
