using MongoDB.Driver;
using Microsoft.Extensions.Options;

public class JornadaService
{
    private readonly IMongoCollection<JornadaDiaria> _jornadasCollection;

    // O construtor recebe a configura��o do banco de dados
    public JornadaService(IOptions<PontoDatabaseSettings> pontoDatabaseSettings)
    {
        var mongoClient = new MongoClient(pontoDatabaseSettings.Value.ConnectionString);
        var mongoDatabase = mongoClient.GetDatabase(pontoDatabaseSettings.Value.DatabaseName);
        _jornadasCollection = mongoDatabase.GetCollection<JornadaDiaria>(pontoDatabaseSettings.Value.CollectionName);
    }

    // O m�todo principal que faz a m�gica acontecer
    public async Task<JornadaDiaria> MarcarPontoAsync()
    {
        var hoje = DateTime.UtcNow.Date;

        // 1. Encontra a jornada de hoje ou cria uma nova se n�o existir
        var jornadaDeHoje = await _jornadasCollection.Find(j => j.Data == hoje).FirstOrDefaultAsync();

        if (jornadaDeHoje == null)
        {
            jornadaDeHoje = new JornadaDiaria { Data = hoje };
        }

        var agora = DateTime.UtcNow;

        // 2. L�gica sequencial para preencher as marca��es
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
            // Opcional: Lan�ar um erro ou apenas retornar a jornada,
            // pois todas as marca��es do dia j� foram feitas.
            throw new Exception("Todas as 4 marca��es do dia j� foram realizadas.");
        }

        // 3. Salva as altera��es no MongoDB (Update ou Insert)
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

            // --- L�gica de C�lculo de Tempo ---
            TimeSpan tempoTrabalhado = TimeSpan.Zero;
            DateTime? horaSaidaPrevista = null;

            // Garante que os valores n�o s�o nulos antes de calcular
            var inicioJornada = jornadaDeHoje.InicioJornada;
            var inicioAlmoco = jornadaDeHoje.InicioAlmoco;
            var fimAlmoco = jornadaDeHoje.FimAlmoco;
            var fimJornada = jornadaDeHoje.FimJornada;

            // Calcula o per�odo da manh�
            if (inicioJornada.HasValue && inicioAlmoco.HasValue)
            {
                tempoTrabalhado += inicioAlmoco.Value - inicioJornada.Value;
            }

            // Calcula o per�odo da tarde
            if (fimAlmoco.HasValue)
            {
                // Se a jornada n�o terminou, calcula at� o momento atual.
                var fimDoCalculo = fimJornada ?? DateTime.UtcNow;
                tempoTrabalhado += fimDoCalculo - fimAlmoco.Value;
            }

            // Calcula a previs�o de sa�da
            if (inicioJornada.HasValue)
            {
                var tempoDeAlmoco = (fimAlmoco ?? inicioAlmoco ?? DateTime.UtcNow) - (inicioAlmoco ?? DateTime.UtcNow);
                if (tempoDeAlmoco < TimeSpan.Zero) tempoDeAlmoco = TimeSpan.Zero;

                var meta = TimeSpan.FromMinutes(jornadaDeHoje.CargaHorariaMetaMinutos);
                horaSaidaPrevista = inicioJornada.Value + meta + tempoDeAlmoco;
            }

            // Formata as marca��es para exibi��o
            var marcacoes = new List<string>();
            if (inicioJornada.HasValue) marcacoes.Add($"Entrada 1: {inicioJornada.Value.ToLocalTime():HH:mm:ss}");
            if (inicioAlmoco.HasValue) marcacoes.Add($"Sa�da Almo�o: {inicioAlmoco.Value.ToLocalTime():HH:mm:ss}");
            if (fimAlmoco.HasValue) marcacoes.Add($"Entrada Almo�o: {fimAlmoco.Value.ToLocalTime():HH:mm:ss}");
            if (fimJornada.HasValue) marcacoes.Add($"Sa�da Final: {fimJornada.Value.ToLocalTime():HH:mm:ss}");

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

// Classe auxiliar para carregar as configura��es do appsettings.json
public class PontoDatabaseSettings
{
    public string ConnectionString { get; set; } = null!;
    public string DatabaseName { get; set; } = null!;
    public string CollectionName { get; set; } = null!;
}