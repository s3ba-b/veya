using Sage.Daemon;
using Sage.Shared.Inference;
using Sage.Shared.Safety;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddSystemd();
builder.Services.AddSingleton<IDBusSessionConnector, DBusSessionConnector>();
builder.Services.AddSingleton<IAuditLog>(_ => new JsonLinesAuditLog(AuditPaths.DefaultDirectory()));
builder.Services.AddSingleton<IApiKeyProvider, EnvironmentApiKeyProvider>();
builder.Services.AddSingleton<IInferenceBackend>(sp =>
    new ClaudeBackend(sp.GetRequiredService<IApiKeyProvider>(), sp.GetRequiredService<IAuditLog>(), "claude-sonnet-4-6"));
builder.Services.AddSingleton<IModelRouter, ModelRouter>();
builder.Services.AddSingleton<Sage1Service>();
builder.Services.AddHostedService<DBusHostedService>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
