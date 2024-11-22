using Xunit;
using Moq;
using MongoDB.Driver;
using MonitoramentoConsumo.Model;
using Microsoft.AspNetCore.Http;

public class EnergyConsumptionInsertTests
{
    [Fact]
    public async Task Insercao_De_Dados_No_MongoDB()
    {
        var mockCollection = new Mock<IMongoCollection<EnergyConsumption>>();
        var mockContext = new DefaultHttpContext();
        var consumo = new EnergyConsumption { Id = "123", Timestamp = DateTime.Now, Consumption = 100 };

        // Simular inserção de dados
        mockCollection.Setup(c => c.InsertOneAsync(consumo, null, default)).Returns(Task.CompletedTask);

        // Simulação de método MapPost
        var result = await app.MapPost("/consumo", consumo, mockContext);

        Assert.Equal(201, result.StatusCode);
    }
}
