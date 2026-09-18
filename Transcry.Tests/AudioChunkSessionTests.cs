using NAudio.Wave;
using Transcry.Helpers;

namespace Transcry.Tests;

public class AudioChunkSessionTests
{
  public static IEnumerable<object[]> LargeMp3Files()
  {
    var sampleDirectory = ResolveSampleDirectory();
    if (sampleDirectory is null)
    {
      yield break;
    }

    foreach (var file in Directory.EnumerateFiles(sampleDirectory, "*.mp3"))
    {
      var info = new FileInfo(file);
      if (info.Length > AudioChunkSession.MaxChunkSizeBytes)
      {
        yield return new object[] { file };
      }
    }
  }

  [Theory]
  [MemberData(nameof(LargeMp3Files))]
  public void Create_splits_large_mp3_into_valid_wave_chunks_under_limit(string filePath)
  {
    using var session = AudioChunkSession.Create(filePath, CancellationToken.None);

    Assert.True(session.WasSplit);
    Assert.True(session.ChunkPaths.Count >= 2);

    foreach (var chunkPath in session.ChunkPaths)
    {
      Assert.Equal(".wav", Path.GetExtension(chunkPath), ignoreCase: true);

      var chunkInfo = new FileInfo(chunkPath);
      Assert.True(chunkInfo.Length > 1024);
      Assert.True(chunkInfo.Length <= AudioChunkSession.MaxChunkSizeBytes);

      using var reader = new AudioFileReader(chunkPath);
      Assert.Equal(16000, reader.WaveFormat.SampleRate);
      Assert.Equal(1, reader.WaveFormat.Channels);
      Assert.True(reader.TotalTime >= TimeSpan.FromSeconds(1));
    }
  }

  [Fact]
  public void Create_splits_first_large_mp3_when_local_samples_exist()
  {
    var sampleDirectory = ResolveSampleDirectory();
    if (sampleDirectory is null)
    {
      return;
    }

    var filePath = Directory.EnumerateFiles(sampleDirectory, "*.mp3")
      .Select(path => new FileInfo(path))
      .Where(info => info.Length > AudioChunkSession.MaxChunkSizeBytes)
      .OrderBy(info => info.Name, StringComparer.OrdinalIgnoreCase)
      .FirstOrDefault()?
      .FullName;

    if (filePath is null)
    {
      return;
    }

    using var session = AudioChunkSession.Create(filePath, CancellationToken.None);

    Assert.True(session.WasSplit);
    Assert.True(session.ChunkPaths.Count >= 2);

    var totalChunkBytes = session.ChunkPaths.Sum(path => new FileInfo(path).Length);
    Assert.True(totalChunkBytes > 0);
  }

  [Fact]
  public void GetMaxChunkDuration_targets_safe_size_for_whisper()
  {
    var duration = AudioChunkSession.GetMaxChunkDuration();

    var estimatedBytes = duration.TotalSeconds * 16000 * 2;
    Assert.True(estimatedBytes < AudioChunkSession.MaxChunkSizeBytes);
    Assert.True(duration.TotalMinutes >= 10);
  }

  private static string? ResolveSampleDirectory()
  {
    var fromEnv = Environment.GetEnvironmentVariable("TRANSCRY_TEST_AUDIO_DIR");
    if (!string.IsNullOrWhiteSpace(fromEnv) && Directory.Exists(fromEnv))
    {
      return fromEnv;
    }

    var localSamples = Path.GetFullPath(
      Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "local-samples"));
    return Directory.Exists(localSamples) ? localSamples : null;
  }
}
