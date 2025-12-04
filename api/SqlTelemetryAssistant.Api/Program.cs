using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SqlTelemetryAssistant.Api.Models;
using SqlTelemetryAssistant.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

builder.Services.AddSingleton<InfluxTelemetryService>();
builder.Services.AddSingleton<OpenAiRecommendationProvider>();
builder.Services.AddSingleton<LocalLlmRecommendationProvider>();
builder.Services.AddSingleton<KnowledgeBaseService>();
builder.Services.AddSingleton<SqlServerTelemetryService>();
builder.Services.AddSingleton<AiSqlService>();


builder.Services.AddSingleton<IAiRecommendationService>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var provider = config["Ai:Provider"] ?? "Local";

    return provider switch
    {
        "OpenAI" => sp.GetRequiredService<OpenAiRecommendationProvider>(),
        _ => sp.GetRequiredService<LocalLlmRecommendationProvider>()
    };
});

var app = builder.Build();

    app.UseSwagger();
    app.UseSwaggerUI();


// statyczny frontend (wwwroot)
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/api", () => Results.Ok(new { status = "ok", message = "SQL Telemetry Assistant API (v6 with Console)" }));

// Stary endpoint – tylko telemetria + AI
app.MapGet("/telemetry/pulse", async (
    InfluxTelemetryService influx,
    IAiRecommendationService ai) =>
{
    var pulse = await influx.GetWaitStatsPulseAsync();
    var prompt = PromptFactory.BuildPrompt(pulse);
    var recommendation = await ai.GetRecommendationAsync(prompt);

    var response = new TelemetryResponse
    {
        GeneratedAtUtc = DateTime.UtcNow,
        Telemetry = pulse,
        Recommendation = recommendation
    };

    return Results.Ok(response);
});

// Nowy endpoint – telemetria + baza wiedzy (Markdown)
app.MapGet("/telemetry/pulse-kb", async (
    InfluxTelemetryService influx,
    IAiRecommendationService ai,
    KnowledgeBaseService kb) =>
{
    var pulse = await influx.GetWaitStatsPulseAsync();
    var kbFragments = kb.GetRelevantFragments(pulse);
    var prompt = PromptFactory.BuildPromptWithKnowledge(pulse, kbFragments);
    var recommendation = await ai.GetRecommendationAsync(prompt);

    var response = new TelemetryWithKnowledgeResponse
    {
        GeneratedAtUtc = DateTime.UtcNow,
        Telemetry = pulse,
        KnowledgeFragments = kbFragments.ToArray(),
        Recommendation = recommendation
    };

    return Results.Ok(response);
});
app.MapGet("/api/demo/sql-with-ai", async (
    SqlServerTelemetryService sqlTelemetry,
    OpenAiRecommendationProvider ai  // lub IAiRecommendationService, jeśli masz taki interfejs
) =>
{
    // 1) Pobieramy snapshot z SQL Servera
    var snapshot = await sqlTelemetry.GetBasicSnapshotAsync();

    // 2) Budujemy prompt
    var prompt = sqlTelemetry.BuildPrompt(snapshot);

    // 3) Pytamy OpenAI o rekomendację
    var recommendation = await ai.GetRecommendationAsync(prompt);

    // 4) Zwracamy JSON
    var result = new
    {
        Source = "SQL Server + OpenAI",
        Snapshot = snapshot,
        Recommendation = recommendation
    };

    return Results.Ok(result);
})
.WithName("SqlWithAiDemo");
app.MapPost("/api/aisql", async (
    AiSqlService aiSql,
    AiSqlRequest request) =>
{
    if (string.IsNullOrWhiteSpace(request.Question))
        return Results.BadRequest(new { error = "Question is required." });

    var response = await aiSql.HandleQuestionAsync(request.Question);
    return Results.Ok(response);
})
.WithName("AiSql");

app.Run();
