var builder = WebApplication.CreateBuilder(args);

// 1. Configura a leitura do appsettings.json para as classes de settings
builder.Services.Configure<PontoDatabaseSettings>(
    builder.Configuration.GetSection("PontoDatabaseSettings"));

// 2. Adiciona nosso serviço de lógica ao container de injeção de dependência
builder.Services.AddSingleton<JornadaService>();

// Adiciona serviços padrão da API
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configura o pipeline de requisições HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 3. CRIA O NOSSO ENDPOINT!
app.MapPost("/api/jornada/marcar", async (JornadaService jornadaService) =>
{
    try
    {
        var jornadaAtualizada = await jornadaService.MarcarPontoAsync();
        return Results.Ok(jornadaAtualizada);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("MarcarPonto")
.WithSummary("Registra um evento de ponto sequencialmente para o dia atual.");

app.MapGet("/api/jornada/status", async (JornadaService jornadaService) =>
{
    try
    {
        var status = await jornadaService.GetStatusDoDiaAsync();
        return Results.Ok(status);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
})
.WithName("GetStatusJornada")
.WithSummary("Retorna o status calculado da jornada do dia atual.");

app.Run();