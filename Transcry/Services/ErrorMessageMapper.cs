using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.Json;

namespace Transcry.Services;

public static class ErrorMessageMapper
{
  public const string RateLimitMessage =
    "Too many requests in a short time. Wait a few minutes and try again.";

  public const string QuotaExhaustedMessage =
    "OpenAI quota or credit is exhausted. Open platform.openai.com, check Billing and Usage, then add credit or raise the spending limit. Waiting will not fix this error.";

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
      return "Transcription cancelled.";
    }

    if (exception is HttpRequestException httpException)
    {
      return MapHttpException(httpException);
    }

    if (exception is SocketException)
    {
      return "No network connection. Check your Internet connection and try again.";
    }

    if (exception is UnauthorizedAccessException)
    {
      return "Access to the file was denied. Check permissions or choose another folder.";
    }

    if (exception is IOException ioException)
    {
      return $"File read/write error: {ioException.Message}";
    }

    return $"An unexpected error occurred: {exception.Message}";
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
        "Invalid or expired API key. Check Settings and try again.",
      HttpStatusCode.Forbidden =>
        "Access to the OpenAI service was denied. Check your account permissions.",
      HttpStatusCode.TooManyRequests =>
        RateLimitMessage,
      HttpStatusCode.BadRequest =>
        "Invalid request. Check the audio file and try again.",
      HttpStatusCode.RequestEntityTooLarge =>
        "The file is too large for Whisper (25 MB maximum).",
      HttpStatusCode.InternalServerError or HttpStatusCode.BadGateway or HttpStatusCode.ServiceUnavailable or HttpStatusCode.GatewayTimeout =>
        "The OpenAI service is temporarily unavailable. Try again shortly.",
      null =>
        "Could not reach the OpenAI service. Check your Internet connection.",
      _ =>
        $"OpenAI service error ({(int)statusCode}): {exception.Message}"
    };
  }

  private static string MapOpenAiMessage(string apiMessage, HttpStatusCode? statusCode)
  {
    if (apiMessage.Contains("invalid_api_key", StringComparison.OrdinalIgnoreCase) ||
        apiMessage.Contains("Incorrect API key", StringComparison.OrdinalIgnoreCase))
    {
      return "Invalid API key. Check Settings and try again.";
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
