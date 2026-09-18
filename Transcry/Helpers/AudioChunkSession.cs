using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Transcry.Helpers;

public sealed class AudioChunkSession : IDisposable
{
  public const long MaxChunkSizeBytes = 24 * 1024 * 1024;
  private const int OutputSampleRate = 16000;
  private const int OutputBitsPerSample = 16;
  private const int OutputChannels = 1;
  private static readonly int OutputBytesPerSecond =
    OutputSampleRate * OutputChannels * (OutputBitsPerSample / 8);

  private readonly string? _tempDirectory;

  private AudioChunkSession(IReadOnlyList<string> chunkPaths, string? tempDirectory)
  {
    ChunkPaths = chunkPaths;
    _tempDirectory = tempDirectory;
  }

  public IReadOnlyList<string> ChunkPaths { get; }

  public bool WasSplit => _tempDirectory is not null;

  public static AudioChunkSession Create(string inputPath, CancellationToken cancellationToken)
  {
    var fileInfo = new FileInfo(inputPath);
    if (fileInfo.Length <= MaxChunkSizeBytes)
    {
      return new AudioChunkSession(new[] { inputPath }, tempDirectory: null);
    }

    var tempDirectory = Path.Combine(Path.GetTempPath(), "Transcry", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(tempDirectory);

    try
    {
      var chunks = SplitIntoWaveChunks(inputPath, tempDirectory, cancellationToken);
      return new AudioChunkSession(chunks, tempDirectory);
    }
    catch
    {
      TryDeleteDirectory(tempDirectory);
      throw;
    }
  }

  public void Dispose()
  {
    if (_tempDirectory is not null)
    {
      TryDeleteDirectory(_tempDirectory);
    }
  }

  public static TimeSpan GetMaxChunkDuration()
  {
    var maxSeconds = (MaxChunkSizeBytes / (double)OutputBytesPerSecond) * 0.92;
    return TimeSpan.FromSeconds(maxSeconds);
  }

  private static IReadOnlyList<string> SplitIntoWaveChunks(
    string inputPath,
    string tempDirectory,
    CancellationToken cancellationToken)
  {
    using var probeReader = new AudioFileReader(inputPath);
    var totalTime = probeReader.TotalTime;
    if (totalTime <= TimeSpan.Zero)
    {
      throw new InvalidOperationException("Could not determine the audio file duration.");
    }

    var maxChunkDuration = GetMaxChunkDuration();
    var chunks = new List<string>();
    var start = TimeSpan.Zero;
    var index = 0;

    while (start < totalTime)
    {
      cancellationToken.ThrowIfCancellationRequested();

      var duration = maxChunkDuration;
      if (start + duration > totalTime)
      {
        duration = totalTime - start;
      }

      if (duration <= TimeSpan.FromMilliseconds(100))
      {
        break;
      }

      var chunkPath = Path.Combine(tempDirectory, $"chunk_{index:D3}.wav");
      EncodeWaveSegment(inputPath, chunkPath, start, duration);
      ValidateChunk(chunkPath, index);
      chunks.Add(chunkPath);

      start += duration;
      index++;
    }

    if (chunks.Count == 0)
    {
      throw new InvalidOperationException("No audio parts were generated from the selected file.");
    }

    return chunks;
  }

  private static void EncodeWaveSegment(
    string inputPath,
    string outputPath,
    TimeSpan start,
    TimeSpan duration)
  {
    using var reader = new AudioFileReader(inputPath);
    var segment = new OffsetSampleProvider(reader.ToSampleProvider())
    {
      SkipOver = start,
      Take = duration
    };

    ISampleProvider prepared = segment.WaveFormat.Channels > 1
      ? new StereoToMonoSampleProvider(segment)
      : segment;

    var resampled = new WdlResamplingSampleProvider(prepared, OutputSampleRate);
    WaveFileWriter.CreateWaveFile16(outputPath, resampled);
  }

  private static void ValidateChunk(string chunkPath, int chunkIndex)
  {
    var fileInfo = new FileInfo(chunkPath);
    if (fileInfo.Length < 1024)
    {
      throw new InvalidOperationException(
        $"Part {chunkIndex + 1} is empty or too small to transcribe.");
    }

    if (fileInfo.Length > MaxChunkSizeBytes)
    {
      throw new InvalidOperationException(
        $"Part {chunkIndex + 1} still exceeds the 25 MB limit after preparation.");
    }

    try
    {
      using var reader = new AudioFileReader(chunkPath);
      if (reader.TotalTime < TimeSpan.FromMilliseconds(500))
      {
        throw new InvalidOperationException(
          $"Part {chunkIndex + 1} does not contain enough audio.");
      }
    }
    catch (InvalidOperationException)
    {
      throw;
    }
    catch (Exception ex)
    {
      throw new InvalidOperationException(
        $"Part {chunkIndex + 1} is not a valid audio file.", ex);
    }
  }

  private static void TryDeleteDirectory(string directory)
  {
    try
    {
      if (Directory.Exists(directory))
      {
        Directory.Delete(directory, recursive: true);
      }
    }
    catch
    {
      // Best-effort cleanup for temp chunk files.
    }
  }
}
