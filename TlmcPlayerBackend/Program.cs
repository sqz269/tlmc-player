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
using TlmcPlayerBackend.Integrations.Meilisearch;
using TlmcPlayerBackend.Integrations.MeiliSearch.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Register DB Related services
NpgsqlConnection.GlobalTypeMapper.EnableDynamicJson();
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSql"), optionsBuilder =>
    {
        optionsBuilder.CommandTimeout(5);
    }));

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

// Search services
builder.Services.AddSingleton<MeilisearchDbContext>(c => new MeilisearchDbContext(
    "http://localhost:7700",
    "23d34ab0da04c66dd2d3152575aa1e3adf5803b78c721617a13cb33030dfc1ea"));

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

app.UseCors();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger();
app.UseSwaggerUI();
//}

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

app.MapControllers();

PrepDb.Prep(app, app.Environment);

var meilisearchInit = new PrepMeilisearch(app, app.Environment);
await meilisearchInit.PrepDb();

app.Run();
