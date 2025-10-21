public class JornadaStatusDto
{
    public string Mensagem { get; set; } = string.Empty;
    public string TempoTrabalhadoHoje { get; set; } = "00:00:00";
    public DateTime? HoraSaidaPrevista { get; set; }
    public StatusJornada StatusAtual { get; set; }
    public List<string> Marcacoes { get; set; } = new();
}