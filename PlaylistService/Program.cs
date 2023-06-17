using KeycloakAuthProvider.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using PlaylistService.Data;
using PlaylistService.Data.Api;
using PlaylistService.Data.Impl;
using PlaylistService.Model;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient();

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(opt => 
    opt.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSql")));

builder.Services.ConfigureJwt(
        builder.Environment.IsDevelopment(),
        builder.Configuration.GetSection("Keycloak")["RsaPublicKey"],
        builder.Configuration.GetSection("Keycloak")["RealmUrl"]
    );

builder.Services.AddTransient<IClaimsTransformation>(provider =>
{
    var config = provider.GetService<IConfiguration>();
    return new ClaimTransformer(config.GetSection("Keycloak")["Realm"]);
});

builder.Services.AddScoped<IPlaylistRepo, PlaylistRepo>();
builder.Services.AddScoped<IPlaylistItemRepo, PlaylistItemRepo>();

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
        opt.SerializerSettings.Converters.Add(new StringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.ConfigureOidcSecurityDefinition())
    .AddSwaggerGenNewtonsoftSupport();

var app = builder.Build();

app.UseCors();
// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

app.UseSwagger(opt =>
{
    opt.RouteTemplate = "/swagger/playlist/{documentName}/swagger.json";
});

app.UseSwaggerUI(opt =>
{
    opt.RoutePrefix = "swagger/playlist";
    opt.SwaggerEndpoint("/swagger/playlist/v1/swagger.json", "Playlist API");
});

// app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

PrepDb.Prep(app, builder.Environment);

app.Run();
