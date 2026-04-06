namespace Avatier.Service.DTOs.Ldap
{
    public record LdapGroupOutputDto
    {
        public string Dn { get; init; } = string.Empty;
        public string Cn { get; init; } = string.Empty;
        public List<string> Members { get; init; } = [];
    }
}
