using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Core.Database.Models;

public class User
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    [StringLength(50)]
    [Column(TypeName = "varchar(50)")]
    public string Username { get; set; }
    
    [Required]
    public byte[] PublicKeyEd25519 { get; set; }
    
    [Required]
    public byte[] PublicKeyX25519 { get; set; }
    
    [Required]
    [StringLength(64)]
    [Column(TypeName = "varchar(64)")]
    public string KeyFingerprint { get; set; }
    
    public string? PasswordHash { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    
    public virtual ICollection<Friendship> InitiatedFriendships { get; set; } = new List<Friendship>();
    
    public virtual ICollection<Friendship> ReceivedFriendships { get; set; } = new List<Friendship>();
}
