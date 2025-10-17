using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FyoraApi.Data;
using FyoraApi.Models;
using FyoraApi.DTOs; // Adicionar o using para os DTOs

namespace FyoraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly FyoraContext _context;

        public UsersController(FyoraContext context)
        {
            _context = context;
        }

        // GET: api/users
        // Exemplo de pesquisa com LINQ
        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUsers([FromQuery] string? nickname)
        {
            IQueryable<User> query = _context.Users;

            if (!string.IsNullOrEmpty(nickname))
            {
                // Pesquisa com LINQ usando Where e Contains
                query = query.Where(u => u.Nickname.ToLower().Contains(nickname.ToLower()));
            }

            // Usar .AsNoTracking() para consultas de apenas leitura melhora o desempenho
            return await query.AsNoTracking().ToListAsync();
        }

        // GET: api/users/5
        [HttpGet("{id}")]
        public async Task<ActionResult<User>> GetUser(int id)
        {
            // Incluir os logs de progresso na consulta
            var user = await _context.Users
                .Include(u => u.ProgressLogs)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == id);

            if (user == null)
            {
                return NotFound(new { message = $"Usuário com ID {id} não encontrado." });
            }

            return user;
        }

        // POST: api/users
        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            // Adiciona validações básicas
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
        }

        // PUT: api/users/5
        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser(int id, User user)
        {
            if (id != user.Id)
            {
                return BadRequest("O ID na URL deve ser o mesmo do objeto no corpo da requisição.");
            }

            _context.Entry(user).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!_context.Users.Any(e => e.Id == id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // DELETE: api/users/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null)
            {
                return NotFound();
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return NoContent();
        }


        // --- INÍCIO DA ATUALIZAÇÃO ---
        // POST: api/users/5/progresslogs
        [HttpPost("{userId}/progresslogs")]
        public async Task<ActionResult<ProgressLog>> PostProgressLog(int userId, [FromBody] CreateProgressLogDto progressLogDto)
        {
            // Verifica se o modelo do DTO é válido (ex: campos obrigatórios)
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return NotFound(new { message = $"Usuário com ID {userId} não encontrado." });
            }

            // Cria um novo objeto ProgressLog a partir do DTO
            var progressLog = new ProgressLog
            {
                DaysWithoutGambling = progressLogDto.DaysWithoutGambling,
                Achievement = progressLogDto.Achievement,
                UserId = userId
            };

            _context.ProgressLogs.Add(progressLog);
            await _context.SaveChangesAsync();

            // Retorna o objeto ProgressLog completo que foi criado no banco de dados
            return CreatedAtAction(nameof(GetUser), new { id = userId }, progressLog);
        }
        // --- FIM DA ATUALIZAÇÃO ---
    }
}