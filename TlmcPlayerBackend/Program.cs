using FFMpegCore;
using KeycloakAuthProvider.Authentication;
using KeycloakAuthProvider.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Npgsql;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Data.Api.MusicData;
using TlmcPlayerBackend.Data.Api.Playlist;
using TlmcPlayerBackend.Data.Api.UserProfile;
using TlmcPlayerBackend.Data.Impl.MusicData;
using TlmcPlayerBackend.Data.Impl.Playlist;
using TlmcPlayerBackend.Data.Impl.UserProfile;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Register DB Related services
NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSql")));

builder.Services.AddScoped<IAlbumRepo, AlbumRepo>();
builder.Services.AddScoped<ITrackRepo, TrackRepo>();
builder.Services.AddScoped<ICircleRepo, CircleRepo>();
builder.Services.AddScoped<IAssetRepo, AssetRepo>();
builder.Services.AddScoped<IHlsPlaylistRepo, HlsPlaylistRepo>();
builder.Services.AddScoped<IOriginalTrackRepo, OriginalTrackRepo>();
builder.Services.AddScoped<IOriginalAlbumRepo, OriginalAlbumRepo>();

builder.Services.AddScoped<IUserProfileRepo, UserProfileRepo>();

builder.Services.AddScoped<IPlaylistRepo, PlaylistRepo>();
builder.Services.AddScoped<IPlaylistItemRepo, PlaylistItemRepo>();

// Configure Jwt Authentication
builder.Services.AddSingleton<OpenIdConnectConfigurationProviderService>();
builder.Services.ConfigureJwt();
builder.Services.AddTransient<IClaimsTransformation>(_ => new KeycloakClaimTransformer());

if (!Directory.Exists(builder.Configuration["FFMpegDirectory"]))
{
    throw new DirectoryNotFoundException(
        $"Invalid FFMpeg Directory. ({builder.Configuration["FFMpegDirectory"]})");
}

GlobalFFOptions.Configure(opt =>
{
    opt.BinaryFolder = builder.Configuration["FFMpegDirectory"];
});

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddHttpContextAccessor();

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().Build();
    });
});

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

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

PrepDb.Prep(app, app.Environment);

app.Run();
