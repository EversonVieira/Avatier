using Avatier.Service.Enums;
using Avatier.Service.Options;
using Avatier.Service.Wrappers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.DirectoryServices.Protocols;
using System.Net;

namespace Avatier.Service.Services
{
    public class LdapService : BaseLayer, ILdapService
    {
        private readonly LdapOptions _ldapOptions;

        public LdapService(ILogger<LdapService> logger, IOptions<LoggingOptions> loggingOptions, IOptions<LdapOptions> ldapOptions)
            : base(logger, loggingOptions)
        {
            _ldapOptions = ldapOptions.Value;
        }

        public Response<bool> TestConnection()
        {
            var response = new Response<bool>();

            try
            {
                using var connection = CreateConnection();
                connection.Bind();
                response.Data = true;
                response.Messages.Add(new Message
                {
                    Text = "Successfully connected to the LDAP server.",
                    Type = MessageTypeEnum.Success
                });

                LogInformation("LDAP connection test succeeded against {Host}:{Port}",
                    [_ldapOptions.Host, _ldapOptions.Port], LogSensitivityLevelEnum.Public);
            }
            catch (LdapException ex)
            {
                response.Data = false;
                response.Messages.Add(new Message
                {
                    Text = $"Failed to connect to the LDAP server: {ex.Message}",
                    Type = MessageTypeEnum.Error
                });

                LogError("LDAP connection test failed: {Error}", [ex.Message], LogSensitivityLevelEnum.Public);
            }

            return response;
        }

        public Response<List<Dictionary<string, string>>> SearchEntries(string filter, string[]? attributes = null)
        {
            var response = new Response<List<Dictionary<string, string>>>();
            var results = new List<Dictionary<string, string>>();

            try
            {
                using var connection = CreateConnection();
                connection.Bind();

                var searchRequest = new SearchRequest(
                    _ldapOptions.BaseDn,
                    filter,
                    SearchScope.Subtree,
                    attributes);

                var searchResponse = (SearchResponse)connection.SendRequest(searchRequest);

                foreach (SearchResultEntry entry in searchResponse.Entries)
                {
                    var entryDict = new Dictionary<string, string>
                    {
                        ["dn"] = entry.DistinguishedName
                    };

                    foreach (DirectoryAttribute attr in entry.Attributes.Values)
                    {
                        entryDict[attr.Name] = attr[0]?.ToString() ?? string.Empty;
                    }

                    results.Add(entryDict);
                }

                response.Data = results;
                response.Messages.Add(new Message
                {
                    Text = $"Found {results.Count} entries.",
                    Type = MessageTypeEnum.Success
                });
            }
            catch (LdapException ex)
            {
                response.Messages.Add(new Message
                {
                    Text = $"LDAP search failed: {ex.Message}",
                    Type = MessageTypeEnum.Error
                });

                LogError("LDAP search failed: {Error}", [ex.Message], LogSensitivityLevelEnum.Public);
            }

            return response;
        }

        private LdapConnection CreateConnection()
        {
            var identifier = new LdapDirectoryIdentifier(_ldapOptions.Host, _ldapOptions.Port);
            var credential = new NetworkCredential(_ldapOptions.AdminDn, _ldapOptions.AdminPassword);
            var connection = new LdapConnection(identifier, credential)
            {
                AuthType = AuthType.Basic
            };

            connection.SessionOptions.ProtocolVersion = 3;

            return connection;
        }
    }
}
