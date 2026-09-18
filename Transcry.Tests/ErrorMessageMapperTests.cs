using Transcry.Services;

namespace Transcry.Tests;

public class ErrorMessageMapperTests
{
  [Theory]
  [InlineData("insufficient_quota")]
  [InlineData("You exceeded your current quota, please check your plan and billing details.")]
  [InlineData("credit_balance_exhausted")]
  [InlineData("HTTP 429 (insufficient_quota) billing_not_active")]
  public void FromTooManyRequests_maps_quota_errors(string details)
  {
    Assert.Equal(
      ErrorMessageMapper.QuotaExhaustedMessage,
      ErrorMessageMapper.FromTooManyRequests(details));
  }

  [Theory]
  [InlineData(null)]
  [InlineData("")]
  [InlineData("Rate limit reached for whisper-1 on requests per minute")]
  public void FromTooManyRequests_keeps_rate_limit_message_otherwise(string? details)
  {
    Assert.Equal(
      ErrorMessageMapper.RateLimitMessage,
      ErrorMessageMapper.FromTooManyRequests(details));
  }
}
