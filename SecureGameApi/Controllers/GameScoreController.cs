using Microsoft.AspNetCore.Mvc;
using SecureGameApi.Models;

namespace SecureGameApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GameScoreController : ControllerBase
    {
        // Skorları geçici olarak saklamak için (ileride DB’ye taşırız)
        private static readonly List<GameScoreSubmissionDto> Scores = new();
        [HttpPost("submit")]
        public IActionResult SubmitScore([FromBody] GameScoreSubmissionDto data)
        {
            // Token kontrolü
            var tokenEntry = ActiveTokens.FirstOrDefault(t => t.Token == data.Token);

            if (tokenEntry == null)

                return BadRequest("Geçersiz token.");

            if (tokenEntry.Used)
                return BadRequest("Token zaten kullanıldı.");

            if (tokenEntry.PlayerId != data.PlayerId)
                return BadRequest("Token bu kullanıcıya ait değil.");

            if ((DateTime.UtcNow - tokenEntry.CreatedAt).TotalMinutes > 10)
                return BadRequest("Token süresi dolmuş.");

            tokenEntry.Used = true; // Token kullanıldı olarak işaretle

            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            if (data.InputLog == null || data.InputLog.Count < 1 || data.InputLog.Count > 10000)
                return BadRequest("Şüpheli input log boyutu.");

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

            // Fixed: Changed && to || for proper validation
            if (data.TrophyCollected == true && (data.EnemySpawned < 10 || data.PlatformSpawned < 80 || data.CoinSpawned < 100 || data.JumpCount < 40))
                return BadRequest("Şüpheli wingame verisi - oyun tamamlanmamış görünüyor");

            if (data.DurationSeconds * 3 < data.PlatformSpawned)
                return BadRequest("Şüpheli platform spawn sıklığı");

            if (data.DurationSeconds * 5 < data.CoinSpawned)
                return BadRequest("Şüpheli coin spawn sıklığı");

            if (data.DurationSeconds / 2 < data.EnemySpawned)
                return BadRequest("Şüpheli düşman spawn sıklığı");

            if (data.DurationSeconds < 3 || data.DurationSeconds > 600)
                return BadRequest("Şüpheli süre.");

            // Accept the score if all validations pass
            Scores.Add(data);

            return Ok(new
            {
                Message = "Skor başarıyla kaydedildi.",
            });
        }

        [HttpGet("all")]
        public IActionResult GetScores()
        {
            return Ok(Scores);
        }

        private static readonly List<GameSessionToken> ActiveTokens = new();

        [HttpPost("start")]
        public IActionResult StartGame([FromBody] StartGameRequestDto req)
        {
            if (string.IsNullOrEmpty(req.PlayerId))
                return BadRequest("PlayerId zorunlu.");

            var token = Guid.NewGuid().ToString();
            ActiveTokens.Add(new GameSessionToken
            {
                Token = token,
                PlayerId = req.PlayerId,
                CreatedAt = DateTime.UtcNow,
                Used = false
            });
            return Ok(new { token });
        }

    }
}
