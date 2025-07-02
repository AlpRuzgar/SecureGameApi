namespace SecureGameApi.Models
{
    public class GameScoreSubmissionDto
    {
        public string Token { get; set; }
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
        public List<InputEventDto> InputLog { get; set; }
    }
    
    public class StartGameRequestDto
    {
        public string PlayerId { get; set; }
    }
    
    public class InputEventDto
    {
        public string Type { get; set; }    // "keydown", "keyup", "pointerdown", "pointerup"
        public string Key { get; set; }     // sadece klavye için (event.code)
        public int? X { get; set; }         // pointer için
        public int? Y { get; set; }
        public int T { get; set; }          // oyun başlangıcına göre milisaniye
    }
}
