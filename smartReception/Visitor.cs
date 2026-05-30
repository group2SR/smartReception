using Postgrest.Attributes;
using Postgrest.Models;

[Table("clients")]
public class Visitor : BaseModel
{
    [PrimaryKey("id", false)]
    public int Id { get; set; }

    [Column("first_name")]
    public string FirstName { get; set; }

    [Column("last_name")]
    public string LastName { get; set; }

    [Column("email")]
    public string Email { get; set; } // REQUIRED by your SQL

    [Column("phone_number")]
    public string PhoneNumber { get; set; } // REQUIRED by your SQL

    [Column("nin")]
    public string NIN { get; set; }
}