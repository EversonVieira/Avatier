namespace Avatier.Service.DTOs.Common
{
    public record LoggedUserContext
    {
        public Guid Id { get; init; }
        public string? FirstName { get; init; }
        public string? LastName { get; init; }
        public string? Email { get; init; }
        public string? PhoneNumber { get; init; }
    }
}
