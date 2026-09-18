using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

namespace Transcry.Services;

public static class ErrorMessageMapper
{
  public const string RateLimitMessage =
    "Troppe richieste in poco tempo. Attendi qualche minuto e riprova.";

  public const string QuotaExhaustedMessage =
    "Quota o credito OpenAI esaurito. Apri platform.openai.com, controlla Fatturazione e Usage, poi ricarica il credito o alza il limite di spesa. Attendere non risolve questo errore.";

  public static string FromTooManyRequests(string? details)
  {
    return LooksLikeQuotaExhausted(details)
      ? QuotaExhaustedMessage
      : RateLimitMessage;
  }

  public static string ToUserMessage(Exception exception)
  {
    if (exception is TranscriptionException transcriptionException)
    {
      return transcriptionException.UserMessage;
    }

    if (exception is OperationCanceledException)
    {
      return "Trascrizione annullata.";
    }

    if (exception is HttpRequestException httpException)
    {
      return MapHttpException(httpException);
    }

    if (exception is SocketException)
    {
      return "Connessione di rete non disponibile. Verifica la connessione Internet e riprova.";
    }

    if (exception is UnauthorizedAccessException)
    {
      return "Accesso negato al file. Verifica i permessi o scegli un'altra cartella.";
    }

    if (exception is IOException ioException)
    {
      return $"Errore di lettura/scrittura del file: {ioException.Message}";
    }

    return $"Si è verificato un errore imprevisto: {exception.Message}";
  }

  private static string MapHttpException(HttpRequestException exception)
  {
    var statusCode = exception.StatusCode;
    var responseBody = exception.Data["ResponseBody"] as string;

    if (!string.IsNullOrWhiteSpace(responseBody))
    {
      var apiMessage = TryExtractOpenAiMessage(responseBody);
      if (!string.IsNullOrWhiteSpace(apiMessage))
      {
        return MapOpenAiMessage(apiMessage, statusCode);
      }
    }

    return statusCode switch
    {
      HttpStatusCode.Unauthorized =>
        "Chiave API non valida o scaduta. Controlla le impostazioni e riprova.",
      HttpStatusCode.Forbidden =>
        "Accesso negato al servizio OpenAI. Verifica i permessi del tuo account.",
      HttpStatusCode.TooManyRequests =>
        RateLimitMessage,
      HttpStatusCode.BadRequest =>
        "Richiesta non valida. Verifica il file audio e riprova.",
      HttpStatusCode.RequestEntityTooLarge =>
        "Il file è troppo grande per il servizio Whisper (massimo 25 MB).",
      HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout =>
        "Il servizio OpenAI non è momentaneamente disponibile. Riprova tra poco.",
      null =>
        "Impossibile contattare il servizio OpenAI. Verifica la connessione Internet.",
      _ =>
        $"Errore dal servizio OpenAI ({(int)statusCode}): {exception.Message}"
    };
  }

  private static string MapOpenAiMessage(string apiMessage, HttpStatusCode? statusCode)
  {
    if (apiMessage.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase) ||
        apiMessage.Contains("Incorrect API key", StringComparison.OrdinalIgnoreCase))
    {
      return "Chiave API non valida. Controlla le impostazioni e riprova.";
    }

    if (LooksLikeQuotaExhausted(apiMessage))
    {
      return QuotaExhaustedMessage;
    }

    if (statusCode == HttpStatusCode.TooManyRequests)
    {
      return RateLimitMessage;
    }

    return apiMessage;
  }

  private static string? TryExtractOpenAiMessage(string responseBody)
  {
    try
    {
      using var document = JsonDocument.Parse(responseBody);
      if (document.RootElement.TryGetProperty("error", out var errorElement) &&
          errorElement.TryGetProperty("message", out var messageElement))
      {
        return messageElement.GetString();
      }
    }
    catch (JsonException)
    {
      // Ignore malformed JSON and fall back to generic mapping.
    }

    return null;
  }

  internal static bool LooksLikeQuotaExhausted(string? text)
  {
    if (string.IsNullOrWhiteSpace(text))
    {
      return false;
    }

    return text.Contains("insufficient_quota", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("credit_balance", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("exceeded your current quota", StringComparison.OrdinalIgnoreCase) ||
           text.Contains("billing_not_active", StringComparison.OrdinalIgnoreCase);
  }
}
