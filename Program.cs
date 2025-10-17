using FyoraApi.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ------------------------------
// Services
// ------------------------------

// EF Core + SQLite
builder.Services.AddDbContext<FyoraContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// Controllers + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Fyora API", Version = "v1" });
});

// HttpClient para chamadas REST ao Gemini (timeout básico)
builder.Services.AddHttpClient().ConfigureHttpClientDefaults(_ =>
{
    // ajuste se quiser algo maior
    _.HttpClient.Timeout = TimeSpan.FromSeconds(20);
});

// CORS via appsettings.json (opcional para front separado)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
if (allowedOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("LocalDev", policy =>
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod());
    });
}

var app = builder.Build();

// ------------------------------
// Pipeline
// ------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Fyora API v1"));
}

app.UseHttpsRedirection();

if (allowedOrigins.Length > 0)
{
    app.UseCors("LocalDev");
}

app.UseAuthorization();

app.MapControllers();

// Healthcheck simples (para automação/monitoramento)
app.MapGet("/health", () => Results.Ok("healthy"));

// ------------------------------
// DB bootstrap: cria o banco na primeira execução
// ------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<FyoraContext>();
    context.Database.EnsureCreated();
}

app.Run();
