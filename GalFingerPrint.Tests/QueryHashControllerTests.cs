using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using FluentAssertions;
using GalFingerPrint.Server.Models;
using Xunit;

namespace GalFingerPrint.Tests;

public class QueryHashControllerTests(CustomWebApplicationFactory factory) : IClassFixture<CustomWebApplicationFactory>
{
    [Fact]
    public async Task QueryByHash_ShouldPickMostFrequentGame()
    {
        var client = factory.CreateClient();

        await SubmitVote(client, "1.1.1.1", "g1", new[] { "h1", "h2" });
        await SubmitVote(client, "2.2.2.2", "g1", new[] { "h1", "h3" });
        await SubmitVote(client, "3.3.3.3", "g2", new[] { "h2" });
        await SubmitVote(client, "4.4.4.4", "g2", new[] { "h3" });

        var request = new QueryHashDto { Hashes = new List<string> { "h1", "h2", "h3" } };
        var response = await client.PostAsJsonAsync("/game/byhash", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var payload = await response.Content.ReadFromJsonAsync<QueryHashResultDto>();
        payload.Should().NotBeNull();
        payload!.VndbId.Should().Be("g1");
    }

    [Fact]
    public async Task QueryByHash_ShouldReturn404_WhenNoMatch()
    {
        var client = factory.CreateClient();

        var request = new QueryHashDto { Hashes = new List<string> { "unknown" } };
        var response = await client.PostAsJsonAsync("/game/byhash", request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

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
}
