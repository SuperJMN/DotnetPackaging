using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using DotnetPackaging.Exe;
using FluentAssertions;
using Serilog;
using Xunit;

namespace DotnetPackaging.Exe.Tests;

public class InstallerStubProviderTests
{
    [Fact]
    public async Task Uses_major_minor_patch_version_when_requesting_stub_assets()
    {
        var rid = "win-x64";
        var cacheDir = Path.Combine(Path.GetTempPath(), "dp-stub-provider-" + Guid.NewGuid());
        var baseUrl = "https://example.com/releases/";
        var version = "8.0.75-1+abc";
        var exeBytes = new byte[] { 1, 2, 3, 4 };
        var expectedHash = Convert.ToHexString(SHA256.HashData(exeBytes));

        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler);
        var logger = new LoggerConfiguration().CreateLogger();
        var provider = new InstallerStubProvider(logger, httpClient);

        var shaUrl = baseUrl + $"DotnetPackaging.Exe.Installer-{rid}-v8.0.75.exe.sha256";
        var exeUrl = baseUrl + $"DotnetPackaging.Exe.Installer-{rid}-v8.0.75.exe";

        handler.Register(shaUrl, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(expectedHash) });
        handler.Register(exeUrl, () => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(exeBytes) });

        using (new EnvironmentOverride()
                   .Set("DOTNETPACKAGING_STUB_URL_BASE", baseUrl)
                   .Set("DOTNETPACKAGING_STUB_CACHE", cacheDir))
        {
            var result = await provider.GetStub(rid, version);

            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : string.Empty);
            File.Exists(result.Value).Should().BeTrue();
            File.ReadAllBytes(result.Value).Should().BeEquivalentTo(exeBytes);

            handler.Requests.Should().Contain(shaUrl);
            handler.Requests.Should().Contain(exeUrl);
        }

        try { if (Directory.Exists(cacheDir)) Directory.Delete(cacheDir, true); } catch { }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpResponseMessage>> responses = new(StringComparer.OrdinalIgnoreCase);
        public List<string> Requests { get; } = new();

        public void Register(string url, Func<HttpResponseMessage> factory)
        {
            responses[url] = factory;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri?.AbsoluteUri ?? string.Empty;
            Requests.Add(uri);
            if (responses.TryGetValue(uri, out var responseFactory))
            {
                var response = responseFactory();
                response.RequestMessage = request;
                return Task.FromResult(response);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request });
        }
    }

    private sealed class EnvironmentOverride : IDisposable
    {
        private readonly Dictionary<string, string?> previous = new();

        public EnvironmentOverride Set(string name, string value)
        {
            previous[name] = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
            return this;
        }

        public void Dispose()
        {
            foreach (var pair in previous)
            {
                Environment.SetEnvironmentVariable(pair.Key, pair.Value);
            }
        }
    }
}
