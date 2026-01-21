using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Polly;
using Polly.Extensions.Http;
using System.Text;
using Tracking.Infrastructure.Data;
using Tracking.Infrastructure.Repositories;
using Tracking.Application.Services;
using Tracking.Application;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IRastreioRepository, RastreioRepository>();
builder.Services.AddScoped<IClienteService, ClienteService>();

// DbContext (SQL Server)
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("SqlServer")));

// DI
builder.Services.AddScoped<IRastreioRepository, RastreioRepository>();
builder.Services.AddScoped<IRastreioStatusRepository, RastreioStatusRepository>(); // ✅ NOVO
builder.Services.AddScoped<IClienteService, ClienteService>();

// HttpClient TPL (typed) + Polly
builder.Services.AddHttpClient<ITplService, TplService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Tpl:BaseUrl"]!);
    client.Timeout = TimeSpan.FromSeconds(15);
})
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .OrResult(r => (int)r.StatusCode == 429)
    .WaitAndRetryAsync(new[]
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(4)
    }))
.AddPolicyHandler(HttpPolicyExtensions
    .HandleTransientHttpError()
    .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ===== Swagger com suporte a JWT =====
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Tracking API",
        Version = "v1",
        Description = "API de rastreamento de pedidos com integração TPL",
        Contact = new OpenApiContact
        {
            Name = "Suporte Tracking",
            Email = "suporte@tracking.com"
        }
    });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT no formato: Bearer {seu_token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// ===== JWT =====
var issuer = builder.Configuration["Jwt:Issuer"];
var audience = builder.Configuration["Jwt:Audience"];
var key = builder.Configuration["Jwt:Key"];
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key ?? throw new InvalidOperationException("JWT Key not configured")));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.RequireHttpsMetadata = false;
        opt.SaveToken = true;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ===== Swagger UI =====
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "Tracking API v1");
    options.RoutePrefix = "swagger";
    options.DocumentTitle = "Tracking API - Documentação";
    options.DefaultModelsExpandDepth(2);
    options.DefaultModelExpandDepth(2);
    options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.List);
    options.DisplayRequestDuration();
});

// app.UseHttpsRedirection(); // Comentado para HTTP
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.Run();

public partial class Program { }
