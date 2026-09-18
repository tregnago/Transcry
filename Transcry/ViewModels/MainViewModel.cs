using Microsoft.Win32;
using Transcry.Helpers;
using Transcry.Services;
using Transcry.Views;

namespace Transcry.ViewModels;

public sealed class MainViewModel : ViewModelBase
{
  private readonly ISettingsService _settingsService;
  private readonly ITranscriptionService _transcriptionService;
  private CancellationTokenSource? _transcriptionCts;

  private string? _selectedFilePath;
  private string _transcriptionText = string.Empty;
  private string _statusMessage = "Select an audio file and start transcription.";
  private string? _errorMessage;
  private bool _isBusy;
  private bool _hasTranscription;

  public MainViewModel(ISettingsService settingsService, ITranscriptionService transcriptionService)
  {
    _settingsService = settingsService;
    _transcriptionService = transcriptionService;

    BrowseCommand = new RelayCommand(BrowseForFile, () => !IsBusy);
    TranscribeCommand = new AsyncRelayCommand(TranscribeAsync, () => !IsBusy && !string.IsNullOrWhiteSpace(SelectedFilePath));
    SaveCommand = new RelayCommand(SaveTranscription, () => !IsBusy && HasTranscription);
    CancelCommand = new RelayCommand(CancelTranscription, () => IsBusy);
    OpenSettingsCommand = new RelayCommand(OpenSettings, () => !IsBusy);
  }

  public string? SelectedFilePath
  {
    get => _selectedFilePath;
    private set
    {
      if (SetProperty(ref _selectedFilePath, value))
      {
        ErrorMessage = null;
        if (!string.IsNullOrWhiteSpace(value) && !AudioFileValidator.TryValidate(value, out var validationError))
        {
          ErrorMessage = validationError;
        }

        StatusMessage = string.IsNullOrWhiteSpace(value)
          ? "Select an audio file and start transcription."
          : BuildSelectedFileStatus(value);
        RaiseCommandStates();
      }
    }
  }

  public string TranscriptionText
  {
    get => _transcriptionText;
    private set
    {
      if (SetProperty(ref _transcriptionText, value))
      {
        HasTranscription = !string.IsNullOrWhiteSpace(value);
      }
    }
  }

  public string StatusMessage
  {
    get => _statusMessage;
    private set => SetProperty(ref _statusMessage, value);
  }

  public string? ErrorMessage
  {
    get => _errorMessage;
    private set => SetProperty(ref _errorMessage, value);
  }

  public bool IsBusy
  {
    get => _isBusy;
    private set
    {
      if (SetProperty(ref _isBusy, value))
      {
        RaiseCommandStates();
      }
    }
  }

  public bool HasTranscription
  {
    get => _hasTranscription;
    private set
    {
      if (SetProperty(ref _hasTranscription, value))
      {
        RaiseCommandStates();
      }
    }
  }

  public string SupportedFormats => AudioFileValidator.SupportedFormatsDescription;

  public RelayCommand BrowseCommand { get; }
  public AsyncRelayCommand TranscribeCommand { get; }
  public RelayCommand SaveCommand { get; }
  public RelayCommand CancelCommand { get; }
  public RelayCommand OpenSettingsCommand { get; }

  private void BrowseForFile()
  {
    var dialog = new OpenFileDialog
    {
      Title = "Select audio file",
      Filter =
        "Supported audio|*.mp3;*.mp4;*.mpeg;*.mpga;*.m4a;*.wav;*.webm|" +
        "All files|*.*"
    };

    if (dialog.ShowDialog() == true)
    {
      SelectedFilePath = dialog.FileName;
    }
  }

  private async Task TranscribeAsync()
  {
    ErrorMessage = null;
    TranscriptionText = string.Empty;

    if (!_settingsService.HasApiKey)
    {
      ErrorMessage = "API key is not configured. Open Settings and enter your OpenAI key.";
      OpenSettings();
      return;
    }

    if (string.IsNullOrWhiteSpace(SelectedFilePath))
    {
      ErrorMessage = "Select an audio file before continuing.";
      return;
    }

    if (!AudioFileValidator.TryValidate(SelectedFilePath, out var validationError))
    {
      ErrorMessage = validationError;
      return;
    }

    _transcriptionCts = new CancellationTokenSource();
    IsBusy = true;
    StatusMessage = "Transcription in progress… this may take a few minutes.";

    try
    {
      var settings = _settingsService.Load();
      var progress = new Progress<string>(message => StatusMessage = message);
      var result = await _transcriptionService.TranscribeAsync(
        SelectedFilePath,
        settings.ApiKey!,
        _transcriptionCts.Token,
        progress).ConfigureAwait(true);

      TranscriptionText = result;
      StatusMessage = "Transcription complete. You can save the text to a file.";
    }
    catch (OperationCanceledException)
    {
      StatusMessage = "Transcription cancelled.";
      ErrorMessage = "Operation cancelled by the user.";
    }
    catch (Exception ex)
    {
      StatusMessage = "Transcription failed.";
      ErrorMessage = ErrorMessageMapper.ToUserMessage(ex);
    }
    finally
    {
      _transcriptionCts?.Dispose();
      _transcriptionCts = null;
      IsBusy = false;
    }
  }

  private void CancelTranscription()
  {
    _transcriptionCts?.Cancel();
    StatusMessage = "Cancelling…";
  }

  private void SaveTranscription()
  {
    ErrorMessage = null;

    if (!HasTranscription)
    {
      ErrorMessage = "There is nothing to save. Run a transcription first.";
      return;
    }

    var defaultName = string.IsNullOrWhiteSpace(SelectedFilePath)
      ? "transcription.txt"
      : $"{Path.GetFileNameWithoutExtension(SelectedFilePath)}_transcription.txt";

    var dialog = new SaveFileDialog
    {
      Title = "Save transcription",
      Filter = "Text files|*.txt|All files|*.*",
      FileName = defaultName,
      DefaultExt = ".txt"
    };

    if (dialog.ShowDialog() != true)
    {
      return;
    }

    try
    {
      File.WriteAllText(dialog.FileName, TranscriptionText);
      StatusMessage = $"Transcription saved to: {dialog.FileName}";
    }
    catch (Exception ex)
    {
      ErrorMessage = $"Could not save the file: {ex.Message}";
    }
  }

  private void OpenSettings()
  {
    var window = new SettingsWindow(_settingsService)
    {
      Owner = System.Windows.Application.Current.MainWindow
    };

    if (window.ShowDialog() == true)
    {
      StatusMessage = _settingsService.HasApiKey
        ? "API key saved."
        : "Configure the API key to use the Whisper service.";
    }
  }

  private static string BuildSelectedFileStatus(string filePath)
  {
    var fileName = Path.GetFileName(filePath);
    if (!AudioFileValidator.ExceedsWhisperLimit(filePath))
    {
      return $"Selected file: {fileName}";
    }

    var sizeMb = AudioFileValidator.GetFileSizeMegabytes(filePath);
    return $"Selected file: {fileName} ({sizeMb:F1} MB) — it will be split automatically.";
  }

  private void RaiseCommandStates()
  {
    BrowseCommand.RaiseCanExecuteChanged();
    TranscribeCommand.RaiseCanExecuteChanged();
    SaveCommand.RaiseCanExecuteChanged();
    CancelCommand.RaiseCanExecuteChanged();
    OpenSettingsCommand.RaiseCanExecuteChanged();
  }
}
