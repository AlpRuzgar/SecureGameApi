using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;
using SecureGameApi.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.Json;

namespace SecureGameApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameScoreController : ControllerBase
    {
        // Token ile secret saklama
        private static readonly Dictionary<string, byte[]> SessionSecrets = new();
        private static readonly List<GameSessionToken> ActiveTokens = new();
        private static readonly List<GameScoreSubmissionDto> Scores = new(); 
        private readonly IConfiguration _config;
        public GameScoreController(IConfiguration config)
        {
            _config = config;
        }

        public class StartDto
        {
            public string PlayerId { get; set; }
        }

        // Oyun başlatma: hem token hem secret üretir
        [HttpPost("start")]
        public IActionResult StartSession([FromBody] StartDto dto)
        {
            if (string.IsNullOrEmpty(dto.PlayerId))
                return BadRequest("PlayerId zorunlu.");

            var token = Guid.NewGuid().ToString("N");
            var secret = new byte[32];
            RandomNumberGenerator.Fill(secret);

            SessionSecrets[token] = secret;
            ActiveTokens.Add(new GameSessionToken
            {
                Token = token,
                PlayerId = dto.PlayerId,
                CreatedAt = DateTime.UtcNow,
                Used = false
            });

            return Ok(new
            {
                token,
                secret = Convert.ToBase64String(secret)
            });
        }

        // Skor gönderme: asenkron I/O ile body okuma ve HMAC doğrulama
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitScoreAsync()
        {
            // 1) X-Signature header
            if (!Request.Headers.TryGetValue("X-Signature", out var sigValues))
                return BadRequest("Eksik imza.");
            var sigHeader = sigValues.ToString();
            Console.WriteLine($"Server - Gelen X-Signature: {sigHeader}");

            // 2) Asenkron body okuma
            HttpContext.Request.EnableBuffering();
            string body;
            using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
            {
                body = await reader.ReadToEndAsync();
                Request.Body.Position = 0;
            }
            Console.WriteLine($"Server - Gelen Body String: {body}");

            // 3) Deserialize
            var data = JsonSerializer.Deserialize<GameScoreSubmissionDto>(
                body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
            );
            if (data == null)
                return BadRequest("Geçersiz payload.");

            // 4) Secret retrieval
            if (!SessionSecrets.TryGetValue(data.Token, out var secret))
                return BadRequest("Geçersiz token.");

            // 5) HMAC hesapla ve sabit zamanlı karşılaştır
            using var hmac = new HMACSHA256(secret);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(body));
            byte[] receivedHash;
            try
            {
                receivedHash = Convert.FromBase64String(sigHeader);
            }
            catch
            {
                return BadRequest("Geçersiz imza formatı.");
            }
            Console.WriteLine($"Server - Hesaplanan İmza: {Convert.ToBase64String(computedHash)}");
            if (!CryptographicOperations.FixedTimeEquals(computedHash, receivedHash))
            {
                Console.WriteLine("Server - İmza Doğrulama Başarısız.");
                return BadRequest("İmza doğrulanamadı.");
            }

            // 6) Token doğrulama
            var tokenEntry = ActiveTokens.FirstOrDefault(t => t.Token == data.Token);
            if (tokenEntry == null)
                return BadRequest("Geçersiz token.");
            if (tokenEntry.Used)
                return BadRequest("Token zaten kullanıldı.");
            if (tokenEntry.PlayerId != data.PlayerId)
                return BadRequest("Token bu kullanıcıya ait değil.");
            if ((DateTime.UtcNow - tokenEntry.CreatedAt).TotalMinutes > 10)
                return BadRequest("Token süresi dolmuş.");
            tokenEntry.Used = true;

            // 7) Payload validasyonları
            if (!ModelState.IsValid)
                return BadRequest(ModelState);
            if (data.JumpPower != 810) return BadRequest("Şüpheli jump power boyutu.");
            if (data.CoinCollected > data.CoinSpawned)
                return BadRequest("Şüpheli toplanan coin sayısı");
            if (data.Score > data.CoinCollected * 10)
                return BadRequest("Şüpheli skor/toplanan coin verisi");
            if (data.Hearts < 0 || data.Hearts > 6)
                return BadRequest("Şüpheli can sayısı");
            if (data.DamageTaken > 6 || data.DamageTaken < 0)
                return BadRequest("Şüpheli hasar alma sayısı");
            if (data.DamageTaken + data.Hearts > 6)
                return BadRequest("Şüpheli can + hasar verisi");
            if (data.TrophyCollected && (data.EnemySpawned < 10 || data.PlatformSpawned < 80 || data.CoinSpawned < 100 || data.JumpCount < 40))
                return BadRequest("Şüpheli wingame verisi - oyun tamamlanmamış görünüyor");
            if (data.DurationSeconds * 3 < data.PlatformSpawned)
                return BadRequest("Şüpheli platform spawn sıklığı");
            if (data.DurationSeconds * 5 < data.CoinSpawned)
                return BadRequest("Şüpheli coin spawn sıklığı");
            if (data.DurationSeconds / 2 < data.EnemySpawned)
                return BadRequest("Şüpheli düşman spawn sıklığı");
            if (data.DurationSeconds < 3 || data.DurationSeconds > 600)
                return BadRequest("Şüpheli süre.");

            // Başarılı ise kaydet
            Scores.Add(data);
            return Ok(new { Message = "Skor başarıyla kaydedildi." });
        }

        [HttpGet("all")]
        public IActionResult GetScores()
        {
            return Ok(Scores);
        }
    }
    public class ReCaptchaResponse
    {
        public bool Success { get; set; }
        public decimal Score { get; set; }
        public string Action { get; set; }
        // … diğer alanlar …
    }
}
