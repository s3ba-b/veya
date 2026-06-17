using Microsoft.Extensions.Logging;
using Veya.Shared.Voice;
using Whisper.net;

namespace Veya.Daemon.Voice;

/// <summary>
/// Transcribes recorded audio with a local Whisper model via Whisper.net
/// (ADR-0015) — in-process, no subprocess, no audio or text leaves the
/// machine. The model is fetched separately (<c>scripts/download-whisper-model.sh</c>);
/// a missing model file degrades to "transcription unavailable" rather than
/// crashing, the same status as a missing <c>tesseract</c> binary (ADR-0013).
/// </summary>
public sealed class WhisperNetTranscriber : ISpeechToText, IDisposable
{
    private readonly string _modelPath;
    private readonly ILogger<WhisperNetTranscriber> _logger;
    private readonly Lazy<WhisperFactory?> _factory;

    public WhisperNetTranscriber(VoiceOptions options, ILogger<WhisperNetTranscriber> logger)
    {
        _modelPath = options.WhisperModelPath;
        _logger = logger;
        _factory = new Lazy<WhisperFactory?>(CreateFactory);
    }

    public async Task<string?> TranscribeAsync(string wavFilePath, CancellationToken cancellationToken = default)
    {
        var factory = _factory.Value;
        if (factory is null)
        {
            return null;
        }

        using var processor = factory.CreateBuilder().WithLanguage("auto").Build();
        await using var stream = File.OpenRead(wavFilePath);

        var text = new System.Text.StringBuilder();
        await foreach (var segment in processor.ProcessAsync(stream, cancellationToken))
        {
            text.Append(segment.Text);
        }

        return text.ToString().Trim();
    }

    private WhisperFactory? CreateFactory()
    {
        if (!File.Exists(_modelPath))
        {
            _logger.LogWarning(
                "Whisper model not found at {ModelPath}; voice transcription is disabled until it is fetched (scripts/download-whisper-model.sh).",
                _modelPath);
            return null;
        }

        return WhisperFactory.FromPath(_modelPath);
    }

    public void Dispose()
    {
        if (_factory.IsValueCreated)
        {
            _factory.Value?.Dispose();
        }
    }
}
