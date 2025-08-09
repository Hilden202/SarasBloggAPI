using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;
using SarasBloggAPI.Services;
using SarasBloggAPI.DAL;
using Microsoft.AspNetCore.Identity;
using Npgsql.EntityFrameworkCore.PostgreSQL;
using Microsoft.AspNetCore.HttpOverrides;

namespace SarasBloggAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Render/containers: bind PORT från env
            var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
            builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

            // Hämta och logga connection string (stöder både DefaultConnection och MyConnection)
            var connectionString =
                builder.Configuration.GetConnectionString("DefaultConnection")
                ?? builder.Configuration.GetConnectionString("MyConnection")
                ?? throw new InvalidOperationException(
                    "No connection string found. Expected 'DefaultConnection' or 'MyConnection'.");

            // Databas & Identitetetskonfiguration
            builder.Services.AddDbContext<MyDbContext>(options =>
                options.UseNpgsql(connectionString));

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
            app.MapControllers();

            app.Run();
        }
    }
}