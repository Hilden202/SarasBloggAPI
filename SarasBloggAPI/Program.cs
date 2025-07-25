using Microsoft.EntityFrameworkCore;
using SarasBloggAPI.Data;
using SarasBloggAPI.Services;
using SarasBloggAPI.DAL;
using Microsoft.AspNetCore.Identity;
using Npgsql.EntityFrameworkCore.PostgreSQL;


namespace SarasBloggAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Databas & Identitetetskonfiguration
            var connectionString = builder.Configuration.GetConnectionString("MyConnection");
            //builder.Services.AddDbContext<MyDbContext>(options =>
            //    options.UseSqlServer(connectionString));

            builder.Services.AddDbContext<MyDbContext>(options =>
                options.UseNpgsql(connectionString));


            builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
                .AddEntityFrameworkStores<MyDbContext>()
                .AddDefaultTokenProviders();

            // MANAGERS / DAL
            builder.Services.AddScoped<BloggManager>();
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
            }

            app.UseHttpsRedirection();
            app.UseAuthorization();
            app.MapControllers();

            app.Run();
        }
    }
}
