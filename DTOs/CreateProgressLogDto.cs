using System.ComponentModel.DataAnnotations;

namespace FyoraApi.DTOs
{
    public class CreateProgressLogDto
    {
        [Required]
        public int DaysWithoutGambling { get; set; }

        [Required]
        [StringLength(200)]
        public required string Achievement { get; set; } // Usando 'required'
    }
}