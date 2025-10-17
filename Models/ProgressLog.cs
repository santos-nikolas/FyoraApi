namespace FyoraApi.Models
{
    public class ProgressLog
    {
        public int Id { get; set; }
        public DateTime LogDate { get; set; } = DateTime.UtcNow;
        public int DaysWithoutGambling { get; set; }
        public required string Achievement { get; set; } // Usando 'required'
        public int UserId { get; set; }
        public virtual User? User { get; set; }
    }
}