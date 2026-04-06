namespace Avatier.Service.DTOs.Ldap
{
    public record AuthenticateInputDto
    {
        public string? Uid { get; init; }
        public string? Password { get; init; }
    }
}
