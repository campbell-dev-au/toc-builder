using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TocBuilder.Web.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped<TreeLibrary>();
builder.Services.AddScoped<ISuggestionProvider, StubSuggestionProvider>();

await builder.Build().RunAsync();
