namespace Transcry.Services;

public sealed class TranscriptionException : Exception
{
  public TranscriptionException(string userMessage)
    : base(userMessage)
  {
    UserMessage = userMessage;
  }

  public TranscriptionException(string userMessage, Exception innerException)
    : base(userMessage, innerException)
  {
    UserMessage = userMessage;
  }

  public string UserMessage { get; }
}
