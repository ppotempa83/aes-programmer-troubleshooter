using System.Net;
using System.Text;
using SuperiorAes.Core.SiteAnalysis;

namespace SuperiorAes.Core.Tests;

public sealed class GeoapifySiteDataServiceTests
{
    [Fact]
    public async Task AnalyzeUsesGeocodingAndElevationWithoutInventingRoofData()
    {
        var handler = new SequenceHandler(
            """
            {"results":[{"lat":41.881832,"lon":-87.623177,"formatted":"Chicago, IL, United States"}]}
            """,
            """
            {"results":[{"location":{"lat":41.881832,"lon":-87.623177},"elevation":181,"units":"m"}]}
            """);
        var service = new GeoapifySiteDataService(new HttpClient(handler));

        var result = await service.AnalyzeAsync("Chicago, IL", "runtime-only-key");

        Assert.Equal("Chicago, IL, United States", result.FormattedAddress);
        Assert.Equal(181, result.GroundElevationMeters);
        Assert.Null(result.EstimatedBuildingHeightMeters);
        Assert.Null(result.RoofAreaSquareMeters);
        Assert.Contains("must be verified on site", result.Notes);
        Assert.Equal(2, handler.RequestCount);
    }

    private sealed class SequenceHandler(params string[] responses) : HttpMessageHandler
    {
        private readonly Queue<string> _responses = new(responses);
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responses.Dequeue(), Encoding.UTF8, "application/json")
            });
        }
    }
}
