namespace Avatier.Service.DTOs.Ldap
{
    public record ChangePasswordInputDto
    {
        public string? CurrentPassword { get; init; }
        public string? NewPassword { get; init; }
    }
}
