using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;
using System.Text.Json;
using MonitoramentoConsumo.Model;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);


var redisConnectionString = builder.Configuration.GetConnectionString("RedisConnection");
if (string.IsNullOrEmpty(redisConnectionString))
{
    throw new ArgumentNullException(nameof(redisConnectionString), "A configuração do Redis não pode ser nula.");
}

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    ConnectionMultiplexer.Connect(redisConnectionString));

builder.Services.AddSingleton<IDatabase>(sp =>
    sp.GetRequiredService<IConnectionMultiplexer>().GetDatabase());


builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDB")
);
builder.Services.AddSingleton<IMongoClient, MongoClient>(sp =>
    new MongoClient(builder.Configuration.GetValue<string>("MongoDB:ConnectionString"))
);
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IMongoClient>().GetDatabase(builder.Configuration.GetValue<string>("MongoDB:DatabaseName")).GetCollection<EnergyConsumption>("EnergyConsumption")
);


builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


builder.Services.AddLogging();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapGet("/health", () => Results.Ok("Service is running"));

app.MapPost("/consumo", async (EnergyConsumption consumption, HttpContext context) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    try
    {
        var collection = context.RequestServices.GetRequiredService<IMongoCollection<EnergyConsumption>>();
        await collection.InsertOneAsync(consumption);
        return Results.Created($"/consumo/{consumption.Id}", consumption);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro ao salvar dados de consumo.");
        return Results.StatusCode(500);
    }
});

app.MapGet("/consumo", async (HttpContext context) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var cache = context.RequestServices.GetRequiredService<IDatabase>();
    var collection = context.RequestServices.GetRequiredService<IMongoCollection<EnergyConsumption>>();

    try
    {
        var cachedData = await cache.StringGetAsync("consumoData");
        if (!cachedData.IsNullOrEmpty)
        {
            var consumoLista = JsonSerializer.Deserialize<List<EnergyConsumption>>(cachedData);
            logger.LogInformation("Dados recuperados do cache.");
            return Results.Ok(consumoLista);
        }

        logger.LogInformation("Dados não encontrados no cache, buscando no MongoDB.");
        var consumoList = await collection.Find(_ => true).ToListAsync();
        await cache.StringSetAsync("consumoData", JsonSerializer.Serialize(consumoList), TimeSpan.FromMinutes(5));
        logger.LogInformation("Dados recuperados do MongoDB e armazenados no cache.");
        return Results.Ok(consumoList);
    }
    catch (RedisConnectionException rex)
    {
        logger.LogError(rex, "Erro ao conectar ao Redis.");
        return Results.StatusCode(500);
    }
    catch (MongoException mex)
    {
        logger.LogError(mex, "Erro ao conectar ao MongoDB.");
        return Results.StatusCode(500);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Erro desconhecido.");
        return Results.StatusCode(500);
    }
});

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
