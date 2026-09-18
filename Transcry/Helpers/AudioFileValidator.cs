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
      errorMessage = "Select an audio file before continuing.";
      return false;
    }

    if (!File.Exists(filePath))
    {
      errorMessage = "The selected file does not exist or is no longer accessible.";
      return false;
    }

    var extension = Path.GetExtension(filePath);
    if (string.IsNullOrEmpty(extension) || !SupportedExtensions.Contains(extension))
    {
      errorMessage =
        $"Unsupported format ({extension.TrimStart('.')}). " +
        $"Whisper only accepts: {SupportedFormatsDescription}. " +
        ".wma files are not supported: convert them to mp3 or wav first.";
      return false;
    }

    try
    {
      var fileInfo = new FileInfo(filePath);
      if (fileInfo.Length == 0)
      {
        errorMessage = "The audio file is empty.";
        return false;
      }
    }
    catch (Exception ex)
    {
      errorMessage = $"Could not read the file: {ex.Message}";
      return false;
    }

    return true;
  }
}
