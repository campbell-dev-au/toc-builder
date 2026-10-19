using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TocBuilder.Web.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped<TreeLibrary>();
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<ISuggestionProvider, HttpSuggestionProvider>();

await builder.Build().RunAsync();
