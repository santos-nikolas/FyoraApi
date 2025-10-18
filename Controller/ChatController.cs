using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace FyoraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IConfiguration _cfg;
        private readonly IHttpClientFactory _httpFactory;
        private readonly bool _isDev;

        public ChatController(IConfiguration cfg, IHttpClientFactory httpFactory, IWebHostEnvironment env)
        {
            _cfg = cfg;
            _httpFactory = httpFactory;
            _isDev = env.IsDevelopment();
        }

        // DTO simples para o corpo do POST
        public record ChatPrompt(string Message);

        [HttpGet("ping")]
        public async Task<ActionResult<string>> Ping()
        {
            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                        ?? _cfg["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return StatusCode(500, "Gemini API key não configurada. Defina GEMINI_API_KEY ou Gemini:ApiKey.");

            var model = _cfg["Gemini:Model"] ?? "gemini-2.0-flash";
            var url = $"v1beta/models/{model}:generateContent?key={apiKey}";

            var client = _httpFactory.CreateClient("gemini");

            var payload = new
            {
                contents = new object[]
                {
                    new { role = "user", parts = new object[] { new { text = "Pong!" } } }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            var res = await client.SendAsync(req);
            var body = await res.Content.ReadAsStringAsync();
            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, $"Gemini ping falhou: {body}");

            var text = TryExtractText(body) ?? "(sem texto)";
            return Ok($"ok; resp={text}");
        }

        [HttpPost("ask")]
        public async Task<ActionResult<string>> Ask([FromBody] ChatPrompt prompt)
        {
            if (prompt is null || string.IsNullOrWhiteSpace(prompt.Message))
                return BadRequest("A mensagem não pode ser vazia.");
            if (prompt.Message.Length > 2000)
                return BadRequest("Mensagem muito longa.");

            var system = @"
PAPEL
Você é a assistente empática do app Fyora (PT-BR), focada em apoio breve, prático e não julgador para pessoas com jogo problemático.

ESTILO
- Máx. ~80–90 palavras, 2–4 frases.
- Tom caloroso e respeitoso; use “você”; 1 emoji no máximo.
- Reconheça emoções e celebre pequenos avanços.

O QUE FAZER (alinhar às features do Fyora)
- Motivar com a metáfora da Fênix (“renascer”, resiliência) e sugerir ações simples: 
  • Guardiã: evoluir a Fênix, resgatar recompensas (“Penas de Fênix”), cumprir desafios do Oásis.
  • Diário/Consciência: registrar conquistas e gatilhos; checar “Radar de Jogo” e “Termômetro de Bem-Estar”.
  • Limites & Foco: revisar “Âncora de Controle”; ativar alertas/pausas (“Guardião do Tempo e Foco”).
  • Educação & Finanças: visitar “Descomplica Jogo”; usar “Bússola Financeira” (orçamento, metas, economia visível).
  • Rede de Apoio: consultar “Meu Porto Seguro” (contatos/recursos) e pedir apoio a alguém de confiança.
- Feche com 1 pergunta breve de avanço (“Qual passo você topa hoje?”).

O QUE EVITAR
- Não dê conselhos médicos, legais ou financeiros personalizados; não explique probabilidades/estratégias de aposta; nunca incentive apostar; não diagnostique.

SEGURANÇA
- Se notar sinais de crise (autoagressão/ideação suicida/violência), responda com acolhimento e oriente ajuda imediata e apropriada.
- Brasil: mencionar de forma sensível que o CVV (188) oferece apoio 24h e canais online. Não dramatizar; priorizar o cuidado e a segurança.

SAÍDA
- Resposta curta, aplicável agora, com 2–3 ações específicas ligadas aos módulos do Fyora + 1 pergunta de compromisso. 1 emoji no máximo.
";


            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                        ?? _cfg["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return StatusCode(500, "Gemini API key não configurada. Defina GEMINI_API_KEY ou Gemini:ApiKey.");

            var model = _cfg["Gemini:Model"] ?? "gemini-2.0-flash";
            var maxTokens = _cfg.GetValue("Gemini:MaxTokens", 512);
            var temperature = _cfg.GetValue("Gemini:Temperature", 0.7);

            var url = $"v1beta/models/{model}:generateContent?key={apiKey}";
            var client = _httpFactory.CreateClient("gemini");

            // v1beta: enviamos o "system" como primeira mensagem do usuário para orientar o tom
            var payload = new
            {
                contents = new object[]
                {
                    new { role = "user", parts = new object[] { new { text = system } } },
                    new { role = "user", parts = new object[] { new { text = prompt.Message } } }
                },
                generationConfig = new
                {
                    maxOutputTokens = maxTokens,
                    temperature = temperature
                }
            };

            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };

            try
            {
                var res = await client.SendAsync(req);
                var body = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                    return StatusCode((int)res.StatusCode, $"Gemini error {res.StatusCode}: {body}");

                var text = TryExtractText(body);
                if (string.IsNullOrWhiteSpace(text))
                    return Ok("Não consegui gerar uma resposta no momento. Tente novamente.");

                return Ok(text);
            }
            catch (Exception ex)
            {
                var msg = _isDev ? $"Erro ao processar: {ex}" : "Erro ao processar sua solicitação. Tente novamente mais tarde.";
                return StatusCode(500, msg);
            }
        }

        // Extrai candidates[0].content.parts[0].text de forma resiliente
        private static string? TryExtractText(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (!root.TryGetProperty("candidates", out var candidates) ||
                    candidates.ValueKind != JsonValueKind.Array ||
                    candidates.GetArrayLength() == 0)
                    return null;

                var content = candidates[0].GetProperty("content");
                if (content.ValueKind == JsonValueKind.Object &&
                    content.TryGetProperty("parts", out var parts) &&
                    parts.ValueKind == JsonValueKind.Array &&
                    parts.GetArrayLength() > 0)
                {
                    var part = parts[0];
                    if (part.TryGetProperty("text", out var txt))
                        return txt.GetString();
                }

                if (candidates[0].TryGetProperty("finishReason", out var fr))
                    return $"(finishReason: {fr.GetString()})";

                return null;
            }
            catch
            {
                return null;
            }
        }
    }
}
