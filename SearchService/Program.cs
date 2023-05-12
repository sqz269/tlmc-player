using Elasticsearch.Net;
using Elasticsearch;
using Nest;
using System.Linq;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var esNodes = builder.Configuration
    .GetSection("ElasticSearch")
    .GetSection("ClusterNodes")
    .Get<string[]>()
    .Select(i => new Uri(i))
    .ToArray();

//var username = builder.Configuration["ElasticSearch:Username"];
//var password = builder.Configuration["ElasticSearch:Password"];

Console.WriteLine($"Using ES Nodes: {string.Join(" ", esNodes.Select(n => n.ToString()))}");
Console.WriteLine("ES NOT USING AUTHENTICATION");

Console.WriteLine("Initializing ES Client");
var highLevelClient = new ElasticClient(new ConnectionSettings(new StaticConnectionPool(esNodes)));
Console.WriteLine("ES Client Initialized");

var results = highLevelClient.Cat.Indices();

Console.WriteLine($"ES Indices: {string.Join(", ", results.Records.Select(e => e.Index))}");

builder.Services.AddSingleton<IElasticClient>(highLevelClient);

builder.Services.AddCors(opt =>
{
    opt.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin().Build();
    });
});

var app = builder.Build();

app.UseCors();

// Configure the HTTP request pipeline.
//if (app.Environment.IsDevelopment())
//{
app.UseSwagger(opt =>
{
    opt.RouteTemplate = "/swagger/search-api/{documentName}/swagger.json";
});
app.UseSwaggerUI(opt =>
{
    opt.RoutePrefix = "swagger/search-api";
    opt.SwaggerEndpoint("/swagger/search-api/v1/swagger.json", "Search API");
});
//}

//app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
