using System;
using System.Data.Common;
using GalFingerPrint.Server.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GalFingerPrint.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Server.Program>
{
    private readonly DbConnection _connection;

    public CustomWebApplicationFactory()
    {
        // Ensure DB_CONNECTION_STRING exists early for Program.EnsureRequiredEnvironment
        Environment.SetEnvironmentVariable("DB_CONNECTION_STRING", "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=tests");
        // Tell the app to skip EF migrations in tests to avoid conflicts between EnsureCreated() and Migrate()
        Environment.SetEnvironmentVariable("SKIP_MIGRATIONS", "1");
        var sqlite = new SqliteConnection("DataSource=:memory:;Cache=Shared");
        sqlite.Open();
        _connection = sqlite;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Configure test host to use sqlite in-memory and propagate a fake DB connection string via UseSetting.
        builder.UseSetting("DB_CONNECTION_STRING", "Host=localhost;Port=5432;Username=postgres;Password=postgres;Database=tests");
        builder.ConfigureServices(services =>
        {
            // 移除已注册的 Npgsql 上下文
            ServiceDescriptor? dbCtx = null;
            foreach (var d in services)
            {
                if (d.ServiceType == typeof(DbContextOptions<GalDbContext>)) { dbCtx = d; break; }
            }
            if (dbCtx != null) services.Remove(dbCtx);

            // 重新注册为 Sqlite 内存库
            services.AddDbContext<GalDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // 构建服务以创建数据库
            using var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<GalDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
