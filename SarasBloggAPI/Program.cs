using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;
using SarasBloggAPI.Services;
using SarasBloggAPI.DAL;

namespace SarasBloggAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // DATABAS
            var connectionString = builder.Configuration.GetConnectionString("MyConnection");
            builder.Services.AddDbContext<MyDbContext>(options =>
                options.UseSqlServer(connectionString));

            // MANAGERS / DAL
            builder.Services.AddScoped<BloggManager>();
            builder.Services.AddScoped<CommentManager>();
            builder.Services.AddScoped<ForbiddenWordManager>();
            builder.Services.AddScoped<AboutMeManager>();
            builder.Services.AddScoped<ContactMeManager>();


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
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
