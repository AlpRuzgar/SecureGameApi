namespace SecureGameApi.Models
{
    public class GameSessionToken
    {
        public string Token { get; set; }
        public string PlayerId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Used { get; set; }
    }
}
