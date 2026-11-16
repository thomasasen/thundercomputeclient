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
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (Protected) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
            var content = path switch
            {
                "/v2/pricing" => """{"pricing":{"l40_x1":0.79,"additional_vcpus":0.04,"disk_gb":0.0003}}""",
                "/v2/specs" => """{"specs":{"l40_x1":{"vramGB":48,"vcpuOptions":[6,8,10,12],"storageGB":{"min":100,"max":500}}}}""",
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
    public async Task LivePricingAuthFailureBlocksPaidCreation()
    {
        using var http = new HttpClient(new FakeHandler { Protected = true });
        var service = new ThunderPublicPricing(http);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.QuoteAsync([new GpuChoice("l40","L40",48,12,.79m)], 1, 149));
        Assert.Contains("gesperrt", error.Message);
    }
}
