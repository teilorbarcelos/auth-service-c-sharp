using System.ComponentModel.DataAnnotations;

namespace MageBackend.Domain
{
    public abstract class BaseEntity
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public bool Active { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public abstract class SoftDeletableEntity : BaseEntity
    {
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}
