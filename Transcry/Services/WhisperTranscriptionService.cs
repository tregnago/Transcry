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
        "Chiave API non configurata. Apri Impostazioni e inserisci la tua chiave OpenAI.");
    }

    if (!AudioFileValidator.TryValidate(audioFilePath, out var validationError))
    {
      throw new TranscriptionException(validationError!);
    }

    if (AudioFileValidator.ExceedsWhisperLimit(audioFilePath))
    {
      progress?.Report(
        $"Preparazione del file ({AudioFileValidator.GetFileSizeMegabytes(audioFilePath):F1} MB): " +
        "conversione e suddivisione in parti WAV... puo richiedere circa 1 minuto.");
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
        $"Impossibile preparare il file audio per la trascrizione: {ex.Message}", ex);
    }

    using (chunkSession)
    {
      var chunks = chunkSession.ChunkPaths;

      if (chunkSession.WasSplit)
      {
        progress?.Report(
          $"File grande ({AudioFileValidator.GetFileSizeMegabytes(audioFilePath):F1} MB): " +
          $"suddivisione in {chunks.Count} parti per Whisper...");
      }

      var transcriptParts = new List<string>(chunks.Count);

      for (var index = 0; index < chunks.Count; index++)
      {
        cancellationToken.ThrowIfCancellationRequested();

        if (chunks.Count > 1)
        {
          progress?.Report($"Trascrizione parte {index + 1} di {chunks.Count}...");
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
          "La trascrizione è vuota. Il file potrebbe non contenere parlato riconoscibile.");
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
          "La trascrizione è vuota. Il file potrebbe non contenere parlato riconoscibile.");
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
        "Il progetto OpenAI collegato alla tua chiave API non ha accesso a whisper-1. " +
        "Vai su platform.openai.com ? seleziona lo stesso progetto della chiave ? Impostazioni ? Limiti: " +
        "verifica che whisper-1 non sia bloccato e che sia tra i modelli consentiti. " +
        "Se la chiave e' limitata (sk-proj-...), crea una nuova chiave dopo aver abilitato whisper-1.",
        ex);
    }
    catch (ClientResultException ex) when (ex.Status == (int)HttpStatusCode.Unauthorized)
    {
      throw new TranscriptionException(
        "Chiave API non valida o scaduta. Controlla le impostazioni e riprova.", ex);
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
          "supera il limite di 25 MB. Prova a comprimere il file audio prima della trascrizione."),
        ex);
    }
    catch (ClientResultException ex) when (ex.Status == (int)HttpStatusCode.BadRequest &&
                                             ex.Message.Contains("decoded", StringComparison.OrdinalIgnoreCase))
    {
      throw new TranscriptionException(
        BuildPartErrorMessage(
          partNumber,
          partCount,
          "non puo essere decodificata da Whisper. Il file originale potrebbe essere danneggiato o in un formato non standard."),
        ex);
    }
    catch (ClientResultException ex) when (ex.Status >= 500)
    {
      throw new TranscriptionException(
        "Il servizio OpenAI non è momentaneamente disponibile. Riprova tra poco.", ex);
    }
    catch (ClientResultException ex)
    {
      throw new TranscriptionException(
        string.IsNullOrWhiteSpace(ex.Message)
          ? "Errore dal servizio OpenAI durante la trascrizione."
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

    return $"Errore sulla parte {partNumber} di {partCount}: {detail}";
  }
}
