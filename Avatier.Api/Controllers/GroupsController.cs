using Avatier.Service.DTOs.Ldap;
using Avatier.Service.Services;
using Avatier.Service.Wrappers;
using Microsoft.AspNetCore.Mvc;

namespace Avatier.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class GroupsController : ControllerBase
    {
        private readonly ILdapService _ldapService;

        public GroupsController(ILdapService ldapService)
        {
            _ldapService = ldapService;
        }

        [HttpGet]
        [ProducesResponseType<Response<List<LdapGroupOutputDto>>>(StatusCodes.Status200OK)]
        public IActionResult List()
        {
            var response = _ldapService.ListGroups();
            return Ok(response);
        }

        [HttpGet("{cn}")]
        [ProducesResponseType<Response<LdapGroupOutputDto>>(StatusCodes.Status200OK)]
        [ProducesResponseType<Response<LdapGroupOutputDto>>(StatusCodes.Status404NotFound)]
        public IActionResult Get(string cn)
        {
            var response = _ldapService.GetGroup(cn);
            if (response.IsInFailure)
                return NotFound(response);

            return Ok(response);
        }

        [HttpPost("add-member")]
        [ProducesResponseType<Response>(StatusCodes.Status200OK)]
        public IActionResult AddMember([FromBody] GroupMembershipInputDto input)
        {
            var response = _ldapService.AddUserToGroup(input);
            if (response.IsInFailure)
                return BadRequest(response);

            return Ok(response);
        }

        [HttpPost("remove-member")]
        [ProducesResponseType<Response>(StatusCodes.Status200OK)]
        public IActionResult RemoveMember([FromBody] GroupMembershipInputDto input)
        {
            var response = _ldapService.RemoveUserFromGroup(input);
            if (response.IsInFailure)
                return BadRequest(response);

            return Ok(response);
        }
    }
}
