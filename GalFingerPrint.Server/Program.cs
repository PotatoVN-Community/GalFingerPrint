namespace GalFingerPrint.Server
{
    using Data;
    using Repositories;
    using Services;
    using Microsoft.EntityFrameworkCore;

    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "GalFingerPrint.Server.xml"));
            });

            // DbContext (PostgreSQL)
            var connStr = builder.Configuration.GetConnectionString("Default");
            builder.Services.AddDbContext<GalDbContext>(options => options.UseNpgsql(connStr));

            // Repositories
            builder.Services.AddScoped<IGalgameRepository, GalgameRepository>();
            builder.Services.AddScoped<IHashRepository, HashRepository>();
            builder.Services.AddScoped<IGalgameHashRepository, GalgameHashRepository>();
            builder.Services.AddScoped<IVoteRepository, VoteRepository>();
            builder.Services.AddScoped<IGameQueryRepository, GameQueryRepository>();

            // Services
            builder.Services.AddScoped<IVoteService, VoteService>();
            builder.Services.AddScoped<IQueryHashService, QueryHashService>();

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
