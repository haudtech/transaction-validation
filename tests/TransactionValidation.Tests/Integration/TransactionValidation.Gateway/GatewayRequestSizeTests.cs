using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

using Xunit;

namespace TransactionValidation.Tests.Integration.TransactionValidation.Gateway;

public sealed class GatewayRequestSizeTests
{
    private const int BodyLimit = 1024 * 1024;
    private const string TransactionPath = "/transactions/v1/partner/transactions";

    [Trait("Category", "Integration")]
    [Theory]
    [InlineData(BodyLimit, false, HttpStatusCode.Accepted)]
    [InlineData(BodyLimit + 1, false, HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(BodyLimit - 1024, true, HttpStatusCode.Accepted)]
    [InlineData(BodyLimit, true, HttpStatusCode.RequestEntityTooLarge)]
    [InlineData(BodyLimit + 1, true, HttpStatusCode.RequestEntityTooLarge)]
    public async Task PostTransaction_WhenBodyReachesSizeLimit_ReturnsExpectedStatus(
        int bodySize, bool chunked, HttpStatusCode expectedStatus)
    {
        await using var backend = new GatewayProxyTests.TestBackend();
        await backend.StartAsync();
        using var factory = new GatewayHostFactory(backend.Address);
        factory.UseKestrel(0);
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, TransactionPath)
        {
            Content = new ByteArrayContent(new byte[bodySize])
        };
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(GatewayTestCredentials.Authorization);
        request.Headers.TransferEncodingChunked = chunked;
        request.Headers.Add("Correlation-Id", "size-test");

        using var response = await client.SendAsync(request);

        Assert.Equal(expectedStatus, response.StatusCode);
        if (expectedStatus == HttpStatusCode.RequestEntityTooLarge)
        {
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("payload_too_large", problem.RootElement.GetProperty("code").GetString());
            Assert.Equal("size-test", problem.RootElement.GetProperty("correlationId").GetString());
            if (!chunked)
            {
                Assert.Equal(0, backend.RequestCount);
            }
        }
        else
        {
            Assert.Equal(bodySize, backend.RequestBodies.Single().Length);
        }
    }

    [Trait("Category", "Integration")]
    [Fact]
    public async Task PostTransaction_WhenBackendRespondsBeforeOversizedChunkedBodyEnds_ReturnsPayloadTooLarge()
    {
        await using var backend = new GatewayProxyTests.TestBackend(readBody: false);
        await backend.StartAsync();
        using var factory = new GatewayHostFactory(backend.Address);
        factory.UseKestrel(0);
        using var client = factory.CreateClient();
        using var socket = new TcpClient();
        await socket.ConnectAsync(client.BaseAddress!.Host, client.BaseAddress.Port);
        await using var stream = socket.GetStream();
        var headers = $"POST {TransactionPath} HTTP/1.1\r\n"
            + $"Host: {client.BaseAddress.Authority}\r\n"
            + $"Authorization: {GatewayTestCredentials.Authorization}\r\n"
            + "Transfer-Encoding: chunked\r\n\r\n1\r\na\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(headers));
        await backend.ResponseSent.WaitAsync(TimeSpan.FromSeconds(5));
        await stream.WriteAsync(Encoding.ASCII.GetBytes("100001\r\n"));
        await stream.WriteAsync(new byte[BodyLimit + 1]);
        await stream.WriteAsync(Encoding.ASCII.GetBytes("\r\n0\r\n\r\n"));
        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);

        var statusLine = await reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.StartsWith("HTTP/1.1 413 ", statusLine, StringComparison.Ordinal);
    }
}
