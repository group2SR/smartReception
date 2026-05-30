using Postgrest.Attributes;
using Postgrest.Models;

[Table("access_logs")]
public class AccessLog : BaseModel
{
    [PrimaryKey("id", false)]
    public int Id { get; set; }

    [Column("client_id")]
    public int ClientId { get; set; }

    [Column("status")]
    public string Status { get; set; } // 'Signed In', 'Signed Out', etc.
}