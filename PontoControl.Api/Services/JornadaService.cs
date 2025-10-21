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

    public async Task<JornadaStatusDto> GetStatusDoDiaAsync()
    {
            var hoje = DateTime.UtcNow.Date;
            var jornadaDeHoje = await _jornadasCollection.Find(j => j.Data == hoje).FirstOrDefaultAsync();

            if (jornadaDeHoje == null || jornadaDeHoje.InicioJornada == null)
            {
                return new JornadaStatusDto { Mensagem = "Nenhuma jornada iniciada hoje." };
            }

            // --- Lógica de Cálculo de Tempo ---
            TimeSpan tempoTrabalhado = TimeSpan.Zero;
            DateTime? horaSaidaPrevista = null;

            // Garante que os valores não são nulos antes de calcular
            var inicioJornada = jornadaDeHoje.InicioJornada;
            var inicioAlmoco = jornadaDeHoje.InicioAlmoco;
            var fimAlmoco = jornadaDeHoje.FimAlmoco;
            var fimJornada = jornadaDeHoje.FimJornada;

            // Calcula o período da manhã
            if (inicioJornada.HasValue && inicioAlmoco.HasValue)
            {
                tempoTrabalhado += inicioAlmoco.Value - inicioJornada.Value;
            }

            // Calcula o período da tarde
            if (fimAlmoco.HasValue)
            {
                // Se a jornada não terminou, calcula até o momento atual.
                var fimDoCalculo = fimJornada ?? DateTime.UtcNow;
                tempoTrabalhado += fimDoCalculo - fimAlmoco.Value;
            }

            // Calcula a previsão de saída
            if (inicioJornada.HasValue)
            {
                var tempoDeAlmoco = (fimAlmoco ?? inicioAlmoco ?? DateTime.UtcNow) - (inicioAlmoco ?? DateTime.UtcNow);
                if (tempoDeAlmoco < TimeSpan.Zero) tempoDeAlmoco = TimeSpan.Zero;

                var meta = TimeSpan.FromMinutes(jornadaDeHoje.CargaHorariaMetaMinutos);
                horaSaidaPrevista = inicioJornada.Value + meta + tempoDeAlmoco;
            }

            // Formata as marcações para exibição
            var marcacoes = new List<string>();
            if (inicioJornada.HasValue) marcacoes.Add($"Entrada 1: {inicioJornada.Value.ToLocalTime():HH:mm:ss}");
            if (inicioAlmoco.HasValue) marcacoes.Add($"Saída Almoço: {inicioAlmoco.Value.ToLocalTime():HH:mm:ss}");
            if (fimAlmoco.HasValue) marcacoes.Add($"Entrada Almoço: {fimAlmoco.Value.ToLocalTime():HH:mm:ss}");
            if (fimJornada.HasValue) marcacoes.Add($"Saída Final: {fimJornada.Value.ToLocalTime():HH:mm:ss}");

            return new JornadaStatusDto
            {
                Mensagem = "Status da jornada atual.",
                TempoTrabalhadoHoje = $"{(int)tempoTrabalhado.TotalHours:00}:{tempoTrabalhado.Minutes:00}:{tempoTrabalhado.Seconds:00}",
                HoraSaidaPrevista = horaSaidaPrevista?.ToLocalTime(),
                StatusAtual = jornadaDeHoje.Status,
                Marcacoes = marcacoes
            };
    }
}

// Classe auxiliar para carregar as configurações do appsettings.json
public class PontoDatabaseSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string CollectionName { get; set; } = null!;
}