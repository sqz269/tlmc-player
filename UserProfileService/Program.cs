using KeycloakAuthProvider.Authentication;
using KeycloakAuthProvider.Service;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using UserProfileService.Data;
using UserProfileService.Data.Api;
using UserProfileService.Data.Impl;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddLogging(loggingBuilder => 
    loggingBuilder.AddConsole());

// Configure Jwt Authentication
builder.Services.AddSingleton<OpenIdConnectConfigurationProviderService>();
builder.Services.ConfigureJwt();
builder.Services.AddTransient<IClaimsTransformation>(_ => new KeycloakClaimTransformer());

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(
    builder.Configuration.GetConnectionString("PostgreSql")));

builder.Services.AddScoped<IUserProfileRepo, UserProfileRepo>();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().Build();
    });
});

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => c.ConfigureOidcSecurityDefinition());

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
//    app.UseSwagger();
//    app.UseSwaggerUI();
//}

app.UseSwagger(opt =>
{
    opt.RouteTemplate = "/swagger/user-profile/{documentName}/swagger.json";
});

app.UseSwaggerUI(opt =>
{
    opt.RoutePrefix = "swagger/user-profile";
    opt.SwaggerEndpoint("/swagger/user-profile/v1/swagger.json", "UserProfile API");
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

PrepDb.Prep(app, app.Environment);

app.Run();
