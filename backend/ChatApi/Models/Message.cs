namespace ChatApi.Models
{
    public class Message
    {
        public int Id { get; set; }
        public string Text { get; set; } = null!;
        public string Sentiment { get; set; } = "neutral";
        public double Score { get; set; } = 0.0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int UserId { get; set; }
        public User? User { get; set; }
    }
}
