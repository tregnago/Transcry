# Transcry

Software Windows per trascrivere file audio con l'API **Whisper** di OpenAI.

## Requisiti

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Chiave API OpenAI con accesso all'API audio

## Avvio

```bash
cd Transcry
dotnet run
```

Per creare un eseguibile:

```bash
dotnet publish Transcry/Transcry.csproj -c Release -r win-x64 --self-contained false
```

L'eseguibile si trova in `Transcry/bin/Release/net8.0-windows/win-x64/publish/`.

## Utilizzo

1. Apri **Impostazioni** e inserisci la tua chiave API OpenAI (inizia con `sk-`).
2. Clicca **Sfoglia…** e seleziona un file audio.
3. Clicca **Trascrivere** e attendi il completamento.
4. Clicca **Salva testo…** per esportare la trascrizione in un file `.txt`.

## Formati supportati

Whisper accetta: **mp3, mp4, mpeg, mpga, m4a, wav, webm**.

L'API ha un limite di **25 MB per richiesta**. Transcry suddivide automaticamente i file più grandi convertendoli in parti **WAV 16 kHz mono** sotto il limite, le trascrive e ricompone il testo.

## Test

```bash
dotnet test Transcry.Tests/Transcry.Tests.csproj --filter AudioChunkSession
```

I test di integrazione sui file grandi sono opzionali: copia gli MP3 in `Transcry.Tests/local-samples/` oppure imposta la variabile d'ambiente `TRANSCRY_TEST_AUDIO_DIR`. Quella cartella è esclusa da git.

> I file **.wma** non sono supportati dall'API Whisper. Convertili prima in mp3 o wav.

## Sicurezza

La chiave API viene salvata solo in `%AppData%\Transcry\settings.json`, crittografata con **DPAPI** (protezione a livello utente Windows). Non viene mai copiata nel repository git.
