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
        public ActionResult<Response<bool>> TestConnection()
        {
            var response = _ldapService.TestConnection();
            if (response.IsInFailure)
                return StatusCode(503, response);

            return Ok(response);
        }

        [HttpGet("search")]
        public ActionResult<Response<List<Dictionary<string, string>>>> Search(
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
