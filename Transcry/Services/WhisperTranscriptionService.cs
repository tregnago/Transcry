using System.ClientModel;
using System.Net;
using OpenAI.Audio;
using Transcry.Helpers;

namespace Transcry.Services;

public sealed class WhisperTranscriptionService : ITranscriptionService
{
  private const string ModelName = "whisper-1";

  public async Task<string> TranscribeAsync(
    string audioFilePath,
    string apiKey,
    CancellationToken cancellationToken,
    IProgress<string>? progress = null)
  {
    if (string.IsNullOrWhiteSpace(apiKey))
    {
      throw new TranscriptionException(
        "API key is not configured. Open Settings and enter your OpenAI key.");
    }

    if (!AudioFileValidator.TryValidate(audioFilePath, out var validationError))
    {
      throw new TranscriptionException(validationError!);
    }

    if (AudioFileValidator.ExceedsWhisperLimit(audioFilePath))
    {
      progress?.Report(
        $"Preparing the file ({AudioFileValidator.GetFileSizeMegabytes(audioFilePath):F1} MB): " +
        "converting and splitting into WAV parts. Duration depends on the file size, format, and storage speed.");
    }

    AudioChunkSession chunkSession;
    try
    {
      chunkSession = AudioChunkSession.Create(audioFilePath, cancellationToken);
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (Exception ex)
    {
      throw new TranscriptionException(
        $"Could not prepare the audio file for transcription: {ex.Message}", ex);
    }

    using (chunkSession)
    {
      var chunks = chunkSession.ChunkPaths;

      if (chunkSession.WasSplit)
      {
        progress?.Report(
          $"Audio prepared in {chunks.Count} parts. Starting transcription...");
      }

      var transcriptParts = new List<string>(chunks.Count);

      for (var index = 0; index < chunks.Count; index++)
      {
        cancellationToken.ThrowIfCancellationRequested();

        if (chunks.Count > 1)
        {
          progress?.Report($"Transcribing part {index + 1} of {chunks.Count}...");
        }

        var part = await TranscribeSingleFileAsync(
          chunks[index],
          apiKey,
          cancellationToken,
          chunks.Count > 1 ? index + 1 : null,
          chunks.Count > 1 ? chunks.Count : null).ConfigureAwait(false);

        transcriptParts.Add(part);
      }

      var fullTranscript = string.Join(" ", transcriptParts).Trim();
      if (string.IsNullOrWhiteSpace(fullTranscript))
      {
        throw new TranscriptionException(
          "The transcription is empty. The file may not contain recognizable speech.");
      }

      return fullTranscript;
    }
  }

  private static async Task<string> TranscribeSingleFileAsync(
    string audioFilePath,
    string apiKey,
    CancellationToken cancellationToken,
    int? partNumber = null,
    int? partCount = null)
  {
    try
    {
      var client = new AudioClient(ModelName, apiKey);
      var options = new AudioTranscriptionOptions
      {
        ResponseFormat = AudioTranscriptionFormat.Text
      };

      await using var audioStream = File.OpenRead(audioFilePath);
      var fileName = Path.GetFileName(audioFilePath);

      ClientResult<AudioTranscription> result = await client
        .TranscribeAudioAsync(audioStream, fileName, options, cancellationToken)
        .ConfigureAwait(false);

      var transcription = result.Value;
      if (string.IsNullOrWhiteSpace(transcription.Text))
      {
        throw new TranscriptionException(
          "The transcription is empty. The file may not contain recognizable speech.");
      }

      return transcription.Text.Trim();
    }
    catch (TranscriptionException)
    {
      throw;
    }
    catch (OperationCanceledException)
    {
      throw;
    }
    catch (ClientResultException ex) when (ex.Status == (int)HttpStatusCode.Forbidden &&
                                             ex.Message.Contains("whisper", StringComparison.OrdinalIgnoreCase))
    {
      throw new TranscriptionException(
        "The OpenAI project linked to your API key does not have access to whisper-1. " +
        "Go to platform.openai.com, select the same project as the key, then Settings > Limits: " +
        "make sure whisper-1 is not blocked and is among the allowed models. " +
        "If the key is project-limited (sk-proj-...), create a new key after enabling whisper-1.",
        ex);
    }
    catch (ClientResultException ex) when (ex.Status == (int)HttpStatusCode.Unauthorized)
    {
      throw new TranscriptionException(
        "Invalid or expired API key. Check Settings and try again.", ex);
    }
    catch (ClientResultException ex) when (ex.Status == (int)HttpStatusCode.TooManyRequests)
    {
      throw new TranscriptionException(
        ErrorMessageMapper.FromTooManyRequests(GetClientErrorDetails(ex)), ex);
    }
    catch (ClientResultException ex) when (ex.Status == (int)HttpStatusCode.RequestEntityTooLarge)
    {
      throw new TranscriptionException(
        BuildPartErrorMessage(
          partNumber,
          partCount,
          "exceeds the 25 MB limit. Try compressing the audio file before transcription."),
        ex);
    }
    catch (ClientResultException ex) when (ex.Status == (int)HttpStatusCode.BadRequest &&
                                             ex.Message.Contains("decoded", StringComparison.OrdinalIgnoreCase))
    {
      throw new TranscriptionException(
        BuildPartErrorMessage(
          partNumber,
          partCount,
          "cannot be decoded by Whisper. The original file may be damaged or in a non-standard format."),
        ex);
    }
    catch (ClientResultException ex) when (ex.Status >= 500)
    {
      throw new TranscriptionException(
        "The OpenAI service is temporarily unavailable. Try again shortly.", ex);
    }
    catch (ClientResultException ex)
    {
      throw new TranscriptionException(
        string.IsNullOrWhiteSpace(ex.Message)
          ? "An error occurred from the OpenAI service during transcription."
          : ex.Message,
        ex);
    }
    catch (Exception ex)
    {
      var message = ErrorMessageMapper.ToUserMessage(ex);
      if (partNumber is not null && partCount is not null)
      {
        message = BuildPartErrorMessage(partNumber, partCount, message);
      }

      throw new TranscriptionException(message, ex);
    }
  }

  private static string? GetClientErrorDetails(ClientResultException exception)
  {
    try
    {
      var content = exception.GetRawResponse()?.Content?.ToString();
      if (!string.IsNullOrWhiteSpace(content))
      {
        return string.IsNullOrWhiteSpace(exception.Message)
          ? content
          : $"{exception.Message} {content}";
      }
    }
    catch (Exception)
    {
      // Fall back to the exception message if the raw body cannot be read.
    }

    return exception.Message;
  }

  private static string BuildPartErrorMessage(int? partNumber, int? partCount, string detail)
  {
    if (partNumber is null || partCount is null)
    {
      return detail;
    }

    return $"Error on part {partNumber} of {partCount}: {detail}";
  }
}
