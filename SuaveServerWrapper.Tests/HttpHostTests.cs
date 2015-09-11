using SuaveServerWrapper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SuaveServerWrapper.Tests
{
    public class HttpHostTests
    {
        private const int TestPort = 8083;
        private readonly string LocalHost = "http://localhost:" + TestPort;

        [Fact]
        public async Task TestEmptyGetRequest()
        {
            using (var server = new HttpHost(TestPort))
            {
                const string path = "/test/path?query=value";

                await server.OpenAsync(request =>
                {
                    Assert.Equal(path, request.RequestUri.PathAndQuery);

                    Assert.Equal(HttpMethod.Get, request.Method);

                    return Task.FromResult(new HttpResponseMessage { StatusCode = HttpStatusCode.Found });
                });

                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Get, LocalHost + path);
                    var response = await client.SendAsync(request);
                    Assert.Equal(HttpStatusCode.Found, response.StatusCode);
                }
            }
        }

        [Fact]
        public async Task TestPostRequestWithCustomeHeadersAndBody()
        {
            using (var server = new HttpHost(TestPort))
            {
                const string path = "/test/path?query=value";
                var headers = new Dictionary<string, string>
                    { { "custom-header-1", "custom-value-1" }, { "content-custom", "content-value" } };
                const string contentType = "suave/test";
                const string content = "string content";
                var responseContent = new byte[] { 1, 2, 3 };

                await server.OpenAsync(async request =>
                {
                    Assert.Equal(path, request.RequestUri.PathAndQuery);

                    Assert.Equal(HttpMethod.Post, request.Method);

                    Assert.True(headers.All(pair => request.Headers.First(s => s.Key == pair.Key).Value.First() == pair.Value));

                    Assert.Equal(contentType, request.Content.Headers.ContentType.ToString());

                    Assert.Equal(content, await request.Content.ReadAsStringAsync());

                    var response =  new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.Accepted,
                        Content = new ByteArrayContent(responseContent)
                    };
                    response.Headers.Add("server-custom", "server-value");
                    return response;
                });

                using (var client = new HttpClient())
                {
                    var request = new HttpRequestMessage(HttpMethod.Post, LocalHost + path);
                    headers.Aggregate(request.Headers, (a, b) =>
                    {
                        a.Add(b.Key, b.Value);
                        return a;
                    });
                    request.Content = new StringContent(content);
                    request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

                    var response = await client.SendAsync(request);
                    
                    Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

                    Assert.Equal(responseContent, await response.Content.ReadAsByteArrayAsync());

                    Assert.Equal("server-value", response.Headers.First(s => s.Key == "server-custom").Value.First());
                }
            }
        }
    }
}