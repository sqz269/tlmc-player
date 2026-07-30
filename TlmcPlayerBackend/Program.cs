using KeycloakAuthProvider.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Data.Repos;
using TlmcPlayerBackend.Ids;
using TlmcPlayerBackend.Search;
using TlmcPlayerBackend.Subsonic;
using TlmcPlayerBackend.Utils;

var builder = WebApplication.CreateBuilder(args);

// Host and database only: the full connection string contains the password, and
// this line put it in container logs on every boot.
var pgConnectionString = builder.Configuration.GetConnectionString("PostgreSql");
{
    var redacted = string.Join(';', (pgConnectionString ?? string.Empty)
        .Split(';', StringSplitOptions.RemoveEmptyEntries)
        .Where(part => !part.TrimStart().StartsWith("Password", StringComparison.OrdinalIgnoreCase)));
    Console.WriteLine($"Connecting to: {redacted}");
}

var dataSource = AppDbOptions.BuildDataSource(pgConnectionString);
builder.Services.AddDbContext<AppDbContext>(opt => AppDbOptions.Configure(opt, dataSource));

builder.Services.AddScoped<IReleaseRepo, ReleaseRepo>();
builder.Services.AddScoped<ITrackRepo, TrackRepo>();
builder.Services.AddScoped<ICircleRepo, CircleRepo>();
builder.Services.AddScoped<IOriginalRepo, OriginalRepo>();
builder.Services.AddScoped<ISimilarityRepo, SimilarityRepo>();

builder.Services.AddScoped<IUserProfileRepo, UserProfileRepo>();

builder.Services.AddScoped<IPlaylistRepo, PlaylistRepo>();
builder.Services.AddScoped<IQueueRepo, QueueRepo>();
builder.Services.AddScoped<IPlayEventRepo, PlayEventRepo>();

// Rows hold storage keys; the roots they resolve against are configuration.
builder.Services.AddSingleton<StorageRootResolver>();

// Meilisearch (SCHEMA-V6.md section 7). The index is a projection, never a source
// of truth: with no engine configured (or the engine down) search answers 503 and
// nothing else notices. SearchIndexBootstrap applies the index settings — the CJK
// localizedAttributes in particular — at startup, because their absence is silent.
builder.Services.Configure<SearchOptions>(builder.Configuration.GetSection(SearchOptions.Section));
builder.Services.AddHttpClient<MeiliClient>((sp, http) =>
{
    var search = sp.GetRequiredService<IOptions<SearchOptions>>().Value;
    if (search.Enabled)
    {
        http.BaseAddress = new Uri(search.Url!.TrimEnd('/') + "/");
        if (!string.IsNullOrEmpty(search.ApiKey))
        {
            http.DefaultRequestHeaders.Authorization = new("Bearer", search.ApiKey);
        }
    }

    http.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddScoped<ISearchIndexService, SearchIndexService>();
builder.Services.AddHostedService<SearchIndexBootstrap>();

// The Subsonic facade (Docs/SUBSONIC.md). Off by default; when disabled the
// auth filter answers 404 for every /rest route, same posture as Search.
builder.Services.Configure<SubsonicOptions>(builder.Configuration.GetSection(SubsonicOptions.Section));
builder.Services.AddScoped<SubsonicAuthFilter>();
builder.Services.AddScoped<SubsonicQueries>();

// Configure Jwt Authentication
builder.Services.ConfigureJwt(builder.Configuration);
builder.Services.AddTransient<IClaimsTransformation>(_ => new KeycloakClaimTransformer());

builder.Services.AddHttpContextAccessor();

// Liveness/readiness target for container orchestrators. Deliberately does not
// touch the database: this answers "is the process serving?". Migrations no longer
// run at startup — they are an explicit `dotnet ef database update` (or a Job).
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

        // snake_case on the wire, matching the SQL identifiers and the stored jsonb
        // keys (SCHEMA-V6.md sections 1 and 15). The enum converter needs the naming
        // strategy passed explicitly — the contract resolver does not reach enum
        // values, which is exactly how the old camelCase/PascalCase mix arose.
        opt.SerializerSettings.ContractResolver = new DefaultContractResolver
        {
            NamingStrategy = new SnakeCaseNamingStrategy(),
        };
        opt.SerializerSettings.Converters.Add(
            new StringEnumConverter(new SnakeCaseNamingStrategy()));

        // Ids serialize as prefixed TypeID strings, not {"value": ...} objects.
        opt.SerializerSettings.AddEntityIdConverters();
    });

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
    {
        c.ConfigureOidcSecurityDefinition();
        c.MapEntityIds();
    })
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

app.Run();
