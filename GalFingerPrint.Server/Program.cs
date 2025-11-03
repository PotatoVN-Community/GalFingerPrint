using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using IPNetwork = Microsoft.AspNetCore.HttpOverrides.IPNetwork;
using GalFingerPrint.Server.Data;
using GalFingerPrint.Server.Repositories;
using GalFingerPrint.Server.Services;
using Microsoft.EntityFrameworkCore;

namespace GalFingerPrint.Server
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            EnsureRequiredEnvironment(builder.Configuration);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "GalFingerPrint.Server.xml"));
            });

            // DbContext (PostgreSQL)
            builder.Services.AddDbContext<GalDbContext>((serviceProvider, options) =>
            {
                var configuration = serviceProvider.GetRequiredService<IConfiguration>();
                var connStr = configuration["DB_CONNECTION_STRING"] ?? Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
                if (string.IsNullOrWhiteSpace(connStr))
                    throw new InvalidOperationException("DB_CONNECTION_STRING environment variable is not set.");
                options.UseNpgsql(connStr);
            });
            
            // Reverse Proxy Support
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                options.KnownNetworks.Clear();
                options.KnownProxies.Clear();
                string ip = GetEnv(builder.Configuration, "REVERSE_PROXY_IP");
                string range = GetEnv(builder.Configuration, "REVERSE_PROXY_IP_RANGE");
                if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(range))
                    return;
                if (!int.TryParse(range, out int prefixLength) || prefixLength < 0 || prefixLength > 32)
                    throw new InvalidOperationException("REVERSE_PROXY_IP_RANGE must be a valid number.");
                options.KnownNetworks.Add(new IPNetwork(IPAddress.Parse(ip), prefixLength));
            });
            
            // CORS
            builder.Services.AddCors(x =>
            {
                x.AddPolicy("AllowAll", corsPolicyBuilder =>
                {
                    corsPolicyBuilder.AllowAnyOrigin()
                        .AllowAnyMethod()
                        .AllowAnyHeader();
                });
            });


            // Repositories
            builder.Services.AddScoped<IGalgameRepository, GalgameRepository>();
            builder.Services.AddScoped<IHashRepository, HashRepository>();
            builder.Services.AddScoped<IGalgameHashRepository, GalgameHashRepository>();
            builder.Services.AddScoped<IVoteRepository, VoteRepository>();
            builder.Services.AddScoped<IGameQueryRepository, GameQueryRepository>();
            // Game service
            builder.Services.AddScoped<IGameService, GameService>();

            // Services
            builder.Services.AddScoped<IVoteService, VoteService>();
            builder.Services.AddScoped<IQueryHashService, QueryHashService>();

            var app = builder.Build();
            
            // DataBase Migration
            using (IServiceScope scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GalDbContext>();
                try
                {
                    var provider = db.Database.ProviderName ?? string.Empty;
                    if (provider.IndexOf("Sqlite", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        var conn = db.Database.GetDbConnection();
                        var connStr = conn?.ConnectionString ?? string.Empty;
                        if (connStr.Contains(":memory:", StringComparison.OrdinalIgnoreCase))
                        {
                            // In-memory Sqlite used by tests; schema is created by tests. Skip migrations.
                            Console.WriteLine("Skipping EF migrations for in-memory Sqlite (test environment).");
                        }
                        else
                        {
                            db.Database.Migrate();
                        }
                    }
                    else
                    {
                        db.Database.Migrate();
                    }
                }
                catch (Exception e)
                {
                    var msg = e.Message ?? string.Empty;
                    if (msg.IndexOf("already exists", StringComparison.OrdinalIgnoreCase) >= 0 &&
                        msg.IndexOf("table", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Console.WriteLine($"Ignored migration error (existing tables): {e.Message}");
                    }
                    else
                    {
                        Console.WriteLine($"Failed to migrate database:\n{e.Message}{e.StackTrace}");
                        return;
                    }
                }
            
            }

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            
            app.UseForwardedHeaders();
            
            app.UseCors("AllowAll");

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }

        private static void EnsureRequiredEnvironment(IConfiguration config)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(GetEnv(config, "DB_CONNECTION_STRING"))) missing.Add("DB_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(GetEnv(config, "REVERSE_PROXY_IP")) !=  
                string.IsNullOrWhiteSpace(GetEnv(config,"REVERSE_PROXY_IP_RANGE")))
                missing.Add("REVERSE_PROXY_IP and REVERSE_PROXY_IP_RANGE both required together");
            if (!string.IsNullOrEmpty(GetEnv(config, "REVERSE_PROXY_IP_RANGE")) && !IsNum("REVERSE_PROXY_IP_RANGE"))
                missing.Add("REVERSE_PROXY_IP_RANGE must be a valid number");

            if (missing.Count == 0)
                return;

            Console.Error.WriteLine($"Missing required environment variables: {string.Join(", ", missing)}");
            Environment.Exit(1);
            bool IsNum(string key) => int.TryParse(config[key] ?? Environment.GetEnvironmentVariable(key), out _);
        }
        
        private static string GetEnv(IConfiguration config, string key) =>
            config[key] ?? Environment.GetEnvironmentVariable(key) ?? string.Empty;
    }
}
