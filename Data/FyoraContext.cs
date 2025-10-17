using FyoraApi.Models; // Ajuste o namespace se for diferente
using Microsoft.EntityFrameworkCore;

namespace FyoraApi.Data
{
    public class FyoraContext : DbContext
    {
        public FyoraContext(DbContextOptions<FyoraContext> options) : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<ProgressLog> ProgressLogs { get; set; }
    }
}