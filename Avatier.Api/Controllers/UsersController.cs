using Avatier.Service.DTOs.Ldap;
using Avatier.Service.Services;
using Avatier.Service.Wrappers;
using Microsoft.AspNetCore.Mvc;

namespace Avatier.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly ILdapService _ldapService;

        public UsersController(ILdapService ldapService)
        {
            _ldapService = ldapService;
        }

        [HttpGet]
        [ProducesResponseType<Response<List<LdapUserOutputDto>>>(StatusCodes.Status200OK)]
        public IActionResult List()
        {
            var response = _ldapService.ListUsers();
            return Ok(response);
        }

        [HttpGet("{uid}")]
        [ProducesResponseType<Response<LdapUserOutputDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<Response<LdapUserOutputDto>>(StatusCodes.Status404NotFound)]
        public IActionResult Get(string uid)
        {
            var response = _ldapService.GetUser(uid);
            if (response.IsInFailure)
                return NotFound(response);

            return Ok(response);
        }

        [HttpPatch("{uid}")]
        [ProducesResponseType<Response>(StatusCodes.Status200OK)]
        public IActionResult Update(string uid, [FromBody] UpdateLdapUserInputDto input)
        {
            var response = _ldapService.UpdateUserAttributes(uid, input);
            if (response.IsInFailure)
                return BadRequest(response);

            return Ok(response);
        }

        [HttpPost("{uid}/change-password")]
        [ProducesResponseType<Response>(StatusCodes.Status200OK)]
        public IActionResult ChangePassword(string uid, [FromBody] ChangePasswordInputDto input)
        {
            var response = _ldapService.ChangePassword(uid, input);
            if (response.IsInFailure)
                return BadRequest(response);

            return Ok(response);
        }
    }
}
