using AquaSafe.Api.Services;
using AquaSafe.Api.Endpoints;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ── Serviços ──────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient<ImaService>();
builder.Services.AddScoped<ImaService>();

builder.Services.AddCors(opts =>
    opts.AddDefaultPolicy(p =>
        p.AllowAnyOrigin()
         .AllowAnyMethod()
         .AllowAnyHeader()));

builder.Services.AddOpenApi();

var app = builder.Build();

// ── Middlewares ───────────────────────────────────────────────────────────────
app.UseCors();
app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

// ── Endpoints ─────────────────────────────────────────────────────────────────
app.MapBeachEndpoints();
app.MapHealthEndpoints();

app.Run();