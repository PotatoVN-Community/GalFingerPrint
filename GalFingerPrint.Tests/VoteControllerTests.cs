using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using GalFingerPrint.Server.Data;
using GalFingerPrint.Server.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace GalFingerPrint.Tests;

public class VoteControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task CreateAndModifyVote_ShouldAffectCountsAndQuery()
    {
        var client = factory.CreateClient();

        var vndb = "v1";
        var body1 = new { hashes = new[] { "h1", "h2", "h1" } };
        var req1 = new HttpRequestMessage(HttpMethod.Patch, $"/vote/{vndb}")
        {
            Content = JsonContent.Create(body1)
        };
        var resp1 = await client.SendAsync(req1);
        resp1.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var page1 = await client.GetFromJsonAsync<VoteQueryResponse>("/vote?page=1&pageSize=10");
        page1.Should().NotBeNull();
        page1!.total.Should().Be(1);
        page1.items.Should().HaveCount(1);
        page1.items[0].VndbId.Should().Be(vndb);
        page1.items[0].Hashes.Should().BeEquivalentTo(new[] { "h1", "h2" });

        // DB asserts: counts
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GalDbContext>();
            var gal = await db.Galgames.SingleAsync(g => g.VndbId == vndb);
            var h1 = await db.Hashes.SingleAsync(h => h.HashValue == "h1");
            var h2 = await db.Hashes.SingleAsync(h => h.HashValue == "h2");
            var gh1 = await db.GalgameHashes.SingleAsync(x => x.GalgameId == gal.Id && x.HashId == h1.Id);
            var gh2 = await db.GalgameHashes.SingleAsync(x => x.GalgameId == gal.Id && x.HashId == h2.Id);
            gh1.Cnt.Should().Be(1);
            gh2.Cnt.Should().Be(1);
        }

        // Modify vote: remove h1, add h3
        var body2 = new { hashes = new[] { "h2", "h3" } };
        var req2 = new HttpRequestMessage(HttpMethod.Patch, $"/vote/{vndb}")
        {
            Content = JsonContent.Create(body2)
        };
        var resp2 = await client.SendAsync(req2);
        resp2.StatusCode.Should().Be(HttpStatusCode.NoContent);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GalDbContext>();
            var hashVals = new[] { "h1", "h2", "h3" };
            var hashes = await db.Hashes.Where(h => hashVals.Contains(h.HashValue)).ToListAsync();
            var map = hashes.ToDictionary(h => h.HashValue, h => h.Id);

            var gal = await db.Galgames.SingleAsync(g => g.VndbId == vndb);
            var gh2 = await db.GalgameHashes.SingleAsync(x => x.GalgameId == gal.Id && x.HashId == map["h2"]);
            gh2.Cnt.Should().Be(1);
            var gh3 = await db.GalgameHashes.SingleAsync(x => x.GalgameId == gal.Id && x.HashId == map["h3"]);
            gh3.Cnt.Should().Be(1);
            var gh1 = await db.GalgameHashes.SingleOrDefaultAsync(x => x.GalgameId == gal.Id && x.HashId == map["h1"]);
            gh1.Should().BeNull(); // 已删
        }

        // Idempotent: same patch again doesn't change counts
        var req3 = new HttpRequestMessage(HttpMethod.Patch, $"/vote/{vndb}")
        {
            Content = JsonContent.Create(body2)
        };
        var resp3 = await client.SendAsync(req3);
        resp3.StatusCode.Should().Be(HttpStatusCode.NoContent);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GalDbContext>();
            var gal = await db.Galgames.SingleAsync(g => g.VndbId == vndb);
            var h2 = await db.Hashes.SingleAsync(h => h.HashValue == "h2");
            var h3 = await db.Hashes.SingleAsync(h => h.HashValue == "h3");
            var gh2 = await db.GalgameHashes.SingleAsync(x => x.GalgameId == gal.Id && x.HashId == h2.Id);
            var gh3 = await db.GalgameHashes.SingleAsync(x => x.GalgameId == gal.Id && x.HashId == h3.Id);
            gh2.Cnt.Should().Be(1);
            gh3.Cnt.Should().Be(1);
        }

        // Add another vote for another game to test paging
        var vndb2 = "v2";
        var req4 = new HttpRequestMessage(HttpMethod.Patch, $"/vote/{vndb2}")
        {
            Content = JsonContent.Create(new { hashes = new[] { "x1" } })
        };
        var resp4 = await client.SendAsync(req4);
        resp4.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var pageSize1 = await client.GetFromJsonAsync<VoteQueryResponse>("/vote?page=1&pageSize=1");
        pageSize1!.total.Should().Be(2);
        pageSize1.items.Should().HaveCount(1);
        var pageSize2 = await client.GetFromJsonAsync<VoteQueryResponse>("/vote?page=2&pageSize=1");
        pageSize2!.items.Should().HaveCount(1);
    }

    private class VoteQueryResponse
    {
        public List<VoteDto> items { get; set; } = new();
        public int total { get; set; }
        public int page { get; set; }
        public int pageSize { get; set; }
    }
}
