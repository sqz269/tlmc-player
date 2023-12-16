using FFMpegCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using MusicDataService.Data;
using MusicDataService.Data.Api;
using MusicDataService.Data.Impl;
using Newtonsoft.Json;
using KeycloakAuthProvider.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using KeycloakAuthProvider.Service;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.
    AddEnvironmentVariables();

// Configure Jwt Authentication
builder.Services.AddSingleton<OpenIdConnectConfigurationProviderService>();
builder.Services.ConfigureJwt();
builder.Services.AddTransient<IClaimsTransformation>(_ => new KeycloakClaimTransformer());

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSql")));

if (!Directory.Exists(builder.Configuration["FFMpegBinary"]))
{
    throw new DirectoryNotFoundException(
        $"Invalid FFMpeg Directory. ({builder.Configuration["FFMpegBinary"]})");
}

GlobalFFOptions.Configure(opt =>
{
    opt.BinaryFolder = builder.Configuration["FFMpegBinary"];
});

builder.Services.AddScoped<IAlbumRepo, AlbumRepo>();
builder.Services.AddScoped<ITrackRepo, TrackRepo>();
builder.Services.AddScoped<ICircleRepo, CircleRepo>();
builder.Services.AddScoped<IAssetRepo, AssetRepo>();
builder.Services.AddScoped<IHlsPlaylistRepo, HlsPlaylistRepo>();
builder.Services.AddScoped<IOriginalTrackRepo, OriginalTrackRepo>();
builder.Services.AddScoped<IOriginalAlbumRepo, OriginalAlbumRepo>();

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().Build();
    });
});

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Information);

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers()
    .AddNewtonsoftJson(opt =>
    {
        opt.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        opt.SerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.None;

        // Convert enum to string
        opt.SerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => 
        c.ConfigureOidcSecurityDefinition())
    .AddSwaggerGenNewtonsoftSupport();

var app = builder.Build();

app.UseCors();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{

Console.WriteLine("7/12/2023");
if (app.Environment.IsProduction())
{
    app.Use((context, next) =>
    {
        context.Request.Scheme = "https";
        return next(context);
    });
}
app.UseSwagger(opt =>
{
    opt.RouteTemplate = "/swagger/music-data/{documentName}/swagger.json";
});

app.UseSwaggerUI(opt =>
{
    opt.RoutePrefix = "swagger/music-data";
    opt.SwaggerEndpoint("/swagger/music-data/v1/swagger.json", "MusicData API");
});
//}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

Console.WriteLine($"Running: {app.Environment.EnvironmentName}");

PrepDb.Prep(app, app.Environment);

await UpdateDb.Update(app, app.Environment);

app.Run();
