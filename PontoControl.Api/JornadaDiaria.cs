using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

public enum StatusJornada
{
    Pendente,
    EmAndamento,
    EmPausa,
    Finalizada
}

public class JornadaDiaria
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonElement("Data")]
    public DateTime Data { get; set; }

    [BsonElement("InicioJornada")]
    public DateTime? InicioJornada { get; set; }

    [BsonElement("InicioAlmoco")]
    public DateTime? InicioAlmoco { get; set; }

    [BsonElement("FimAlmoco")]
    public DateTime? FimAlmoco { get; set; }

    [BsonElement("FimJornada")]
    public DateTime? FimJornada { get; set; }

    [BsonElement("Status")]
    [BsonRepresentation(BsonType.String)]
    public StatusJornada Status { get; set; }

    // Meta em minutos (8h 48m = 528 minutos)
    [BsonElement("CargaHorariaMetaMinutos")]
    public int CargaHorariaMetaMinutos { get; set; } = 528;
}