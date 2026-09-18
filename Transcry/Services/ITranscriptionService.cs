namespace Transcry.Services;

public interface ITranscriptionService
{
  Task<string> TranscribeAsync(
    string audioFilePath,
    string apiKey,
    CancellationToken cancellationToken,
    IProgress<string>? progress = null);
}
