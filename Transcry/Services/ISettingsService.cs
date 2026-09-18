using Transcry.Models;

namespace Transcry.Services;

public interface ISettingsService
{
  AppSettings Load();

  void Save(AppSettings settings);

  bool HasApiKey { get; }
}
