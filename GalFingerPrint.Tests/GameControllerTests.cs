using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace GalFingerPrint.Tests;

public class GameControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    private static async Task SubmitVote(HttpClient client, string ip, string vndbId, IEnumerable<string> hashes)
    {
        var req = new HttpRequestMessage(HttpMethod.Patch, $"/vote/{vndbId}")
        {
            Content = JsonContent.Create(new { hashes })
        };
        req.Headers.Add("X-Forwarded-For", ip);
        var resp = await client.SendAsync(req);
        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    public class TestGameHashVoteDto
    {
        public string Hash { get; set; } = string.Empty;
        public int Count { get; set; }
        public bool IsDominant { get; set; }
    }

    public class TestGameVotesDto
    {
        public string VndbId { get; set; } = string.Empty;
        public List<TestGameHashVoteDto> Items { get; set; } = new();
    }

    public class TestGalgameDto
    {
        public string VndbId { get; set; } = string.Empty;
    }

    public class TestGameListDto
    {
        public List<TestGalgameDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    [Fact]
    public async Task GetById_ReturnsHashCountsAndDominance()
    {
        var client = factory.CreateClient();

        await SubmitVote(client, "1.1.1.1", "g1", new[] { "h1", "h2" });
        await SubmitVote(client, "2.2.2.2", "g1", new[] { "h1", "h3" });
        await SubmitVote(client, "3.3.3.3", "g2", new[] { "h2" });
        await SubmitVote(client, "4.4.4.4", "g2", new[] { "h3" });

        var resp = await client.GetAsync("/game/byid/g1");
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await resp.Content.ReadFromJsonAsync<TestGameVotesDto>();
        payload.Should().NotBeNull();
        payload!.VndbId.Should().Be("g1");
        payload.Items.Should().HaveCount(3);

        var map = payload.Items.ToDictionary(i => i.Hash, StringComparer.Ordinal);
        map["h1"].Count.Should().Be(2);
        map["h1"].IsDominant.Should().BeTrue();

        map["h2"].Count.Should().Be(1);
        map["h2"].IsDominant.Should().BeTrue();

        map["h3"].Count.Should().Be(1);
        map["h3"].IsDominant.Should().BeTrue();
    }

    [Fact]
    public async Task List_ReturnsPagedGameIds()
    {
        var client = factory.CreateClient();

        await SubmitVote(client, "1.1.1.1", "gA", new[] { "a1" });
        await SubmitVote(client, "2.2.2.2", "gB", new[] { "b1" });

        var p1 = await client.GetFromJsonAsync<TestGameListDto>("/game?page=1&pageSize=1");
        p1.Should().NotBeNull();
        p1!.Total.Should().Be(2);
        p1.Items.Should().HaveCount(1);

        var p2 = await client.GetFromJsonAsync<TestGameListDto>("/game?page=2&pageSize=1");
        p2.Should().NotBeNull();
        p2!.Items.Should().HaveCount(1);
        p1.Items[0].VndbId.Should().NotBe(p2.Items[0].VndbId);
    }
}