namespace Avatier.Service.DTOs.Common
{
    public record BaseOutputDto
    {
        public Guid Id { get; init; }
        public DateTimeOffset RegisteredAt { get; init; }
        public bool IsActive { get; init; }
    }
}
