using Avatier.Service.Services;
using Avatier.Service.Wrappers;
using Microsoft.AspNetCore.Mvc;

namespace Avatier.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LdapController : ControllerBase
    {
        private readonly ILdapService _ldapService;

        public LdapController(ILdapService ldapService)
        {
            _ldapService = ldapService;
        }

        [HttpGet("test-connection")]
        [ProducesResponseType<Response<bool>>(StatusCodes.Status200OK)]
        [ProducesResponseType<Response<bool>>(StatusCodes.Status503ServiceUnavailable)]
        public IActionResult TestConnection()
        {
            var response = _ldapService.TestConnection();
            if (response.IsInFailure)
                return StatusCode(StatusCodes.Status503ServiceUnavailable, response);

            return Ok(response);
        }

        [HttpGet("search")]
        [ProducesResponseType<Response<List<Dictionary<string, string>>>>(StatusCodes.Status200OK)]
        public IActionResult Search(
            [FromQuery] string filter = "(objectClass=*)",
            [FromQuery] string? attributes = null)
        {
            var attrs = attributes?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var response = _ldapService.SearchEntries(filter, attrs);
            if (response.IsInFailure)
                return BadRequest(response);

            return Ok(response);
        }
    }
}
