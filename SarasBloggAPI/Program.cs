using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;
using SarasBloggAPI.Services;
using SarasBloggAPI.DAL;
using Microsoft.AspNetCore.Identity;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.AspNetCore.HttpOverrides;
using Npgsql;
using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using HealthChecks.NpgSql;

namespace SarasBloggAPI
{
    public class Program
    {
        public static async Task Main(string[] args)   // 🔹 async för att kunna vänta in DB
        {
            var builder = WebApplication.CreateBuilder(args);

            // Render/containers: bind PORT från env
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

            // Hämta connection string (stöder både DefaultConnection och MyConnection)
            var rawConnectionString =
                builder.Configuration.GetConnectionString("DefaultConnection")
                ?? builder.Configuration.GetConnectionString("MyConnection")
                ?? throw new InvalidOperationException(
                    "No connection string found. Expected 'DefaultConnection' or 'MyConnection'.");

            // 🔹 Bygg Npgsql-connectionstring med SSL/Trust (stöd för postgres:// och Npgsql-format)
            string BuildNpgsqlCs(string cs)
            {
                if (!string.IsNullOrWhiteSpace(cs) && cs.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase))
                {
                    var uri = new Uri(cs);
                    var userInfo = uri.UserInfo.Split(':');
                    var b = new NpgsqlConnectionStringBuilder
                    {
                        Host = uri.Host,
                        Port = uri.Port,
                        Database = uri.AbsolutePath.Trim('/'),
                        Username = userInfo[0],
                        Password = userInfo.Length > 1 ? userInfo[1] : "",
                        SslMode = SslMode.Require,
                        TrustServerCertificate = true,
                        Pooling = true,
                        MinPoolSize = 0,
                        MaxPoolSize = 20,
                        KeepAlive = 60,
                        Timeout = 15,
                        CommandTimeout = 30
                    };
                    return b.ToString();
                }

                var nb = new NpgsqlConnectionStringBuilder(cs)
                {
                    SslMode = SslMode.Require,
                    TrustServerCertificate = true,
                    Pooling = true,
                    MinPoolSize = 0,
                    MaxPoolSize = 20,
                    KeepAlive = 60,
                    Timeout = 15,
                    CommandTimeout = 30
                };
                return nb.ToString();
            }

            var npgsqlCs = BuildNpgsqlCs(rawConnectionString);

            // Databas & Identitetetskonfiguration (med EF-retry)
            // (din originalrad behålls nedan, utkommenterad)
            // builder.Services.AddDbContext<MyDbContext>(options => options.UseNpgsql(connectionString));
            builder.Services.AddDbContext<MyDbContext>(options =>
                options.UseNpgsql(npgsqlCs, npg =>
                    npg.EnableRetryOnFailure(
                        maxRetryCount: 5,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null)));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<MyDbContext>()
                .AddDefaultTokenProviders();

            // MANAGERS / DAL
            builder.Services.AddScoped<IFileHelper, GitHubFileHelper>();
            builder.Services.AddScoped<BloggManager>();
            builder.Services.AddScoped<BloggImageManager>();
            builder.Services.AddScoped<CommentManager>();
            builder.Services.AddScoped<ForbiddenWordManager>();
            builder.Services.AddScoped<AboutMeManager>();
            builder.Services.AddScoped<ContactMeManager>();
            builder.Services.AddScoped<UserManagerService>();

            // HTTP-KLIENTER
            builder.Services.AddHttpClient<ContentSafetyService>();
            builder.Services.AddSingleton<IConfiguration>(builder.Configuration);

            // API-KOMPONENTER
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // 🔹 Health checks (inkl. Postgres)
            builder.Services.AddHealthChecks().AddNpgSql(npgsqlCs);

            var app = builder.Build();

            // MELLANVAROR & PIPELINE
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();

                // HTTPS-redirect bara i dev (valfritt)
                app.UseHttpsRedirection();
            }
            else
            {
                // Viktigt bakom proxy (Render)
                app.UseForwardedHeaders(new ForwardedHeadersOptions
                {
                    ForwardedHeaders = ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedFor
                });

                // Ingen app.UseHttpsRedirection() i prod på Render
            }

            // TODO: Om vi börjar använda [Authorize] i API:t måste autentisering slås på här,
            // och ligga FÖRE UseAuthorization():
            // app.UseAuthentication();

            app.UseAuthorization();

            // 🔹 Vänta in DB & kör migreringar med exponentiell backoff (kallstart-safe)
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
                var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                var maxAttempts = 8;
                var delay = TimeSpan.FromSeconds(1);

                for (int attempt = 1; attempt <= maxAttempts; attempt++)
                {
                    try
                    {
                        await db.Database.OpenConnectionAsync();
                        await db.Database.ExecuteSqlRawAsync("SELECT 1");
                        await db.Database.CloseConnectionAsync();

                        await db.Database.MigrateAsync();
                        logger.LogInformation("Database connection OK, migrations applied.");
                        break;
                    }
                    catch (Exception ex) when (
                        ex is NpgsqlException ||
                        ex is SocketException ||
                        ex is TimeoutException ||
                        ex.InnerException is NpgsqlException
                    )
                    {
                        logger.LogWarning(ex, "DB not ready (attempt {Attempt}/{Max}). Waiting {Delay}...", attempt, maxAttempts, delay);
                        if (attempt == maxAttempts) throw;
                        await Task.Delay(delay);
                        delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 15));
                    }
                }
            }

            app.MapControllers();

            // 🔹 Health endpoints
            app.MapHealthChecks("/health");
            app.MapGet("/health/db", async (MyDbContext db) =>
            {
                try
                {
                    await db.Database.ExecuteSqlRawAsync("SELECT 1");
                    return Results.Ok("DB OK");
                }
                catch (Exception ex)
                {
                    return Results.Problem(ex.Message);
                }
            });

            // 🔹 Root endpoint
            app.MapGet("/", () => Results.Ok("SarasBloggAPI is running"));

            app.Run();
        }
    }
}
