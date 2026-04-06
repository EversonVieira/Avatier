namespace Avatier.Service.DTOs.Ldap
{
    public record LdapUserOutputDto
    {
        public string Dn { get; init; } = string.Empty;
        public string Uid { get; init; } = string.Empty;
        public string CommonName { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string PhoneNumber { get; init; } = string.Empty;
    }
}
