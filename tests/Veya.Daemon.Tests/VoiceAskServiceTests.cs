using Veya.Daemon.Voice;
using Veya.Shared.Inference;
using Veya.Shared.Permissions;
using Veya.Shared.Safety;
using Veya.Shared.Voice;
using Xunit;

namespace Veya.Daemon.Tests;

public class VoiceAskServiceTests
{
    private sealed class FixedGate(bool granted) : IPermissionGate
    {
        public PermissionSource? Source { get; private set; }
        public string? Requester { get; private set; }

        public Task<bool> CheckAsync(PermissionSource source, string requester, CancellationToken cancellationToken = default)
        {
            Source = source;
            Requester = requester;
            return Task.FromResult(granted);
        }
    }

    private sealed class FakeRecorder(string? path) : IAudioRecorder
    {
        public int CallCount { get; private set; }
        public TimeSpan? LastDuration { get; private set; }

        public Task<string?> RecordToFileAsync(TimeSpan maxDuration, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastDuration = maxDuration;
            return Task.FromResult(path);
        }
    }

    private sealed class FakeSpeechToText(string? transcript) : ISpeechToText
    {
        public int CallCount { get; private set; }

        public Task<string?> TranscribeAsync(string wavFilePath, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(transcript);
        }
    }

    private sealed class FakeTextToSpeech(bool succeeds = true) : ITextToSpeech
    {
        public int CallCount { get; private set; }
        public string? LastText { get; private set; }

        public Task<bool> SpeakAsync(string text, CancellationToken cancellationToken = default)
        {
            CallCount++;
            LastText = text;
            return Task.FromResult(succeeds);
        }
    }

    private sealed class FakeModelRouter(Func<string, Task<string>> respond) : IModelRouter
    {
        public Task<string> AskAsync(string prompt, CancellationToken cancellationToken = default) => respond(prompt);
    }

    private sealed class RecordingAuditLog : IAuditLog
    {
        public List<AuditEvent> Events { get; } = [];

        public Task WriteAsync(AuditEvent auditEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(auditEvent);
            return Task.CompletedTask;
        }
    }

    private static VoiceAskService Service(
        IPermissionGate gate,
        IAudioRecorder recorder,
        ISpeechToText speechToText,
        ITextToSpeech textToSpeech,
        IModelRouter modelRouter,
        IAuditLog auditLog,
        VoiceOptions? options = null) =>
        new(gate, recorder, speechToText, textToSpeech, modelRouter, auditLog, options ?? new VoiceOptions());

    [Fact]
    public async Task AskAsync_WhenDenied_DoesNotRecordAndReportsRefusal()
    {
        var recorder = new FakeRecorder("/tmp/whatever.wav");
        var gate = new FixedGate(granted: false);
        var service = Service(gate, recorder, new FakeSpeechToText("hello"), new FakeTextToSpeech(), new FakeModelRouter(p => Task.FromResult(p)), new RecordingAuditLog());

        var (transcript, reply) = await service.AskAsync(8000);

        Assert.Equal(string.Empty, transcript);
        Assert.Contains("not granted", reply);
        Assert.Equal(0, recorder.CallCount);
        Assert.Equal(PermissionSource.Microphone, gate.Source);
        Assert.Equal("ask_voice", gate.Requester);
    }

    [Fact]
    public async Task AskAsync_WhenRecordingFails_ReportsFailureAndAuditsUnsuccessful()
    {
        var recorder = new FakeRecorder(path: null);
        var auditLog = new RecordingAuditLog();
        var service = Service(new FixedGate(true), recorder, new FakeSpeechToText("hello"), new FakeTextToSpeech(), new FakeModelRouter(p => Task.FromResult(p)), auditLog);

        var (transcript, reply) = await service.AskAsync(8000);

        Assert.Equal(string.Empty, transcript);
        Assert.Contains("couldn't hear", reply);

        var captureEvent = Assert.Single(auditLog.Events);
        Assert.Equal("voice.capture", captureEvent.EventType);
        Assert.Equal(false, captureEvent.Fields["success"]);
    }

    [Fact]
    public async Task AskAsync_WhenTranscriptionEmpty_ReportsFailureAndDeletesTempFile()
    {
        var path = Path.GetTempFileName();
        var auditLog = new RecordingAuditLog();
        var service = Service(new FixedGate(true), new FakeRecorder(path), new FakeSpeechToText(transcript: "   "), new FakeTextToSpeech(), new FakeModelRouter(p => Task.FromResult(p)), auditLog);

        var (transcript, reply) = await service.AskAsync(8000);

        Assert.Equal(string.Empty, transcript);
        Assert.Contains("didn't catch", reply);
        Assert.False(File.Exists(path));
        Assert.Contains(auditLog.Events, e => e.EventType == "voice.capture" && Equals(e.Fields["success"], false));
    }

    [Fact]
    public async Task AskAsync_HappyPath_RunsTranscriptThroughModelRouterAndSpeaksReply()
    {
        var path = Path.GetTempFileName();
        var recorder = new FakeRecorder(path);
        var textToSpeech = new FakeTextToSpeech();
        var auditLog = new RecordingAuditLog();
        var service = Service(
            new FixedGate(true), recorder, new FakeSpeechToText("what's the weather"), textToSpeech,
            new FakeModelRouter(prompt => Task.FromResult($"Veya heard: {prompt}")), auditLog);

        var (transcript, reply) = await service.AskAsync(8000);

        Assert.Equal("what's the weather", transcript);
        Assert.Equal("Veya heard: what's the weather", reply);
        Assert.False(File.Exists(path));

        Assert.Equal(1, textToSpeech.CallCount);
        Assert.Equal(reply, textToSpeech.LastText);

        Assert.Contains(auditLog.Events, e => e.EventType == "voice.capture" && Equals(e.Fields["success"], true) && Equals(e.Fields["transcriptLength"], transcript.Length));
        Assert.Contains(auditLog.Events, e => e.EventType == "voice.speak" && Equals(e.Fields["success"], true) && Equals(e.Fields["textLength"], reply.Length));
    }

    [Fact]
    public async Task AskAsync_WhenTextToSpeechFails_StillReturnsReply()
    {
        var path = Path.GetTempFileName();
        var auditLog = new RecordingAuditLog();
        var service = Service(
            new FixedGate(true), new FakeRecorder(path), new FakeSpeechToText("hello"), new FakeTextToSpeech(succeeds: false),
            new FakeModelRouter(prompt => Task.FromResult("a reply")), auditLog);

        var (_, reply) = await service.AskAsync(8000);

        Assert.Equal("a reply", reply);
        Assert.Contains(auditLog.Events, e => e.EventType == "voice.speak" && Equals(e.Fields["success"], false));
    }

    [Fact]
    public async Task AskAsync_WhenBackendUnavailable_ReturnsErrorMessageInsteadOfThrowing()
    {
        var path = Path.GetTempFileName();
        var service = Service(
            new FixedGate(true), new FakeRecorder(path), new FakeSpeechToText("hello"), new FakeTextToSpeech(),
            new FakeModelRouter(_ => throw new BackendUnavailableException("no API key configured")), new RecordingAuditLog());

        var (_, reply) = await service.AskAsync(8000);

        Assert.Contains("no API key configured", reply);
    }

    [Fact]
    public async Task AskAsync_ClampsRequestedDurationToConfiguredMaximum()
    {
        var recorder = new FakeRecorder(Path.GetTempFileName());
        var service = Service(
            new FixedGate(true), recorder, new FakeSpeechToText("hello"), new FakeTextToSpeech(),
            new FakeModelRouter(p => Task.FromResult(p)), new RecordingAuditLog(), new VoiceOptions { MaxRecordingMs = 5000 });

        await service.AskAsync(60_000);

        Assert.Equal(TimeSpan.FromMilliseconds(5000), recorder.LastDuration);
    }
}
