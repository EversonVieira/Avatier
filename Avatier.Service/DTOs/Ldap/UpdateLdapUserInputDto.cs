namespace Avatier.Service.DTOs.Ldap
{
    public record UpdateLdapUserInputDto
    {
        public string? FirstName { get; init; }
        public string? Email { get; init; }
        public string? PhoneNumber { get; init; }
    }
}
