using Npgsql;

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

var pgConnectionString = builder.Configuration.GetConnectionString("PostgreSQL")
    ?? "Host=localhost;Port=5432;Username=jobassistant;Password=jobassistant;Database=jobassistant";
var ragBaseUrl = builder.Configuration["Services:RagApiBaseUrl"] ?? "http://localhost:8001";

app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "job-assistant-api" }))
    .WithOpenApi();

app.MapGet("/api/stack-status", async (IHttpClientFactory httpFactory) =>
    {
        var postgresOk = false;
        try
        {
            await using var conn = new NpgsqlConnection(pgConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT 1;", conn);
            await cmd.ExecuteScalarAsync();
            postgresOk = true;
        }
        catch
        {
            postgresOk = false;
        }

        var ragOk = false;
        try
        {
            var client = httpFactory.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var resp = await client.GetAsync($"{ragBaseUrl.TrimEnd('/')}/health");
            ragOk = resp.IsSuccessStatusCode;
        }
        catch
        {
            ragOk = false;
        }

        return Results.Ok(new
        {
            api = true,
            postgres = postgresOk,
            ragApi = ragOk,
        });
    })
    .WithName("StackStatus")
    .WithOpenApi();

app.Run();
