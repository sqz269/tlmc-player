using System.Text.Json.Serialization;
using AuthServiceClientApi;
using AuthServiceClientApi.KeyProviders;
using FFMpegCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MusicDataService.Data;
using MusicDataService.Data.Api;
using MusicDataService.Data.Impl;
using MusicDataService.DataService;
using MusicDataService.DataService.SyncDataService.Grpc;
using MusicDataService.Utils;
using Newtonsoft.Json;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.
    AddEnvironmentVariables();

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
builder.Services.AddScoped<IOriginalTrackRepo, OriginalTrackRepo>();
builder.Services.AddScoped<IOriginalAlbumRepo, OriginalAlbumRepo>();

builder.Services.AddSingleton<IAuthDataClient, GrpcAuthDataClient>();
builder.Services.AddSingleton<IJwtKeyProvider, JwtKeyFromHttpAuthDataService>();
builder.Services.AddSingleton<JwtManager>();

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().Build();
    });
});

builder.Logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddHttpContextAccessor();

builder.Services.AddControllers()
    .AddNewtonsoftJson(opt =>
    {
        opt.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
        opt.SerializerSettings.PreserveReferencesHandling = PreserveReferencesHandling.None;
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen()
    .AddSwaggerGenNewtonsoftSupport();

var app = builder.Build();

app.UseCors();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger(opt =>
{
    opt.RouteTemplate = "/swagger/music-data/{documentName}/swagger.json";
});

app.UseSwaggerUI(opt =>
{
    opt.RoutePrefix = "swagger/music-data";
    opt.SwaggerEndpoint("/swagger/music-data/v1/swagger.json", "Auth API");
});
//}

// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

Console.WriteLine($"Running: {app.Environment.EnvironmentName}");

PrepDb.Prep(app, app.Environment);

await UpdateDb.Update(app, app.Environment);

app.Run();
