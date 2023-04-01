using System.IO.Compression;
using System.Text.Json.Serialization;
using FastEndpoints;
using FastEndpoints.Swagger;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;

var builder = WebApplication.CreateBuilder(args);
var configuration = builder.Configuration;

#if DEBUG
configuration.AddUserSecrets<Program>();
#endif

var services = builder.Services;
services.AddFastEndpoints();
services.AddSwaggerDoc(serializerSettings: x => x.Converters.Add(new JsonStringEnumConverter()));
services.AddHttpContextAccessor();
services.AddSingleton<IClock>(SystemClock.Instance);
services.Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest);
services.AddResponseCompression(options =>
{
    options.Providers.Add<GzipCompressionProvider>();
    options.EnableForHttps = true;
});

var app = builder.Build();


app.Run();

namespace Mass.Transit.Outbox.Repo.Replicate
{
    public partial class Program
    {
    }
}

