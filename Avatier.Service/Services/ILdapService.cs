using Avatier.Service.Wrappers;

namespace Avatier.Service.Services
{
    public interface ILdapService
    {
        Response<bool> TestConnection();
        Response<List<Dictionary<string, string>>> SearchEntries(string filter, string[]? attributes = null);
    }
}
