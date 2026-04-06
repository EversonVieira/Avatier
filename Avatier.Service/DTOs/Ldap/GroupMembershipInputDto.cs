namespace Avatier.Service.DTOs.Ldap
{
    public record GroupMembershipInputDto
    {
        public string? Uid { get; init; }
        public string? GroupCn { get; init; }
    }
}
