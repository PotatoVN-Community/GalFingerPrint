namespace GalFingerPrint.Server
{
    using GalFingerPrint.Server.Data;
    using GalFingerPrint.Server.Repositories;
    using GalFingerPrint.Server.Services;
    using Microsoft.EntityFrameworkCore;

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            // DbContext (PostgreSQL)
            var connStr = builder.Configuration.GetConnectionString("Default");
            builder.Services.AddDbContext<GalDbContext>(options =>
                options.UseNpgsql(connStr));

            // Repositories
            builder.Services.AddScoped<IGalgameRepository, GalgameRepository>();
            builder.Services.AddScoped<IHashRepository, HashRepository>();
            builder.Services.AddScoped<IGalgameHashRepository, GalgameHashRepository>();
            builder.Services.AddScoped<IVoteRepository, VoteRepository>();

            // Services
            builder.Services.AddScoped<IVoteService, VoteService>();

            var app = builder.Build();

            // Configure the HTTP request pipeline.
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
