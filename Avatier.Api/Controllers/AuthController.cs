using Avatier.Service.DTOs.Ldap;
using Avatier.Service.Services;
using Avatier.Service.Wrappers;
using Microsoft.AspNetCore.Mvc;

namespace Avatier.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ILdapService _ldapService;

        public AuthController(ILdapService ldapService)
        {
            _ldapService = ldapService;
        }

        [HttpPost("login")]
        [ProducesResponseType<Response<bool>>(StatusCodes.Status200OK)]
        public IActionResult Login([FromBody] AuthenticateInputDto input)
        {
            var response = _ldapService.Authenticate(input);
            if (response.IsInFailure)
                return Unauthorized(response);

            return Ok(response);
        }
    }
}
