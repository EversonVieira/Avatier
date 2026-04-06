using Avatier.Service.DTOs.Ldap;
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

        private static readonly string[] UserAttributes = ["uid", "cn", "sn", "givenName", "mail", "telephoneNumber"];
        private static readonly string[] GroupAttributes = ["cn", "member"];

        public LdapService(
            ILogger<LdapService> logger,
            IOptions<LogFeederOptions> loggingOptions,
            IOptions<LdapOptions> ldapOptions)
            : base(logger, loggingOptions)
        {
            _ldapOptions = ldapOptions.Value;
        }

        public Response<bool> TestConnection()
        {
            var response = new Response<bool>();

            try
            {
                using var connection = CreateAdminConnection();
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

        public Response<List<LdapUserOutputDto>> ListUsers()
        {
            var response = new Response<List<LdapUserOutputDto>>();

            using var connection = CreateAdminConnection();
            connection.Bind();

            var request = new SearchRequest(
                _ldapOptions.UsersDn,
                "(objectClass=inetOrgPerson)",
                SearchScope.OneLevel,
                UserAttributes);

            var searchResponse = (SearchResponse)connection.SendRequest(request);
            var users = new List<LdapUserOutputDto>();

            foreach (SearchResultEntry entry in searchResponse.Entries)
            {
                users.Add(MapToUserDto(entry));
            }

            response.Data = users;
            response.Messages.Add(new Message
            {
                Text = $"Found {users.Count} user(s).",
                Type = MessageTypeEnum.Success
            });

            LogInformation("Listed {Count} LDAP users", [users.Count], LogSensitivityLevelEnum.Public);
            return response;
        }

        public Response<LdapUserOutputDto> GetUser(string uid)
        {
            var response = new Response<LdapUserOutputDto>();

            if (string.IsNullOrWhiteSpace(uid))
            {
                response.Messages.Add(new Message
                {
                    Text = "User ID (uid) is required.",
                    Type = MessageTypeEnum.Validation
                });
                return response;
            }

            using var connection = CreateAdminConnection();
            connection.Bind();

            var entry = FindEntry(connection, _ldapOptions.UsersDn,
                $"(&(objectClass=inetOrgPerson)(uid={EscapeLdapFilter(uid)}))", UserAttributes);

            if (entry is null)
            {
                response.Messages.Add(new Message
                {
                    Text = $"User '{uid}' was not found.",
                    Type = MessageTypeEnum.Validation
                });
                LogWarning("LDAP user not found: {Uid}", [uid], LogSensitivityLevelEnum.Public);
                return response;
            }

            response.Data = MapToUserDto(entry);
            response.Messages.Add(new Message
            {
                Text = "User retrieved successfully.",
                Type = MessageTypeEnum.Success
            });
            return response;
        }

        public Response UpdateUserAttributes(string uid, UpdateLdapUserInputDto input)
        {
            var response = new Response();

            if (string.IsNullOrWhiteSpace(uid))
            {
                response.Messages.Add(new Message { Text = "User ID (uid) is required.", Type = MessageTypeEnum.Validation });
                return response;
            }

            var modifications = new List<DirectoryAttributeModification>();

            if (input.FirstName is not null)
                modifications.Add(CreateModification("givenName", input.FirstName));
            if (input.Email is not null)
                modifications.Add(CreateModification("mail", input.Email));
            if (input.PhoneNumber is not null)
                modifications.Add(CreateModification("telephoneNumber", input.PhoneNumber));

            if (modifications.Count == 0)
            {
                response.Messages.Add(new Message
                {
                    Text = "At least one attribute must be provided for update.",
                    Type = MessageTypeEnum.Validation
                });
                return response;
            }

            using var connection = CreateAdminConnection();
            connection.Bind();

            var userDn = FindUserDn(connection, uid);
            if (userDn is null)
            {
                response.Messages.Add(new Message { Text = $"User '{uid}' was not found.", Type = MessageTypeEnum.Validation });
                return response;
            }

            try
            {
                var modifyRequest = new ModifyRequest(userDn, [.. modifications]);
                connection.SendRequest(modifyRequest);
            }
            catch (DirectoryOperationException ex) when (ex.Response?.ResultCode == ResultCode.NoSuchObject)
            {
                response.Messages.Add(new Message { Text = $"User '{uid}' was not found.", Type = MessageTypeEnum.Validation });
                LogWarning("LDAP modify failed – user not found: {Uid}", [uid], LogSensitivityLevelEnum.Public);
                return response;
            }

            response.Messages.Add(new Message
            {
                Text = $"User '{uid}' attributes updated successfully.",
                Type = MessageTypeEnum.Success
            });
            LogInformation("Updated attributes for LDAP user {Uid}", [uid], LogSensitivityLevelEnum.Public);
            return response;
        }

        public Response ChangePassword(string uid, ChangePasswordInputDto input)
        {
            var response = new Response();

            if (string.IsNullOrWhiteSpace(uid))
            {
                response.Messages.Add(new Message { Text = "User ID (uid) is required.", Type = MessageTypeEnum.Validation });
                return response;
            }
            if (string.IsNullOrWhiteSpace(input.CurrentPassword))
            {
                response.Messages.Add(new Message { Text = "Current password is required.", Type = MessageTypeEnum.Validation });
                return response;
            }
            if (string.IsNullOrWhiteSpace(input.NewPassword))
            {
                response.Messages.Add(new Message { Text = "New password is required.", Type = MessageTypeEnum.Validation });
                return response;
            }

            using var adminConnection = CreateAdminConnection();
            adminConnection.Bind();

            var userDn = FindUserDn(adminConnection, uid);
            if (userDn is null)
            {
                response.Messages.Add(new Message { Text = $"User '{uid}' was not found.", Type = MessageTypeEnum.Validation });
                return response;
            }

            try
            {
                using var userConnection = CreateUserConnection(userDn, input.CurrentPassword);
                userConnection.Bind();
            }
            catch (LdapException ex) when (ex.ErrorCode == 49)
            {
                response.Messages.Add(new Message { Text = "Current password is incorrect.", Type = MessageTypeEnum.Validation });
                LogWarning("Password change failed – invalid current password for user {Uid}", [uid], LogSensitivityLevelEnum.PrivateProtected);
                return response;
            }

            var modification = CreateModification("userPassword", input.NewPassword);
            var modifyRequest = new ModifyRequest(userDn, modification);
            adminConnection.SendRequest(modifyRequest);

            response.Messages.Add(new Message
            {
                Text = $"Password changed successfully for user '{uid}'.",
                Type = MessageTypeEnum.Success
            });
            LogInformation("Password changed for LDAP user {Uid}", [uid], LogSensitivityLevelEnum.Public);
            return response;
        }

        // ── Groups ─────────────────────────────────────────────────────

        public Response<List<LdapGroupOutputDto>> ListGroups()
        {
            var response = new Response<List<LdapGroupOutputDto>>();

            using var connection = CreateAdminConnection();
            connection.Bind();

            var request = new SearchRequest(
                _ldapOptions.GroupsDn,
                "(objectClass=groupOfNames)",
                SearchScope.OneLevel,
                GroupAttributes);

            var searchResponse = (SearchResponse)connection.SendRequest(request);
            var groups = new List<LdapGroupOutputDto>();

            foreach (SearchResultEntry entry in searchResponse.Entries)
            {
                groups.Add(MapToGroupDto(entry));
            }

            response.Data = groups;
            response.Messages.Add(new Message
            {
                Text = $"Found {groups.Count} group(s).",
                Type = MessageTypeEnum.Success
            });
            LogInformation("Listed {Count} LDAP groups", [groups.Count], LogSensitivityLevelEnum.Public);
            return response;
        }

        public Response<LdapGroupOutputDto> GetGroup(string cn)
        {
            var response = new Response<LdapGroupOutputDto>();

            if (string.IsNullOrWhiteSpace(cn))
            {
                response.Messages.Add(new Message { Text = "Group name (cn) is required.", Type = MessageTypeEnum.Validation });
                return response;
            }

            using var connection = CreateAdminConnection();
            connection.Bind();

            var entry = FindEntry(connection, _ldapOptions.GroupsDn,
                $"(&(objectClass=groupOfNames)(cn={EscapeLdapFilter(cn)}))", GroupAttributes);

            if (entry is null)
            {
                response.Messages.Add(new Message { Text = $"Group '{cn}' was not found.", Type = MessageTypeEnum.Validation });
                return response;
            }

            response.Data = MapToGroupDto(entry);
            response.Messages.Add(new Message { Text = "Group retrieved successfully.", Type = MessageTypeEnum.Success });
            return response;
        }

        public Response AddUserToGroup(GroupMembershipInputDto input)
        {
            var response = new Response();

            if (string.IsNullOrWhiteSpace(input.Uid))
            {
                response.Messages.Add(new Message { Text = "User ID (uid) is required.", Type = MessageTypeEnum.Validation });
                return response;
            }
            if (string.IsNullOrWhiteSpace(input.GroupCn))
            {
                response.Messages.Add(new Message { Text = "Group name (cn) is required.", Type = MessageTypeEnum.Validation });
                return response;
            }

            using var connection = CreateAdminConnection();
            connection.Bind();

            var userDn = FindUserDn(connection, input.Uid);
            if (userDn is null)
            {
                response.Messages.Add(new Message { Text = $"User '{input.Uid}' was not found.", Type = MessageTypeEnum.Validation });
                return response;
            }

            var groupDn = FindGroupDn(connection, input.GroupCn);
            if (groupDn is null)
            {
                response.Messages.Add(new Message { Text = $"Group '{input.GroupCn}' was not found.", Type = MessageTypeEnum.Validation });
                return response;
            }

            var modification = new DirectoryAttributeModification
            {
                Name = "member",
                Operation = DirectoryAttributeOperation.Add
            };
            modification.Add(userDn);

            try
            {
                connection.SendRequest(new ModifyRequest(groupDn, modification));
            }
            catch (DirectoryOperationException ex) when (ex.Response?.ResultCode == ResultCode.AttributeOrValueExists)
            {
                response.Messages.Add(new Message
                {
                    Text = $"User '{input.Uid}' is already a member of group '{input.GroupCn}'.",
                    Type = MessageTypeEnum.Warning
                });
                return response;
            }

            response.Messages.Add(new Message
            {
                Text = $"User '{input.Uid}' added to group '{input.GroupCn}' successfully.",
                Type = MessageTypeEnum.Success
            });
            LogInformation("Added user {Uid} to group {Group}", [input.Uid, input.GroupCn], LogSensitivityLevelEnum.Public);
            return response;
        }

        public Response RemoveUserFromGroup(GroupMembershipInputDto input)
        {
            var response = new Response();

            if (string.IsNullOrWhiteSpace(input.Uid))
            {
                response.Messages.Add(new Message { Text = "User ID (uid) is required.", Type = MessageTypeEnum.Validation });
                return response;
            }
            if (string.IsNullOrWhiteSpace(input.GroupCn))
            {
                response.Messages.Add(new Message { Text = "Group name (cn) is required.", Type = MessageTypeEnum.Validation });
                return response;
            }

            using var connection = CreateAdminConnection();
            connection.Bind();

            var userDn = FindUserDn(connection, input.Uid);
            if (userDn is null)
            {
                response.Messages.Add(new Message { Text = $"User '{input.Uid}' was not found.", Type = MessageTypeEnum.Validation });
                return response;
            }

            var groupDn = FindGroupDn(connection, input.GroupCn);
            if (groupDn is null)
            {
                response.Messages.Add(new Message { Text = $"Group '{input.GroupCn}' was not found.", Type = MessageTypeEnum.Validation });
                return response;
            }

            var modification = new DirectoryAttributeModification
            {
                Name = "member",
                Operation = DirectoryAttributeOperation.Delete
            };
            modification.Add(userDn);

            try
            {
                connection.SendRequest(new ModifyRequest(groupDn, modification));
            }
            catch (DirectoryOperationException ex) when (ex.Response?.ResultCode == ResultCode.NoSuchAttribute)
            {
                response.Messages.Add(new Message
                {
                    Text = $"User '{input.Uid}' is not a member of group '{input.GroupCn}'.",
                    Type = MessageTypeEnum.Warning
                });
                return response;
            }
            catch (DirectoryOperationException ex) when (ex.Response?.ResultCode == ResultCode.ObjectClassViolation)
            {
                response.Messages.Add(new Message
                {
                    Text = $"Cannot remove the last member from group '{input.GroupCn}'. The groupOfNames object class requires at least one member.",
                    Type = MessageTypeEnum.Validation
                });
                return response;
            }

            response.Messages.Add(new Message
            {
                Text = $"User '{input.Uid}' removed from group '{input.GroupCn}' successfully.",
                Type = MessageTypeEnum.Success
            });
            LogInformation("Removed user {Uid} from group {Group}", [input.Uid, input.GroupCn], LogSensitivityLevelEnum.Public);
            return response;
        }

        public Response<bool> Authenticate(AuthenticateInputDto input)
        {
            var response = new Response<bool>();

            if (string.IsNullOrWhiteSpace(input.Uid))
            {
                response.Messages.Add(new Message { Text = "User ID (uid) is required.", Type = MessageTypeEnum.Validation });
                return response;
            }
            if (string.IsNullOrWhiteSpace(input.Password))
            {
                response.Messages.Add(new Message { Text = "Password is required.", Type = MessageTypeEnum.Validation });
                return response;
            }

            using var adminConnection = CreateAdminConnection();
            adminConnection.Bind();

            var userDn = FindUserDn(adminConnection, input.Uid);
            if (userDn is null)
            {
                response.Data = false;
                response.Messages.Add(new Message { Text = "Invalid credentials.", Type = MessageTypeEnum.Validation });
                LogWarning("Authentication failed – user not found: {Uid}", [input.Uid], LogSensitivityLevelEnum.PrivateProtected);
                return response;
            }

            try
            {
                using var userConnection = CreateUserConnection(userDn, input.Password);
                userConnection.Bind();
            }
            catch (LdapException ex) when (ex.ErrorCode == 49)
            {
                response.Data = false;
                response.Messages.Add(new Message { Text = "Invalid credentials.", Type = MessageTypeEnum.Validation });
                LogWarning("Authentication failed – invalid password for user {Uid}", [input.Uid], LogSensitivityLevelEnum.PrivateProtected);
                return response;
            }

            response.Data = true;
            response.Messages.Add(new Message { Text = "Authentication successful.", Type = MessageTypeEnum.Success });
            LogInformation("User {Uid} authenticated successfully", [input.Uid], LogSensitivityLevelEnum.Public);
            return response;
        }

        public Response<List<Dictionary<string, string>>> SearchEntries(string filter, string[]? attributes = null)
        {
            var response = new Response<List<Dictionary<string, string>>>();
            var results = new List<Dictionary<string, string>>();

            using var connection = CreateAdminConnection();
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
            return response;
        }

        private LdapConnection CreateAdminConnection()
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

        private LdapConnection CreateUserConnection(string userDn, string password)
        {
            var identifier = new LdapDirectoryIdentifier(_ldapOptions.Host, _ldapOptions.Port);
            var credential = new NetworkCredential(userDn, password);
            var connection = new LdapConnection(identifier, credential)
            {
                AuthType = AuthType.Basic
            };
            connection.SessionOptions.ProtocolVersion = 3;
            return connection;
        }

        private static SearchResultEntry? FindEntry(LdapConnection connection, string baseDn, string filter, string[]? attributes = null)
        {
            var request = new SearchRequest(baseDn, filter, SearchScope.OneLevel, attributes);
            var searchResponse = (SearchResponse)connection.SendRequest(request);
            return searchResponse.Entries.Count > 0 ? searchResponse.Entries[0] : null;
        }

        private string? FindUserDn(LdapConnection connection, string uid)
        {
            var entry = FindEntry(connection, _ldapOptions.UsersDn,
                $"(&(objectClass=inetOrgPerson)(uid={EscapeLdapFilter(uid)}))", ["uid"]);
            return entry?.DistinguishedName;
        }

        private string? FindGroupDn(LdapConnection connection, string cn)
        {
            var entry = FindEntry(connection, _ldapOptions.GroupsDn,
                $"(&(objectClass=groupOfNames)(cn={EscapeLdapFilter(cn)}))", ["cn"]);
            return entry?.DistinguishedName;
        }

        private static LdapUserOutputDto MapToUserDto(SearchResultEntry entry) => new()
        {
            Dn = entry.DistinguishedName,
            Uid = GetAttribute(entry, "uid"),
            CommonName = GetAttribute(entry, "cn"),
            FirstName = GetAttribute(entry, "givenName"),
            LastName = GetAttribute(entry, "sn"),
            Email = GetAttribute(entry, "mail"),
            PhoneNumber = GetAttribute(entry, "telephoneNumber")
        };

        private static LdapGroupOutputDto MapToGroupDto(SearchResultEntry entry) => new()
        {
            Dn = entry.DistinguishedName,
            Cn = GetAttribute(entry, "cn"),
            Members = GetAttributeValues(entry, "member")
        };

        private static string GetAttribute(SearchResultEntry entry, string name)
        {
            if (entry.Attributes.Contains(name) && entry.Attributes[name].Count > 0)
                return entry.Attributes[name][0]?.ToString() ?? string.Empty;
            return string.Empty;
        }

        private static List<string> GetAttributeValues(SearchResultEntry entry, string name)
        {
            var values = new List<string>();
            if (!entry.Attributes.Contains(name))
                return values;

            for (int i = 0; i < entry.Attributes[name].Count; i++)
            {
                var val = entry.Attributes[name][i]?.ToString();
                if (!string.IsNullOrEmpty(val))
                    values.Add(val);
            }
            return values;
        }

        private static DirectoryAttributeModification CreateModification(string attributeName, string value)
        {
            var mod = new DirectoryAttributeModification
            {
                Name = attributeName,
                Operation = DirectoryAttributeOperation.Replace
            };
            mod.Add(value);
            return mod;
        }

        private static string EscapeLdapFilter(string input)
        {
            return input
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29")
                .Replace("\0", "\\00");
        }
    }
}
