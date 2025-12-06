using DevNexus.Core.Abstractions;
using DevNexus.Core.Services;
using DevNexus.Core.Extensions;
using DevNexus.Infrastructure.Extensions;
using DevNexus.ApiService.Hubs;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();

// Add infrastructure services (database, Redis, Seq, Identity)
builder.Services.AddInfrastructureServices(builder.Configuration);

// Add core services (LLM, Kernel)
builder.Services.AddCoreServices(builder.Configuration);

// Add core services
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddScoped<IAuthService, AuthService>();

// Add SignalR support with Redis backplane
var signalRBuilder = builder.Services.AddSignalR();

// 配置 Redis 背板支持多实例部署
var redisConnectionString = builder.Configuration.GetRedisConnectionString();
if (!string.IsNullOrEmpty(redisConnectionString))
{
    signalRBuilder.AddStackExchangeRedis(redisConnectionString, options =>
    {
        options.Configuration.ChannelPrefix = StackExchange.Redis.RedisChannel.Literal("DevNexus");
    });
}

// Add authentication and authorization services
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-secret-key-here-1234567890123456";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "DevNexus";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "DevNexus";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    var key = Encoding.UTF8.GetBytes(jwtKey);
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(key)
    };
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // 从SignalR连接查询字符串中获取令牌
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/chat-hub"))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// Add services to the container.
builder.Services.AddProblemDetails();
builder.Services.AddControllers();

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "DevNexus AI API",
        Version = "v1",
        Description = "智能研发工作站 API 文档 - 提供实时 AI 对话、代码生成、文档管理等功能",
        Contact = new OpenApiContact
        {
            Name = "DevNexus Team",
            Email = "dev@devnexus.ai"
        }
    });
    
    // 启用 XML 注释
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
    
    // 配置 JWT 认证
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
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

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "DevNexus AI API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "DevNexus AI API Documentation";
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map SignalR hubs
app.MapHub<ChatHub>("/chat-hub");

// Apply database migrations and seed data
await app.Services.ApplyDatabaseMigrationsAsync();

app.MapDefaultEndpoints();

app.Run();
