using AquaSafe.Api.Services;
using AquaSafe.Api.Endpoints;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader()));

builder.Services.AddOpenApi();

builder.Services.AddHttpClient<ImaService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add(
        "User-Agent",
        "AquaSafe/2.0 (github.com/GUISLOTTI/AquaSafe; monitoramento balneabilidade SC)");
});

builder.Services.AddHttpClient<WeatherService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(15);
    client.DefaultRequestHeaders.Add("User-Agent", "AquaSafe/2.0");
});

var app = builder.Build();

app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapBeachEndpoints();
app.MapHealthEndpoints();
app.MapWeatherEndpoints();

app.Run();
