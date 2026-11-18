using System.Net;
using System.Text;
using ThunderClient.Core;
using Xunit;

namespace ThunderClient.Tests;

public class ThunderPublicPricingTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        public bool Protected;
        public bool MissingA100;
        public bool A100PriceSurge;
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (Protected) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            var content = path switch
            {
                "/v2/pricing" => A100PriceSurge
                    ? """{"pricing":{"l40_x1":0.79,"a100xl_x1":1.95,"additional_vcpus":0.04,"disk_gb":0.0003}}"""
                    : """{"pricing":{"l40_x1":0.79,"a100xl_x1":1.09,"additional_vcpus":0.04,"disk_gb":0.0003}}""",
                "/v2/specs" => MissingA100
                    ? """{"specs":{"l40_x1":{"vramGB":48,"vcpuOptions":[6,8,10,12],"storageGB":{"min":100,"max":500}}}}"""
                    : """{"specs":{"l40_x1":{"vramGB":48,"vcpuOptions":[6,8,10,12],"storageGB":{"min":100,"max":500}},"a100xl_x1":{"vramGB":80,"vcpuOptions":[4,8,12],"storageGB":{"min":100,"max":500}}}}""",
                _ => throw new InvalidOperationException("Undokumentierter Endpunkt")
            };
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent(content, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task ProviderCpuAndDiskRatesCannotPassFalseBudget()
    {
        using var http = new HttpClient(new FakeHandler());
        var quotes = await new ThunderPublicPricing(http).QuoteAsync(
            [new GpuChoice("l40", "L40", 48, 12, .79m)], 1, 149);
        Assert.True(quotes["l40"].HourlyUsd > 1.04m);
        Assert.True(quotes["l40"].HourlyUsd > .85m);
    }

    [Fact]
    public async Task InvalidVcpuOrDiskConfigurationsAreRejectedBeforeCreate()
    {
        using var http = new HttpClient(new FakeHandler());
        var service = new ThunderPublicPricing(http);
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.QuoteAsync([new GpuChoice("l40","L40",48,16,.79m)], 1, 149));
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.QuoteAsync([new GpuChoice("l40","L40",48,12,.79m)], 1, 550));
    }

    [Fact]
    public async Task A100UsesThunderCanonicalA100xlSpecAndLivePrice()
    {
        using var http = new HttpClient(new FakeHandler());
        var gpu = new GpuChoice("a100", "A100 80 GB", 80, 8, 1.09m);
        Assert.Equal("a100xl", ThunderGpuNames.ToProviderKey(gpu.Key));
        var quotes = await new ThunderPublicPricing(http).QuoteAsync([gpu], 1, 211);
        Assert.Single(quotes);
        Assert.True(quotes.ContainsKey("a100")); // local UI identity remains stable
        Assert.InRange(quotes["a100"].HourlyUsd, 1.28m, 1.29m);
    }

    [Fact]
    public async Task PriceSurgeFromThunderCannotBeUnderquotedByLocalA100Estimate()
    {
        using var http = new HttpClient(new FakeHandler { A100PriceSurge = true });
        var quotes = await new ThunderPublicPricing(http).QuoteAsync(
            [new GpuChoice("a100", "A100 80 GB", 80, 8, 1.09m)], 1, 211);
        Assert.True(quotes["a100"].HourlyUsd > 2.14m);
        Assert.True(quotes["a100"].HourlyUsd > 1.35m);
    }

    [Fact]
    public async Task MissingCanonicalA100SpecFailsClosedRatherThanUsingAliasOrGuess()
    {
        using var http = new HttpClient(new FakeHandler { MissingA100 = true });
        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new ThunderPublicPricing(http).QuoteAsync(
                [new GpuChoice("a100", "A100 80 GB", 80, 8, 1.09m)], 1, 211));
        Assert.Contains("a100xl_x1", error.Message);
        Assert.Contains("Keine kostenpflichtige Instanz", error.Message);
    }

    [Fact]
    public async Task LivePricingAuthFailureBlocksPaidCreation()
    {
        using var http = new HttpClient(new FakeHandler { Protected = true });
        var service = new ThunderPublicPricing(http);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.QuoteAsync([new GpuChoice("l40","L40",48,12,.79m)], 1, 149));
        Assert.Contains("gesperrt", error.Message);
    }
}
