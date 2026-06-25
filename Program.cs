using Microsoft.EntityFrameworkCore;
using PaymentProcessor.Channels;
using PaymentProcessor.Data;
using PaymentProcessor.Services;
using Polly;
using Polly.Extensions.Http;
using Serilog;
using Serilog.Formatting.Compact;

var builder = WebApplication.CreateBuilder(args);

// ──────────────────────────────────────────────
// Serilog: Logging estructurado en JSON
// ──────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq("http://localhost:5341")
    .CreateLogger();

builder.Host.UseSerilog();

// ──────────────────────────────────────────────
// DbContext con Azure SQL Server
// ──────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ConnectionData"),
        sqlOptions => sqlOptions.EnableRetryOnFailure(3)));

// ──────────────────────────────────────────────
// HttpClient "AcquirerClient" con política de resiliencia Polly
// ──────────────────────────────────────────────
builder.Services.AddHttpClient("AcquirerClient", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Acquirer:BaseUrl"] ?? "http://localhost:5001/");
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddPolicyHandler(GetRetryPolicy())
.AddPolicyHandler(GetCircuitBreakerPolicy());

// ──────────────────────────────────────────────
// Registro de interfaces e implementaciones
// ──────────────────────────────────────────────
builder.Services.AddSingleton<IPaymentChannel, PaymentChannel>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();   // Repositorio (acceso a datos)
builder.Services.AddScoped<IPaymentService, PaymentService>();         // Servicio de negocio
builder.Services.AddScoped<IAcquirerIntegrationService, AcquirerIntegrationService>();
builder.Services.AddScoped<IPaymentQueryService, PaymentQueryService>();
builder.Services.AddSingleton<PaymentRequestValidator>();
builder.Services.AddHostedService<PaymentWorkerService>();

// ──────────────────────────────────────────────
// Swagger / OpenAPI
// ──────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Payment Processor API",
        Version = "v1",
        Description = "API REST para procesamiento asíncrono de pagos con adquirente externo."
    });
});

var app = builder.Build();

// ──────────────────────────────────────────────
// Middleware pipeline
// ──────────────────────────────────────────────
app.UseSerilogRequestLogging(); // Log JSON de cada request HTTP

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

// Aplicar migraciones al iniciar (solo para desarrollo/desafío)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.Migrate();
}

Log.Information("Payment Processor API starting...");
app.Run();

// ──────────────────────────────────────────────
// Políticas de resiliencia
// ──────────────────────────────────────────────

static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError() // Maneja HttpRequestException, 5xx, 408
        .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.RequestTimeout)
        .WaitAndRetryAsync(
            retryCount: 3,
            sleepDurationProvider: retryAttempt =>
                TimeSpan.FromMilliseconds(100 * Math.Pow(2, retryAttempt)), // Exponential backoff
            onRetry: (outcome, timespan, retryAttempt, context) =>
            {
                Log.Warning(
                    "Retry {RetryAttempt}/3 after {Delay}ms due to {StatusCode}",
                    retryAttempt, timespan.TotalMilliseconds,
                    (int)outcome.Result.StatusCode);
            });
}

static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
{
    return HttpPolicyExtensions
        .HandleTransientHttpError()
        .CircuitBreakerAsync(
            handledEventsAllowedBeforeBreaking: 5,
            durationOfBreak: TimeSpan.FromSeconds(30));
}
