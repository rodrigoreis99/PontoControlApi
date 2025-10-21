using MongoDB.Driver;
using Microsoft.Extensions.Options;

public class JornadaService
{
    private readonly IMongoCollection<JornadaDiaria> _jornadasCollection;

    // O construtor recebe a configuração do banco de dados
    public JornadaService(IOptions<PontoDatabaseSettings> pontoDatabaseSettings)
    {
        var mongoClient = new MongoClient(pontoDatabaseSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(pontoDatabaseSettings.Value.DatabaseName);
        _jornadasCollection = mongoDatabase.GetCollection<JornadaDiaria>(pontoDatabaseSettings.Value.CollectionName);
    }

    // O método principal que faz a mágica acontecer
    public async Task<JornadaDiaria> MarcarPontoAsync()
    {
        var hoje = DateTime.UtcNow.Date;

        // 1. Encontra a jornada de hoje ou cria uma nova se não existir
        var jornadaDeHoje = await _jornadasCollection.Find(j => j.Data == hoje).FirstOrDefaultAsync();

        if (jornadaDeHoje == null)
        {
            jornadaDeHoje = new JornadaDiaria { Data = hoje };
        }

        var agora = DateTime.UtcNow;

        // 2. Lógica sequencial para preencher as marcações
        if (jornadaDeHoje.InicioJornada == null)
        {
            jornadaDeHoje.InicioJornada = agora;
            jornadaDeHoje.Status = StatusJornada.EmAndamento;
        }
        else if (jornadaDeHoje.InicioAlmoco == null)
        {
            jornadaDeHoje.InicioAlmoco = agora;
            jornadaDeHoje.Status = StatusJornada.EmPausa;
        }
        else if (jornadaDeHoje.FimAlmoco == null)
        {
            jornadaDeHoje.FimAlmoco = agora;
            jornadaDeHoje.Status = StatusJornada.EmAndamento;
        }
        else if (jornadaDeHoje.FimJornada == null)
        {
            jornadaDeHoje.FimJornada = agora;
            jornadaDeHoje.Status = StatusJornada.Finalizada;
        }
        else
        {
            // Opcional: Lançar um erro ou apenas retornar a jornada,
            // pois todas as marcações do dia já foram feitas.
            throw new Exception("Todas as 4 marcações do dia já foram realizadas.");
        }

        // 3. Salva as alterações no MongoDB (Update ou Insert)
        var filter = Builders<JornadaDiaria>.Filter.Eq(s => s.Id, jornadaDeHoje.Id);
        await _jornadasCollection.ReplaceOneAsync(filter, jornadaDeHoje, new ReplaceOptions { IsUpsert = true });

        return jornadaDeHoje;
    }
}

// Classe auxiliar para carregar as configurações do appsettings.json
public class PontoDatabaseSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string CollectionName { get; set; } = null!;
}