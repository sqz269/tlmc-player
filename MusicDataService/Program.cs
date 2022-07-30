using Microsoft.Extensions.Options;
using MusicDataService.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.Configure<AlbumDatabaseSettings>(options =>
{
    builder.Configuration.GetSection(nameof(AlbumDatabaseSettings)).Bind(options);
});
builder.Services.AddSingleton<IAlbumDatabaseSettings>(service =>
    service.GetRequiredService<IOptions<AlbumDatabaseSettings>>().Value);

builder.Services.AddScoped<IAlbumRepo, AlbumRepo>();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
