# Avatier – LDAP Identity Management API

A .NET 10 Web API that connects to an OpenLDAP directory and exposes core identity management operations: listing accounts, editing user attributes, managing group membership, changing passwords, and authentication.

## Prerequisites

| Tool | Version |
|------|---------|
| [.NET SDK](https://dotnet.microsoft.com/) | 10.0+ |
| [Docker & Docker Compose](https://docs.docker.com/get-docker/) | 24+ / Compose V2 |

## Quick Start

### 1. Start the infrastructure

```bash
docker-compose up -d
```

This brings up:

| Service | URL / Port | Purpose |
|---------|-----------|---------|
| **OpenLDAP** | `localhost:389` | LDAP directory seeded with test data |
| **phpLDAPadmin** | [http://localhost:8200](http://localhost:8200) | Web UI for browsing the directory |
| **PostgreSQL** | `localhost:5432` | Application database |
| **Seq** | [http://localhost:8100](http://localhost:8100) | Centralized log viewer |

### 2. Run the API

```bash
cd Avatier.Api
dotnet run
```

The API starts on `https://localhost:5001` / `http://localhost:5000` by default.

### 3. Explore the API

Open the Scalar interactive documentation at:

```
https://localhost:5001/scalar/v1
```

The NSwag-generated OpenAPI spec is available at:

```
https://localhost:5001/swagger/v1/swagger.json
```

## Seed Data

The file `ldap/seed.ldif` is mounted into the OpenLDAP container and loaded on first startup. It creates:

### Users (`ou=People,dc=avatier,dc=local`)

| uid | Name | Email | Password |
|-----|------|-------|----------|
| `jdoe` | John Doe | john.doe@avatier.local | `password123` |
| `jsmith` | Jane Smith | jane.smith@avatier.local | `password123` |
| `bwilson` | Bob Wilson | bob.wilson@avatier.local | `password123` |

### Groups (`ou=Groups,dc=avatier,dc=local`)

| cn | Members |
|----|---------|
| `developers` | jdoe, jsmith |
| `admins` | jdoe |

## API Endpoints

### Connection

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/ldap/test-connection` | Verify LDAP connectivity |
| GET | `/api/ldap/search?filter=...` | Raw LDAP search |

### Users

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/users` | List all user accounts |
| GET | `/api/users/{uid}` | Get a single user |
| PATCH | `/api/users/{uid}` | Edit user attributes (firstName, email, phoneNumber) |
| POST | `/api/users/{uid}/change-password` | Change user password |

### Groups

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/groups` | List all groups |
| GET | `/api/groups/{cn}` | Get a single group with its members |
| POST | `/api/groups/add-member` | Add a user to a group |
| POST | `/api/groups/remove-member` | Remove a user from a group |

### Authentication

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/auth/login` | Authenticate via LDAP bind |

## phpLDAPadmin Access

Navigate to [http://localhost:8200](http://localhost:8200) and log in with:

- **Login DN:** `cn=admin,dc=avatier,dc=local`
- **Password:** `admin`

## Configuration

LDAP settings are in `appsettings.Development.json` (local dev) and overridden via environment variables when running inside Docker (`docker-compose.override.yml`).

```jsonc
{
  "Ldap": {
    "Host": "localhost",      // "openldap" inside Docker
    "Port": 389,
    "BaseDn": "dc=avatier,dc=local",
    "AdminDn": "cn=admin,dc=avatier,dc=local",
    "AdminPassword": "admin"
  }
}
```

## Assumptions

- OpenLDAP is used as the directory server with the `osixia/openldap` Docker image.
- Users are stored as `inetOrgPerson` entries under `ou=People`.
- Groups use the `groupOfNames` object class under `ou=Groups`.
- The admin account (`cn=admin`) is used for all server-side LDAP operations; user-initiated actions (password change, login) verify credentials via a separate LDAP bind as the target user.
- `groupOfNames` requires at least one `member`; the API returns a clear message if you try to remove the last member.
- Passwords are stored using OpenLDAP's default hashing scheme.

## Time Consumption

~7 hours total:

- 1.5 hours: Setting up the Api infrastructure (OpenLDAP, phpLDAPadmin, PostgreSQL, Seq)
- 4 hours: Implementing the API endpoints, including LDAP operations and error handling
- 1.5 hours: Reviewing the SQL Migration, testing the API with scalar, and writing this README documentation.

## Considerations:

- First time integrating with OpenLDAP and understanding its schema and operations took some time.
- Some libraries for LDAP were missing and required deep research.
- Architecture was defined to be simple and focused on core identity management features, without over-engineering for extensibility or additional features like provisioning workflows, role-based access control, etc.
- AI was used to assist with generating boilerplate code, improving the readability for this .md files and generate boilerplate code.