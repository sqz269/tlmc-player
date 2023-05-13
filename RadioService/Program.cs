using RadioService;
using RadioService.SyncDataService;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddEnvironmentVariables();

// Add services to the container.
builder.Services.AddHttpClient();

//builder.Services.AddLogging();

builder.Logging.AddConsole();

builder.Services.AddSingleton<MusicDataHttpApi>();
builder.Services.AddSingleton<RadioSongProviderService>();
builder.Services.AddHostedService<RadioEnqueuerService>();

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
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger(opt =>
{
    opt.RouteTemplate = "/swagger/radio/{documentName}/swagger.json";
});

app.UseSwaggerUI(opt =>
{
    opt.RoutePrefix = "swagger/radio";
    opt.SwaggerEndpoint("/swagger/radio/v1/swagger.json", "Radio API");
});
//}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
