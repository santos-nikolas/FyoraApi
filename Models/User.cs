namespace FyoraApi.Models
{
    public class User
    {
        public int Id { get; set; }
        public required string Nickname { get; set; } // Usando 'required' para garantir que não seja nulo
        public required string Email { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public virtual ICollection<ProgressLog> ProgressLogs { get; set; } = new List<ProgressLog>();
    }
}