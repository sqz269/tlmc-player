using System.Reflection;
using System.Text.Json.Serialization;
using AuthenticationService.Data;
using AuthenticationService.SyncDataService.Grpc;
using AuthServiceClientApi;
using AuthServiceClientApi.KeyProviders;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(opt =>
{
    // this allows gRPC calls over http (without TLS)
    //opt.ConfigureEndpointDefaults(option =>
    //{
    //    option.Protocols = HttpProtocols.Http2;
    //});

    opt.ListenAnyIP(80, options =>
    {
        options.Protocols = HttpProtocols.Http1;
    });

    //opt.ListenAnyIP(443, options =>
    //{
    //    options.Protocols = HttpProtocols.Http2;
    //});
});

// Add services to the container.

if (builder.Environment.IsDevelopment())
{
    Console.WriteLine("--> Using In Memory Database");
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseInMemoryDatabase("InMemory"));
}
else
{
    Console.WriteLine("--> Using Production PostgreSQL Database");
    builder.Services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSql")));
}

builder.Services.AddGrpc(options =>
{
    options.EnableDetailedErrors = true;
});

builder.Services.AddScoped<IUserRepo, UserRepo>();
builder.Services.AddScoped<IRoleRepo, RoleRepo>();

builder.Services.AddSingleton<IJwtKeyProvider, JwtKeyFromConfiguration>();
builder.Services.AddSingleton<JwtManager>();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddControllers()
    .AddJsonOptions(opt =>
    {
        opt.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Jwt", new OpenApiSecurityScheme
    {
        Description = @"JWT Authorization header using the Bearer scheme. <br> 
                      Enter 'Jwt' [space] and then your token in the text input below.
                      <br> Example: 'Jwt 12345abcdef'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Jwt"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement()
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Jwt"
                },
                Scheme = "oauth2",
                Name = "Jwt",
                In = ParameterLocation.Header,

            },
            new List<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

//app.UseHttpsRedirection();

// app.UseAuthorization();

app.MapGrpcService<GrpcAuthDataService>();

app.MapControllers();

app.Run();
