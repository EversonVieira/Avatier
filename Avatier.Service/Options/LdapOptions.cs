namespace Avatier.Service.Options
{
    public class LdapOptions
    {
        public const string SectionName = "Ldap";

        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 389;
        public string BaseDn { get; set; } = "dc=avatier,dc=local";
        public string AdminDn { get; set; } = "cn=admin,dc=avatier,dc=local";
        public string AdminPassword { get; set; } = string.Empty;
    }
}
