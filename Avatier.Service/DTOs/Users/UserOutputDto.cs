using Avatier.Service.DTOs.Common;

namespace Avatier.Service.DTOs.Users
{
    public record UserOutputDto:BaseOutputDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }
}
