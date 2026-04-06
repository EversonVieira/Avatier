using System;
using System.Collections.Generic;
using System.Text;

namespace Avatier.Service.DTOs.Users
{
    public record CreateUserInputDto
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
        public string? Password { get; set; }
        public string? ConfirmPassword { get; set; }
    }
}
