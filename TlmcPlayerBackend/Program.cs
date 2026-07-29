using FFMpegCore;
using KeycloakAuthProvider.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Npgsql;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Utils;
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
NpgsqlConnection.GlobalTypeMapper.UseVector();

// Host and database only: the full connection string contains the password, and
// this line put it in container logs on every boot.
var pgConnectionString = builder.Configuration.GetConnectionString("PostgreSql");
{
    var redacted = string.Join(';', (pgConnectionString ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Where(part => !part.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase)));
    Console.WriteLine($"Connecting to: {redacted}");
}
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(pgConnectionString, optionsBuilder =>
    {
        optionsBuilder.CommandTimeout(5);
        optionsBuilder.UseVector();
    }));

builder.Services.AddScoped<IAlbumRepo, AlbumRepo>();
builder.Services.AddScoped<ITrackRepo, TrackRepo>();
builder.Services.AddScoped<ICircleRepo, CircleRepo>();
builder.Services.AddScoped<IAssetRepo, AssetRepo>();
builder.Services.AddScoped<IHlsPlaylistRepo, HlsPlaylistRepo>();
builder.Services.AddScoped<IOriginalTrackRepo, OriginalTrackRepo>();
builder.Services.AddScoped<IOriginalAlbumRepo, OriginalAlbumRepo>();
builder.Services.AddScoped<IDashPlaylistRepo, DashPlaylistRepo>();

builder.Services.AddScoped<IUserProfileRepo, UserProfileRepo>();

builder.Services.AddScoped<IPlaylistRepo, PlaylistRepo>();
builder.Services.AddScoped<IPlaylistItemRepo, PlaylistItemRepo>();

// Configure Jwt Authentication
builder.Services.ConfigureJwt(builder.Configuration);
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

// Asset rows hold absolute filesystem paths that are opened directly, so what counts
// as a servable path is a deployment decision (Assets:Roots).
builder.Services.AddSingleton<AssetPathPolicy>();

// Liveness/readiness target for container orchestrators. Deliberately does not
// touch the database: this answers "is the process serving?", and the startup
// migration/backfill already gates when the process begins listening.
builder.Services.AddHealthChecks();

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

app.UseCors();

// Configure the HTTP request pipeline.
// The environment check here was commented out, so the full OpenAPI document -- every
// route, parameter and DTO, including the api/internal write surface -- was served
// publicly. Development still gets it by default; anywhere else has to ask for it.
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Swagger:Enabled"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

if (app.Environment.IsProduction())
{
    app.Use((context, next) =>
    {
        context.Request.Scheme = "https";
        return next(context);
    });
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapHealthChecks("/healthz").AllowAnonymous();

app.MapControllers();

PrepDb.Prep(app, app.Environment);
await UpdateDb.Update(app, app.Environment);

app.Run();
