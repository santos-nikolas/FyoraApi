using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;

namespace FyoraApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _cfg;
        private readonly bool _isDev;

        public ChatController(IHttpClientFactory httpFactory, IConfiguration cfg, IWebHostEnvironment env)
        {
            _httpFactory = httpFactory;
            _cfg = cfg;
            _isDev = env.IsDevelopment();
        }

        [HttpPost("ask")]
        public async Task<ActionResult<string>> Ask([FromBody] ChatPrompt prompt)
        {
            if (string.IsNullOrWhiteSpace(prompt.Message))
                return BadRequest("A mensagem não pode ser vazia.");
            if (prompt.Message.Length > 2000)
                return BadRequest("Mensagem muito longa.");

            // instrução de sistema mínima para manter o tom
            var system =
                "Você é um assistente de apoio empático do app Fyora. " +
                "Responda de forma breve, acolhedora e prática a pessoas que lutam contra o jogo problemático. " +
                "Evite conselhos médicos.";

            try
            {
                var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? _cfg["Gemini:ApiKey"];
                if (string.IsNullOrWhiteSpace(apiKey))
                    return StatusCode(500, "Gemini API key não configurada. Defina GEMINI_API_KEY ou Gemini:ApiKey.");

                var model = _cfg["Gemini:Model"] ?? "gemini-2.0-flash";
                var maxTokens = _cfg.GetValue("Gemini:MaxTokens", 512);
                var temperature = _cfg.GetValue("Gemini:Temperature", 0.7);

                var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";

                // payload compatível v1beta; sem safety em Dev, safety moderada em Prod
                object payload =
                    _isDev
                    ? new
                    {
                        contents = new[]
                        {
                            new {
                                role = "user",
                                parts = new object[]
                                {
                                    new { text = $"{system}\n\nUsuário: {prompt.Message}" }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            temperature,
                            maxOutputTokens = maxTokens
                        }
                    }
                    : new
                    {
                        contents = new[]
                        {
                            new {
                                role = "user",
                                parts = new object[]
                                {
                                    new { text = $"{system}\n\nUsuário: {prompt.Message}" }
                                }
                            }
                        },
                        generationConfig = new
                        {
                            temperature,
                            maxOutputTokens = maxTokens
                        },
                        // Somente categorias suportadas no v1beta
                        safetySettings = new[]
                        {
                            new { category = "HARM_CATEGORY_HARASSMENT",        threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                            new { category = "HARM_CATEGORY_HATE_SPEECH",       threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                            new { category = "HARM_CATEGORY_SEXUALLY_EXPLICIT", threshold = "BLOCK_MEDIUM_AND_ABOVE" },
                            new { category = "HARM_CATEGORY_DANGEROUS_CONTENT", threshold = "BLOCK_MEDIUM_AND_ABOVE" }
                        }
                    };

                var json = JsonSerializer.Serialize(payload);
                var http = _httpFactory.CreateClient();
                using var req = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                using var res = await http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
                var body = await res.Content.ReadAsStringAsync();

                if (!res.IsSuccessStatusCode)
                {
                    // Em dev, devolve o corpo; em prod, mensagens genéricas
                    if (_isDev) return StatusCode((int)res.StatusCode, $"Gemini error {res.StatusCode}: {body}");
                    return StatusCode((int)res.StatusCode, "Erro externo ao gerar resposta.");
                }

                string extracted = TryExtractText(body) ?? "(sem texto)";
                return Ok(extracted.Trim());
            }
            catch (Exception ex)
            {
                Console.WriteLine("Erro ao chamar Gemini REST:");
                Console.WriteLine(ex.ToString());

                return StatusCode(500, _isDev
                    ? $"Erro (dev): {ex.GetType().Name}: {ex.Message}"
                    : "Erro ao processar sua solicitação. Tente novamente mais tarde.");
            }
        }

        [HttpGet("ping")]
        public async Task<ActionResult<string>> Ping()
        {
            var apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? _cfg["Gemini:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return StatusCode(500, "Sem API key.");

            var model = _cfg["Gemini:Model"] ?? "gemini-2.0-flash";
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={apiKey}";
            var payload = new
            {
                contents = new[]
                {
                    new { role = "user", parts = new object[] { new { text = "ping" } } }
                }
            };

            try
            {
                var http = _httpFactory.CreateClient();
                var json = JsonSerializer.Serialize(payload);
                var res = await http.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
                var body = await res.Content.ReadAsStringAsync();
                if (!res.IsSuccessStatusCode)
                    return StatusCode((int)res.StatusCode, $"Ping falhou: {res.StatusCode}: {body}");

                var txt = TryExtractText(body) ?? "(sem texto)";
                return Ok($"ok; resp={txt}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Ping exception: {ex.GetType().Name}: {ex.Message}");
            }
        }

        private static string? TryExtractText(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // v1beta: candidates[0].content.parts[0].text
                if (root.TryGetProperty("candidates", out var cands)
                    && cands.ValueKind == JsonValueKind.Array
                    && cands.GetArrayLength() > 0)
                {
                    var c0 = cands[0];

                    if (c0.TryGetProperty("content", out var content)
                        && content.TryGetProperty("parts", out var parts)
                        && parts.ValueKind == JsonValueKind.Array
                        && parts.GetArrayLength() > 0)
                    {
                        var p0 = parts[0];
                        if (p0.TryGetProperty("text", out var textEl))
                            return textEl.GetString();
                    }

                    // fallback: alguns retornos têm "text" direto
                    if (c0.TryGetProperty("text", out var textEl2))
                        return textEl2.GetString();
                }
            }
            catch { /* ignore */ }
            return null;
        }
    }

    public class ChatPrompt
    {
        [Required]
        public string Message { get; set; } = string.Empty;
    }
}
