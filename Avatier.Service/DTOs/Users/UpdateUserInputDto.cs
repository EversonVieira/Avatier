namespace Avatier.Service.DTOs.Users
{
    public record UpdateUserInputDto : CreateUserInputDto
    {
        public Guid Id { get; set; }
        public string? OldPassword { get; set; }
    }
}
