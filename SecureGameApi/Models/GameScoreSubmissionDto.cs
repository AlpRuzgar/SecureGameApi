namespace SecureGameApi.Models
{
    public class GameScoreSubmissionDto
    {
        public string PlayerId { get; set; }      // Kullanıcıyı tanımak için
        public int Score { get; set; }            // Kullanıcının skoru
        public long StartTime { get; set; }
        public long EndTime { get; set; }
        public int DurationSeconds { get; set; }  // Oyun süresi (isteğe bağlı)
        public int Hearts { get; set; }
        public long HeightReached { get; set; }

        public int PlatformSpawned { get; set; }
        public bool TrophySpawned { get; set; }   // Gerçekten trophy'e ulaştı mı
        public bool TrophyCollected { get; set; }
        public int CoinSpawned {  get; set; }
        public int CoinCollected {  get; set; }
        public int EnemySpawned { get; set; }
        public int DamageTaken {  get; set; }
        public int JumpCount { get; set; }
    }
}