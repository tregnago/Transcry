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
  private string _statusMessage = "Seleziona un file audio e avvia la trascrizione.";
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
          ? "Seleziona un file audio e avvia la trascrizione."
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
      Title = "Seleziona file audio",
      Filter =
        "Audio supportati|*.mp3;*.mp4;*.mpeg;*.mpga;*.m4a;*.wav;*.webm|" +
        "Tutti i file|*.*"
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
      ErrorMessage = "Chiave API non configurata. Apri Impostazioni e inserisci la tua chiave OpenAI.";
      OpenSettings();
      return;
    }

    if (string.IsNullOrWhiteSpace(SelectedFilePath))
    {
      ErrorMessage = "Seleziona un file audio prima di procedere.";
      return;
    }

    if (!AudioFileValidator.TryValidate(SelectedFilePath, out var validationError))
    {
      ErrorMessage = validationError;
      return;
    }

    _transcriptionCts = new CancellationTokenSource();
    IsBusy = true;
    StatusMessage = "Trascrizione in corso… attendi, potrebbe richiedere alcuni minuti.";

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
      StatusMessage = "Trascrizione completata. Puoi salvare il testo in un file.";
    }
    catch (OperationCanceledException)
    {
      StatusMessage = "Trascrizione annullata.";
      ErrorMessage = "Operazione annullata dall'utente.";
    }
    catch (Exception ex)
    {
      StatusMessage = "Trascrizione non riuscita.";
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
    StatusMessage = "Annullamento in corso…";
  }

  private void SaveTranscription()
  {
    ErrorMessage = null;

    if (!HasTranscription)
    {
      ErrorMessage = "Non c'è nulla da salvare. Esegui prima una trascrizione.";
      return;
    }

    var defaultName = string.IsNullOrWhiteSpace(SelectedFilePath)
      ? "trascrizione.txt"
      : $"{Path.GetFileNameWithoutExtension(SelectedFilePath)}_trascrizione.txt";

    var dialog = new SaveFileDialog
    {
      Title = "Salva trascrizione",
      Filter = "File di testo|*.txt|Tutti i file|*.*",
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
      StatusMessage = $"Trascrizione salvata in: {dialog.FileName}";
    }
    catch (Exception ex)
    {
      ErrorMessage = $"Impossibile salvare il file: {ex.Message}";
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
        ? "Chiave API salvata correttamente."
        : "Configura la chiave API per usare il servizio Whisper.";
    }
  }

  private static string BuildSelectedFileStatus(string filePath)
  {
    var fileName = Path.GetFileName(filePath);
    if (!AudioFileValidator.ExceedsWhisperLimit(filePath))
    {
      return $"File selezionato: {fileName}";
    }

    var sizeMb = AudioFileValidator.GetFileSizeMegabytes(filePath);
    return $"File selezionato: {fileName} ({sizeMb:F1} MB) — verrà suddiviso automaticamente.";
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
