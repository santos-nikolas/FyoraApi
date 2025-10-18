using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FyoraApi.Data;

var builder = WebApplication.CreateBuilder(args);

// ------------------------- Services -------------------------

builder.Services.AddControllers();

// Swagger: documento sempre, UI em Dev ou flag
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "FyoraApi",
        Version = "v1",
        Description = "ASP.NET Core 8 Web API • EF Core (SQLite) • Swagger • Gemini (REST)"
    });
});

// ===== Connection string resiliente (local x Azure) =====
string? connFromConfig = builder.Configuration.GetConnectionString("DefaultConnection");

// Heurística: estamos no Azure App Service se a env var WEBSITE_SITE_NAME existir
bool runningOnAzure = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME"));

string connStr = !string.IsNullOrWhiteSpace(connFromConfig)
    ? connFromConfig!
    : runningOnAzure
        // Azure: área persistente gravável
        ? "Data Source=/home/site/wwwroot/app_data/fyora_api.db"
        // Local: arquivo no diretório do app
        : "Data Source=fyora_api.db";

builder.Services.AddDbContext<FyoraContext>(opt => opt.UseSqlite(connStr));

// CORS (opcional) — defina Cors__AllowedOrigins__0, __1... no Azure
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
if (allowedOrigins is { Length: > 0 })
{
    builder.Services.AddCors(opt =>
    {
        opt.AddPolicy("AllowConfiguredOrigins", p =>
            p.WithOrigins(allowedOrigins)
             .AllowAnyHeader()
             .AllowAnyMethod());
    });
}

// HttpClient nomeado para a API do Gemini
builder.Services.AddHttpClient("gemini", client =>
{
    client.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

// ------------------------- Pipeline -------------------------

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

// Swagger JSON sempre; UI em Dev ou flag
app.UseSwagger();
bool swaggerEnabled = app.Configuration.GetValue("Swagger:Enabled", false);
if (app.Environment.IsDevelopment() || swaggerEnabled)
{
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fyora API v1");
        // Manter UI do Swagger em /swagger (não ocupar a raiz)
    });
}

app.UseHttpsRedirection();

// Servir wwwroot (index.html abre em "/")
app.UseDefaultFiles(new DefaultFilesOptions { DefaultFileNames = { "index.html" } });
app.UseStaticFiles();

// CORS (se configurado)
if (allowedOrigins is { Length: > 0 })
    app.UseCors("AllowConfiguredOrigins");

app.MapControllers();

// Health simples
app.MapGet("/health", () => Results.Ok("healthy"));

// Criar o banco na primeira execução (sem derrubar o app se falhar)
try
{
    using var scope = app.Services.CreateScope();
    var ctx = scope.ServiceProvider.GetRequiredService<FyoraContext>();
    ctx.Database.EnsureCreated();
}
catch (Exception ex)
{
    // Loga no console do App Service / Log Stream e segue
    Console.Error.WriteLine($"[EF EnsureCreated] Falhou: {ex.GetType().Name}: {ex.Message}");
    // Em produção você pode remover EnsureCreated e usar migrações.
}

app.Run();
