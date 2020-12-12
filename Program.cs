using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;
using Microsoft.AspNetCore.Components;

using System.Net.Http.Headers;
using System.Threading;
using Microsoft.Authentication.WebAssembly.Msal.Models;

using Microsoft.Graph;

namespace blazorwasm5_two_api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");

            builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

            builder.Services.AddMsalAuthentication(options =>
            {
                builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
                options.ProviderOptions.DefaultAccessTokenScopes.Add("https://identitycrisis.dev/bigbadbiz/stuff.do"); // your api scopes
                //options.ProviderOptions.AdditionalScopesToConsent.Add("User.Read");
            });

            builder.Services.AddHttpClient("MyApi",
                client => client.BaseAddress = new Uri("https://myapiendpoint/")) // your api endpoint
                .AddHttpMessageHandler<MyApiAuthorizationMessageHandler>();

            builder.Services.AddScoped<MyApiAuthorizationMessageHandler>();
            builder.Services.AddScoped(sp => sp.GetRequiredService<IHttpClientFactory>().CreateClient("MyApi"));
            builder.Services.AddGraphClient(new[] { "User.Read" }); // graph helper from sample
            await builder.Build().RunAsync();
        }
    }

    public class MyApiAuthorizationMessageHandler : AuthorizationMessageHandler
    {
        public MyApiAuthorizationMessageHandler(IAccessTokenProvider provider,
            NavigationManager navigationManager)
            : base(provider, navigationManager)
        {
            ConfigureHandler(
                authorizedUrls: new[] { "https://myapiendpoint/api/whatever" },
                scopes: new[] { "https://identitycrisis.dev/bigbadbiz/stuff.do" });
        }
    }
    internal static class GraphClientExtensions
    {
        public static IServiceCollection AddGraphClient(this IServiceCollection services, params string[] scopes)
        {
            services.Configure<RemoteAuthenticationOptions<MsalProviderOptions>>(
                options =>
                {
                    foreach (var scope in scopes)
                    {
                        options.ProviderOptions.AdditionalScopesToConsent.Add(scope);
                    }
                });

            services.AddScoped<IAuthenticationProvider, NoOpGraphAuthenticationProvider>();
            services.AddScoped<IHttpProvider, HttpClientHttpProvider>(sp =>
                new HttpClientHttpProvider(new HttpClient()));
            services.AddScoped(sp =>
            {
                return new GraphServiceClient(
                    sp.GetRequiredService<IAuthenticationProvider>(),
                    sp.GetRequiredService<IHttpProvider>());
            });

            return services;
        }

        private class NoOpGraphAuthenticationProvider : IAuthenticationProvider
        {
            public NoOpGraphAuthenticationProvider(IAccessTokenProvider tokenProvider)
            {
                TokenProvider = tokenProvider;
            }

            public IAccessTokenProvider TokenProvider { get; }

            public async Task AuthenticateRequestAsync(HttpRequestMessage request)
            {
                var result = await TokenProvider.RequestAccessToken(
                    new AccessTokenRequestOptions()
                    {
                        Scopes = new[] { "User.Read" }
                    });

                if (result.TryGetToken(out var token))
                {
                    request.Headers.Authorization ??= new AuthenticationHeaderValue(
                        "Bearer", token.Value);
                }
            }
        }

        private class HttpClientHttpProvider : IHttpProvider
        {
            private readonly HttpClient http;

            public HttpClientHttpProvider(HttpClient http)
            {
                this.http = http;
            }

            public ISerializer Serializer { get; } = new Serializer();

            public TimeSpan OverallTimeout { get; set; } = TimeSpan.FromSeconds(300);

            public void Dispose()
            {
            }

            public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
            {
                return http.SendAsync(request);
            }

            public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
                HttpCompletionOption completionOption,
                CancellationToken cancellationToken)
            {
                return http.SendAsync(request, completionOption, cancellationToken);
            }
        }
    }
}