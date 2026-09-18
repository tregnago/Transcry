# Transcry

Windows software to transcribe audio files with the OpenAI **Whisper** API.

## Requirements

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An OpenAI API key with access to the audio API

## Getting started

```bash
cd Transcry
dotnet run
```

To build an executable:

```bash
dotnet publish Transcry/Transcry.csproj -c Release -r win-x64 --self-contained false
```

The executable is written to `Transcry/bin/Release/net8.0-windows/win-x64/publish/`.

## Usage

1. Open **Settings** and enter your OpenAI API key (it starts with `sk-`).
2. Click **Browse...** and select an audio file.
3. Click **Transcribe** and wait for the result.
4. Click **Save text...** to export the transcript as a `.txt` file.

## Supported formats

Whisper accepts: **mp3, mp4, mpeg, mpga, m4a, wav, webm**.

The API has a **25 MB per request** limit. Transcry automatically splits larger files into **16 kHz mono WAV** chunks under that limit, transcribes them, and joins the text.

## Tests

```bash
dotnet test Transcry.Tests/Transcry.Tests.csproj --filter AudioChunkSession
```

Integration tests on large files are optional: copy MP3 files into `Transcry.Tests/local-samples/` or set the `TRANSCRY_TEST_AUDIO_DIR` environment variable. That folder is excluded from git.

> **.wma** files are not supported by the Whisper API. Convert them to mp3 or wav first.

## Security

The API key is stored only in `%AppData%\Transcry\settings.json`, encrypted with **DPAPI** (Windows per-user protection). It is never committed to git.
