namespace Transcry.Helpers;

public static class AudioFileValidator
{
  public const long WhisperApiMaxBytes = 25 * 1024 * 1024;

  private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
  {
    ".mp3", ".mp4", ".mpeg", ".mpga", ".m4a", ".wav", ".webm"
  };

  public static string SupportedFormatsDescription =>
    "mp3, mp4, mpeg, mpga, m4a, wav, webm";

  public static bool ExceedsWhisperLimit(string filePath) =>
    new FileInfo(filePath).Length > WhisperApiMaxBytes;

  public static double GetFileSizeMegabytes(string filePath) =>
    new FileInfo(filePath).Length / (1024.0 * 1024.0);

  public static bool TryValidate(string filePath, out string? errorMessage)
  {
    errorMessage = null;

    if (string.IsNullOrWhiteSpace(filePath))
    {
      errorMessage = "Seleziona un file audio prima di procedere.";
      return false;
    }

    if (!File.Exists(filePath))
    {
      errorMessage = "Il file selezionato non esiste o non è più accessibile.";
      return false;
    }

    var extension = Path.GetExtension(filePath);
    if (string.IsNullOrEmpty(extension) || !SupportedExtensions.Contains(extension))
    {
      errorMessage =
        $"Formato non supportato ({extension.TrimStart('.')}). " +
        $"Whisper accetta solo: {SupportedFormatsDescription}. " +
        "I file .wma non sono supportati: convertili prima in mp3 o wav.";
      return false;
    }

    try
    {
      var fileInfo = new FileInfo(filePath);
      if (fileInfo.Length == 0)
      {
        errorMessage = "Il file audio è vuoto.";
        return false;
      }
    }
    catch (Exception ex)
    {
      errorMessage = $"Impossibile leggere il file: {ex.Message}";
      return false;
    }

    return true;
  }
}
