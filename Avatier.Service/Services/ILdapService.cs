using Avatier.Service.DTOs.Ldap;
using Avatier.Service.Wrappers;

namespace Avatier.Service.Services
{
    public interface ILdapService
    {
        Response<bool> TestConnection();
        Response<List<Dictionary<string, string>>> SearchEntries(string filter, string[]? attributes = null);

        Response<List<LdapUserOutputDto>> ListUsers();
        Response<LdapUserOutputDto> GetUser(string uid);
        Response UpdateUserAttributes(string uid, UpdateLdapUserInputDto input);
        Response ChangePassword(string uid, ChangePasswordInputDto input);

        Response<List<LdapGroupOutputDto>> ListGroups();
        Response<LdapGroupOutputDto> GetGroup(string cn);
        Response AddUserToGroup(GroupMembershipInputDto input);
        Response RemoveUserFromGroup(GroupMembershipInputDto input);

        Response<bool> Authenticate(AuthenticateInputDto input);
    }
}
