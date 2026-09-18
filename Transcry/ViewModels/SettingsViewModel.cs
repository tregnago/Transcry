using Transcry.Models;
using Transcry.Services;

namespace Transcry.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
  private readonly ISettingsService _settingsService;
  private string _apiKey = string.Empty;
  private string? _errorMessage;

  public SettingsViewModel(ISettingsService settingsService)
  {
    _settingsService = settingsService;
    _apiKey = settingsService.Load().ApiKey ?? string.Empty;
  }

  public string ApiKey
  {
    get => _apiKey;
    set => SetProperty(ref _apiKey, value);
  }

  public string? ErrorMessage
  {
    get => _errorMessage;
    private set => SetProperty(ref _errorMessage, value);
  }

  public bool TrySave()
  {
    ErrorMessage = null;

    if (string.IsNullOrWhiteSpace(ApiKey))
    {
      ErrorMessage = "Inserisci una chiave API OpenAI valida.";
      return false;
    }

    if (!ApiKey.StartsWith("sk-", StringComparison.Ordinal))
    {
      ErrorMessage = "La chiave API non sembra valida. Le chiavi OpenAI iniziano di solito con \"sk-\".";
      return false;
    }

    try
    {
      _settingsService.Save(new AppSettings { ApiKey = ApiKey.Trim() });
      return true;
    }
    catch (Exception ex)
    {
      ErrorMessage = $"Impossibile salvare le impostazioni: {ex.Message}";
      return false;
    }
  }
}
