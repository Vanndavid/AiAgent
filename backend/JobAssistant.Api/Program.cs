using Npgsql;

static string? FindEnvFile(string startDir)
{
    var dir = new DirectoryInfo(startDir);
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, ".env");
        if (File.Exists(candidate) && File.Exists(Path.Combine(dir.FullName, "docker-compose.yml")))
            return candidate;
        dir = dir.Parent;
    }
    return null;
}

var envFile = FindEnvFile(Directory.GetCurrentDirectory());
if (envFile is not null)
{
    foreach (var line in File.ReadAllLines(envFile))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
            continue;
        var eq = trimmed.IndexOf('=');
        if (eq <= 0) continue;
        var key = trimmed[..eq].Trim();
        var val = trimmed[(eq + 1)..].Trim();
        Environment.SetEnvironmentVariable(key, val);
    }
}

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();
var logger = app.Logger;

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

var pgConnectionString = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? "Host=127.0.0.1;Port=5432;Username=jobassistant;Password=jobassistant;Database=jobassistant";
var ragBaseUrl = builder.Configuration["Services:RagApiBaseUrl"] ?? "http://localhost:8001";
logger.LogInformation("Postgres connection: {ConnStr}", pgConnectionString);
logger.LogInformation("RAG base URL: {RagUrl}", ragBaseUrl);

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "job-assistant-api" }))
    .WithOpenApi();

app.MapGet("/api/stack-status", async (IHttpClientFactory httpFactory) =>
    {
        var postgresOk = false;
        string? postgresError = null;
        try
        {
            await using var conn = new NpgsqlConnection(pgConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT 1;", conn);
            await cmd.ExecuteScalarAsync();
            postgresOk = true;
        }
        catch (Exception ex)
        {
            postgresError = ex.Message;
            logger.LogWarning(ex, "Postgres health check failed");
        }

        var ragOk = false;
        string? ragError = null;
        try
        {
            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var resp = await client.GetAsync($"{ragBaseUrl.TrimEnd('/')}/health");
            ragOk = resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            ragError = ex.Message;
            logger.LogWarning(ex, "RAG API health check failed");
        }

        return Results.Ok(new
        {
            api = true,
            postgres = postgresOk,
            postgresError,
            ragApi = ragOk,
            ragError,
        });
    })
    .WithName("StackStatus")
    .WithOpenApi();

app.Run();
