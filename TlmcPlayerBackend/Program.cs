using KeycloakAuthProvider.Authentication;
using Microsoft.AspNetCore.Authentication;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using TlmcPlayerBackend.Data;
using TlmcPlayerBackend.Ids;

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

// Repositories are re-registered here as the v6 port brings them back.

// Configure Jwt Authentication
builder.Services.ConfigureJwt(builder.Configuration);
builder.Services.AddTransient<IClaimsTransformation>(_ => new KeycloakClaimTransformer());

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

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
