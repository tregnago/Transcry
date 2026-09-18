using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Transcry.Models;

namespace Transcry.Services;

public sealed class SettingsService : ISettingsService
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    WriteIndented = true
  };

  private readonly string _settingsFilePath;

  public SettingsService()
  {
    var appDataPath = Path.Combine(
      Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
      "Transcry");
    Directory.CreateDirectory(appDataPath);
    _settingsFilePath = Path.Combine(appDataPath, "settings.json");
  }

  public bool HasApiKey => !string.IsNullOrWhiteSpace(Load().ApiKey);

  public AppSettings Load()
  {
    if (!File.Exists(_settingsFilePath))
    {
      return new AppSettings();
    }

    try
    {
      var json = File.ReadAllText(_settingsFilePath);
      var stored = JsonSerializer.Deserialize<StoredSettings>(json, JsonOptions);
      if (stored is null)
      {
        return new AppSettings();
      }

      return new AppSettings
      {
        ApiKey = string.IsNullOrWhiteSpace(stored.ProtectedApiKey)
          ? null
          : Unprotect(stored.ProtectedApiKey)
      };
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException(
        "Could not read the saved settings. The file may be damaged.",
        ex);
    }
  }

  public void Save(AppSettings settings)
  {
    var stored = new StoredSettings
    {
      ProtectedApiKey = string.IsNullOrWhiteSpace(settings.ApiKey)
        ? null
        : Protect(settings.ApiKey.Trim())
    };

    var json = JsonSerializer.Serialize(stored, JsonOptions);
    File.WriteAllText(_settingsFilePath, json);
  }

  private static string Protect(string plainText)
  {
    var plainBytes = Encoding.UTF8.GetBytes(plainText);
    var protectedBytes = ProtectedData.Protect(plainBytes, null, DataProtectionScope.CurrentUser);
    return Convert.ToBase64String(protectedBytes);
  }

  private static string Unprotect(string protectedText)
  {
    var protectedBytes = Convert.FromBase64String(protectedText);
    var plainBytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
    return Encoding.UTF8.GetString(plainBytes);
  }

  private sealed class StoredSettings
  {
    public string? ProtectedApiKey { get; set; }
  }
}
