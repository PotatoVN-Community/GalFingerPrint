using System.Text.Json;
using GalFingerPrint.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GalFingerPrint.Server.Data;

public class GalDbContext(DbContextOptions<GalDbContext> options) : DbContext(options)
{
    public DbSet<Galgame> Galgames => Set<Galgame>();
    public DbSet<Hash> Hashes => Set<Hash>();
    public DbSet<GalgameHash> GalgameHashes => Set<GalgameHash>();
    public DbSet<Vote> Votes => Set<Vote>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Galgame
        modelBuilder.Entity<Galgame>(b =>
        {
            b.HasKey(g => g.Id);
            b.Property(g => g.VndbId).IsRequired().HasMaxLength(20);
            b.HasIndex(g => g.VndbId).IsUnique();

            // 链接到中间表
            b.HasMany(g => g.GalgameHashes)
             .WithOne()
             .HasForeignKey(gh => gh.GalgameId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // Hash
        modelBuilder.Entity<Hash>(b =>
        {
            b.HasKey(h => h.Id);
            b.Property(h => h.HashValue).IsRequired();
            b.HasIndex(h => h.HashValue).IsUnique();

            // 链接到中间表
            b.HasMany(h => h.GalgameHashes)
             .WithOne()
             .HasForeignKey(gh => gh.HashId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // GalgameHash (join with count)
        modelBuilder.Entity<GalgameHash>(b =>
        {
            b.HasKey(x => new { x.HashId, x.GalgameId });
            b.Property(x => x.Cnt).HasDefaultValue(0);
        });

        // Vote with unique by (Address, VndbId) and JSONB hashes
        modelBuilder.Entity<Vote>(b =>
        {
            b.HasKey(v => v.Id);
            b.HasIndex(v => new { v.Address, v.VndbId }).IsUnique();

            // uint -> bigint in PostgreSQL
            var addrConverter = new ValueConverter<uint, long>(
                v => (long)v,
                v => (uint)v);
            b.Property(v => v.Address)
                .HasConversion(addrConverter)
                .HasColumnType("bigint");

            var jsonConverter = new ValueConverter<List<string>, string>(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => JsonSerializer.Deserialize<List<string>>(v, (JsonSerializerOptions?)null) ?? new List<string>());
            var valueComparer = new ValueComparer<List<string>>(
                (l1, l2) => l1 != null && l2 != null && l1.SequenceEqual(l2),
                l => l == null ? 0 : l.Aggregate(0, (a, v) => HashCode.Combine(a, v.GetHashCode())),
                l => l == null ? new List<string>() : l.ToList());

            b.Property(v => v.Hashes)
                .HasConversion(jsonConverter)
                .Metadata.SetValueComparer(valueComparer);
            b.Property(v => v.Hashes)
                .HasColumnType("jsonb");
        });
    }
}
