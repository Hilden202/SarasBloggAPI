namespace SarasBloggAPI.DTOs
{
    public sealed class AccountDeleteDto
    {
        public string? Password { get; set; }   // null/empty om kontot saknar lokalt lösen
    }
}
