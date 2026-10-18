using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TocBuilder.Web.Client;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.Services.AddScoped<TreeLibrary>();

await builder.Build().RunAsync();
