# SQL Migration Review — Privileged Users & Last Login Report

## Original Query (MSSQL)

```sql
-- ============================================================
-- Report: Privileged users and last login
-- Monthly audit of active accounts in 'admin' or 'sysop' groups
-- ============================================================
WITH ActiveUsers AS (
    SELECT
        u.UserID,
        u.Username,
        u.Email,
        u.CreatedAt,
        ISNULL(u.DisplayName, u.Username)            -- [1]
            AS DisplayName,
        DATEDIFF(day, u.LastLoginAt, GETDATE())       -- [2]
            AS DaysSinceLogin,
        CONVERT(varchar, u.LastLoginAt, 103)           -- [3]
            AS LastLoginFormatted
    FROM dbo.Users u WITH (NOLOCK)                     -- [4]
    WHERE u.IsActive = 1
      AND u.DeletedAt IS NULL
),
PrivilegedGroups AS (
    SELECT DISTINCT
        ug.UserID,
        STUFF(                                         -- [5]
            (
                SELECT ', ' + g.GroupName              -- [6]
                FROM dbo.UserGroups ug2
                JOIN dbo.Groups g ON g.GroupID = ug2.GroupID
                WHERE ug2.UserID = ug.UserID
                  AND g.GroupType IN ('admin', 'sysop')
                FOR XML PATH(''), TYPE                 -- [7]
            ).value('.', 'nvarchar(max)'),
            1, 2, ''
        ) AS GroupList
    FROM dbo.UserGroups ug WITH (NOLOCK)               -- [4]
    WHERE ug.GroupID IN (
        SELECT GroupID FROM dbo.Groups
        WHERE GroupType IN ('admin', 'sysop')
    )
),
AccessStats AS (
    SELECT
        al.UserID,
        COUNT(*) AS TotalActions,
        SUM(CASE WHEN al.ActionType = 'FAILED_LOGIN'
             THEN 1 ELSE 0 END)                        -- [8]
            AS FailedLogins,
        MAX(al.CreatedAt) AS LastActionAt
    FROM dbo.AuditLog al WITH (NOLOCK)                 -- [4]
    WHERE al.CreatedAt >= DATEADD(month, -3, GETDATE()) -- [2]
    GROUP BY al.UserID
)
SELECT TOP (500)                                        -- [9]
    au.UserID,
    au.DisplayName,
    au.Email,
    au.LastLoginFormatted,
    au.DaysSinceLogin,
    ISNULL(pg.GroupList, '—') AS Groups,               -- [1]
    ISNULL(ast.TotalActions, 0) AS TotalActions,
    ISNULL(ast.FailedLogins, 0) AS FailedLogins,
    CASE
        WHEN au.DaysSinceLogin > 90 THEN 'INACTIVE'
        WHEN ast.FailedLogins >= 5  THEN 'AT_RISK'
        ELSE 'OK'
    END AS RiskStatus
FROM ActiveUsers au
LEFT JOIN PrivilegedGroups pg  ON pg.UserID = au.UserID
LEFT JOIN AccessStats     ast ON ast.UserID = au.UserID
WHERE pg.UserID IS NOT NULL
ORDER BY au.DaysSinceLogin DESC,
         ast.FailedLogins DESC;
```

---

## 1. MSSQL-Specific Syntax — Identification & PostgreSQL Equivalents

### [1] `ISNULL()`

**MSSQL behaviour:** `ISNULL(expr, replacement)` returns `replacement` when `expr` is `NULL`. It is limited to exactly two arguments and coerces the result to the data type of the first argument.

**PostgreSQL equivalent:** Use `COALESCE(expr, replacement)`. `COALESCE` is SQL-standard, accepts two or more arguments, and returns the first non-`NULL` value. It does not silently truncate the replacement to the type length of the first argument, which makes it safer.

```sql
-- MSSQL
ISNULL(u.DisplayName, u.Username)

-- PostgreSQL
COALESCE(u.DisplayName, u.Username)
```

---

### [2] `DATEDIFF()` / `DATEADD()` / `GETDATE()`

**MSSQL behaviour:**
- `DATEDIFF(day, start, end)` returns the integer number of date-part boundaries crossed.
- `DATEADD(month, -3, GETDATE())` subtracts three months from the current timestamp.
- `GETDATE()` returns the current date-time as `datetime`.

**PostgreSQL equivalent:**
- Date arithmetic uses the `-` operator. Subtracting two `date` values returns an integer number of days.
- `GETDATE()` → `NOW()` or `CURRENT_TIMESTAMP`.
- `DATEADD(month, -3, GETDATE())` → `NOW() - INTERVAL '3 months'`.

> ⚠️ **Warning:** `EXTRACT(DAY FROM interval)` only returns the **days component**, not the total days. For example, an interval of `3 mons 15 days` returns `15`, not ~105. Use `date`-level subtraction instead.

```sql
-- MSSQL
DATEDIFF(day, u.LastLoginAt, GETDATE())

-- PostgreSQL (correct — returns total days as integer)
(NOW()::date - u."LastLoginAt"::date)

-- PostgreSQL (INCORRECT — returns only the days component)
-- EXTRACT(DAY FROM (NOW() - u."LastLoginAt"))::int   -- DO NOT USE

-- MSSQL
DATEADD(month, -3, GETDATE())

-- PostgreSQL
NOW() - INTERVAL '3 months'
```

> **Note:** `DATEDIFF` counts date-part boundary crossings. PostgreSQL `date - date` counts calendar days. The results align for the `day` datepart but verify edge-case behavior in tests.

---

### [3] `CONVERT(varchar, value, 103)`

**MSSQL behaviour:** `CONVERT` with style code `103` formats a date as `dd/mm/yyyy`.

**PostgreSQL equivalent:** Use `TO_CHAR`.

```sql
-- MSSQL
CONVERT(varchar, u.LastLoginAt, 103)

-- PostgreSQL
TO_CHAR(u."LastLoginAt", 'DD/MM/YYYY')
```

---

### [4] `WITH (NOLOCK)` — Table Hint

**MSSQL behaviour:** `WITH (NOLOCK)` is equivalent to `SET TRANSACTION ISOLATION LEVEL READ UNCOMMITTED` at the table level. It allows dirty reads and avoids taking shared locks, which reduces blocking under high concurrency.

**PostgreSQL equivalent:** PostgreSQL has **no table-level hint syntax**. Remove `WITH (NOLOCK)` entirely. PostgreSQL uses MVCC by default and its `READ COMMITTED` isolation already provides non-blocking reads of committed data — dirty reads are never allowed.

If the intent was to reduce lock contention in MSSQL without dirty reads, enable **Read Committed Snapshot Isolation (RCSI)** at the database level instead:

```sql
-- MSSQL alternative (database-wide, one-time)
ALTER DATABASE [YourDB] SET READ_COMMITTED_SNAPSHOT ON;
```

In PostgreSQL no action is needed — MVCC handles this natively.

---

### [5] `STUFF()` + [7] `FOR XML PATH('')` — String Aggregation

**MSSQL behaviour:** The classic MSSQL pattern for concatenating rows into a comma-separated list. `FOR XML PATH('')` serialises rows as XML fragments, `.value('.', 'nvarchar(max)')` extracts the text, and `STUFF(..., 1, 2, '')` strips the leading `, `.

**PostgreSQL equivalent:** Use `STRING_AGG()` (available since PostgreSQL 9.0).

```sql
-- MSSQL
STUFF(
    (SELECT ', ' + g.GroupName
     FROM dbo.UserGroups ug2
     JOIN dbo.Groups g ON g.GroupID = ug2.GroupID
     WHERE ug2.UserID = ug.UserID
       AND g.GroupType IN ('admin', 'sysop')
     FOR XML PATH(''), TYPE
    ).value('.', 'nvarchar(max)'),
    1, 2, ''
)

-- PostgreSQL
STRING_AGG(g."GroupName", ', ')
```

> **Important — performance:** The MSSQL version uses a **correlated subquery** that executes once per outer row. See the performance section below for the recommended rewrite.

---

### [6] String Concatenation with `+`

**MSSQL behaviour:** The `+` operator concatenates strings.

**PostgreSQL equivalent:** Use `||`.

```sql
-- MSSQL
', ' + g.GroupName

-- PostgreSQL
', ' || g."GroupName"
```

(Moot after rewriting to `STRING_AGG`, but relevant if porting other expressions.)

---

### [8] `SUM(CASE … THEN 1 ELSE 0 END)` — Conditional Count

**Compatibility:** This pattern is **SQL-standard** and works identically in both MSSQL and PostgreSQL. No change required.

PostgreSQL also supports the shorter `FILTER` clause:

```sql
COUNT(*) FILTER (WHERE al."ActionType" = 'FAILED_LOGIN') AS FailedLogins
```

---

### [9] `SELECT TOP (500)`

**MSSQL behaviour:** `TOP (n)` limits the result set. It is placed immediately after `SELECT`.

**PostgreSQL equivalent:** Use `LIMIT`.

```sql
-- MSSQL
SELECT TOP (500) ...

-- PostgreSQL
SELECT ...
ORDER BY ...
LIMIT 500;
```

---

### Schema Qualification: `dbo.`

**MSSQL behaviour:** `dbo` is the default schema.

**PostgreSQL equivalent:** The default schema is `public`. Remove or replace `dbo.` with `public.` (or the appropriate schema), or rely on `search_path`.

---

### Identifier Casing

**MSSQL behaviour:** Identifiers are case-insensitive by default.

**PostgreSQL behaviour:** Unquoted identifiers are folded to **lowercase**. If tables or columns were created with mixed-case using double quotes (e.g. `"UserID"`), they must always be quoted. Verify the DDL and quote accordingly.

---

## 2. PostgreSQL-Equivalent Query

```sql
-- ============================================================
-- Report: Privileged users and last login (PostgreSQL)
-- Monthly audit of active accounts in 'admin' or 'sysop' groups
-- ============================================================
WITH ActiveUsers AS (
    SELECT
        u."UserID",
        u."Username",
        u."Email",
        COALESCE(u."DisplayName", u."Username")              AS "DisplayName",
        COALESCE(NOW()::date - u."LastLoginAt"::date, 99999)  AS "DaysSinceLogin",
        COALESCE(TO_CHAR(u."LastLoginAt", 'DD/MM/YYYY'), 'Never')
                                                              AS "LastLoginFormatted"
    FROM public."Users" u
    WHERE u."IsActive" = true
      AND u."DeletedAt" IS NULL
),
PrivilegedGroups AS (
    SELECT
        ug."UserID",
        STRING_AGG(g."GroupName", ', ' ORDER BY g."GroupName") AS "GroupList"
    FROM public."UserGroups" ug
    JOIN public."Groups" g ON g."GroupID" = ug."GroupID"
    WHERE g."GroupType" IN ('admin', 'sysop')
    GROUP BY ug."UserID"
),
AccessStats AS (
    SELECT
        al."UserID",
        COUNT(*)                                              AS "TotalActions",
        COUNT(*) FILTER (WHERE al."ActionType" = 'FAILED_LOGIN')
                                                              AS "FailedLogins"
    FROM public."AuditLog" al
    WHERE al."CreatedAt" >= NOW() - INTERVAL '3 months'
    GROUP BY al."UserID"
)
SELECT
    au."UserID",
    au."DisplayName",
    au."Email",
    au."LastLoginFormatted",
    au."DaysSinceLogin",
    COALESCE(pg."GroupList", '—')                             AS "Groups",
    COALESCE(ast."TotalActions", 0)                           AS "TotalActions",
    COALESCE(ast."FailedLogins", 0)                           AS "FailedLogins",
    CASE
        WHEN au."DaysSinceLogin" > 90           THEN 'INACTIVE'
        WHEN COALESCE(ast."FailedLogins", 0) >= 5 THEN 'AT_RISK'
        ELSE 'OK'
    END                                                       AS "RiskStatus"
FROM ActiveUsers au
INNER JOIN PrivilegedGroups pg ON pg."UserID" = au."UserID"
LEFT JOIN AccessStats     ast ON ast."UserID" = au."UserID"
ORDER BY au."DaysSinceLogin" DESC,
         ast."FailedLogins"  DESC,
         au."UserID"         ASC
LIMIT 500;
```

---

## 3. High-Load & Production Readiness (AuditLog at 10 M+ Rows)

### 3.1 AuditLog Filtering — Index on `CreatedAt`

The `AccessStats` CTE filters `AuditLog` with:

```sql
WHERE al.CreatedAt >= DATEADD(month, -3, GETDATE())
```

Without an index, this is a **full sequential scan on 10 M+ rows** — the single biggest bottleneck in this query.

**Recommendation:** Create an index (or verify one exists) on the filter and grouping columns:

```sql
-- MSSQL
CREATE NONCLUSTERED INDEX IX_AuditLog_CreatedAt_UserID
ON dbo.AuditLog (CreatedAt, UserID)
INCLUDE (ActionType);

-- PostgreSQL
CREATE INDEX ix_auditlog_created_userid
ON public."AuditLog" ("CreatedAt", "UserID")
INCLUDE ("ActionType");
```

The `INCLUDE` column covers the `CASE`/`FILTER` expression so the engine can satisfy the query from the index alone (index-only scan).

---

### 3.2 `NOLOCK` Removal — Concurrency Implications

Removing `WITH (NOLOCK)` means the query now takes shared locks under MSSQL's default `READ COMMITTED` isolation. Under heavy write concurrency this can increase lock contention and blocking.

**MSSQL mitigation:** Enable **Read Committed Snapshot Isolation** (RCSI) at the database level. This gives MVCC-style non-blocking reads without dirty-read risk:

```sql
ALTER DATABASE [YourDB] SET READ_COMMITTED_SNAPSHOT ON;
```

**PostgreSQL:** No action needed. PostgreSQL's default `READ COMMITTED` already uses MVCC — readers never block writers and vice versa.

---

### 3.3 Correlated Subquery in `STUFF` / `FOR XML PATH` → Proper JOIN + `STRING_AGG`

The original `PrivilegedGroups` CTE contains a **correlated subquery** inside `STUFF(… FOR XML PATH …)`. This inner `SELECT` executes once per distinct `UserID` row returned by the outer query. At scale, this multiplied I/O is expensive.

**Recommended rewrite (both engines):** Replace the correlated subquery with a simple `JOIN` + `STRING_AGG` (MSSQL 2017+ / PostgreSQL 9.0+):

```sql
-- Rewritten CTE (MSSQL 2017+)
PrivilegedGroups AS (
    SELECT
        ug.UserID,
        STRING_AGG(g.GroupName, ', ')
            WITHIN GROUP (ORDER BY g.GroupName) AS GroupList
    FROM dbo.UserGroups ug
    JOIN dbo.Groups g ON g.GroupID = ug.GroupID
    WHERE g.GroupType IN ('admin', 'sysop')
    GROUP BY ug.UserID
)
```

This executes a single pass over the joined set and aggregates in one step — no per-row subquery overhead.

---

### 3.4 `TOP 500` / `LIMIT 500` Without Stable Ordering & Without Index Support

The final `ORDER BY` is:

```sql
ORDER BY au.DaysSinceLogin DESC, ast.FailedLogins DESC
```

`DaysSinceLogin` is a **computed expression** (`DATEDIFF` in MSSQL / `date` subtraction in PostgreSQL), so no index can directly serve this sort.

**Recommendations:**

1. **Covering index on `Users`:** An index on `(LastLoginAt DESC)` with `INCLUDE (UserID, DisplayName, Email, IsActive, DeletedAt)` lets the engine read users in login-recency order and avoid a sort.

    ```sql
    -- MSSQL
    CREATE NONCLUSTERED INDEX IX_Users_LastLogin
    ON dbo.Users (LastLoginAt DESC)
    INCLUDE (UserID, Username, DisplayName, Email, IsActive, DeletedAt);

    -- PostgreSQL
    CREATE INDEX ix_users_lastlogin
    ON public."Users" ("LastLoginAt" DESC)
    INCLUDE ("UserID", "Username", "DisplayName", "Email", "IsActive", "DeletedAt");
    ```

2. **Stable ordering:** If `DaysSinceLogin` ties are common (e.g., many users logged in the same day), the result order is non-deterministic. Add a tiebreaker column (e.g., `au.UserID`) to ensure reproducible pagination.

    ```sql
    ORDER BY au.DaysSinceLogin DESC,
             ast.FailedLogins DESC,
             au.UserID ASC
    ```

---

### 3.5 CTE Materialisation Behaviour (PostgreSQL ≤ 11 vs. ≥ 12)

| Engine | Behaviour |
|---|---|
| **MSSQL** | CTEs are **inlined** (treated like views); the optimiser can push predicates through them. |
| **PostgreSQL ≤ 11** | CTEs are **optimisation fences** — each CTE is materialised into a temporary result before the outer query runs, even if only a subset of rows is needed. |
| **PostgreSQL ≥ 12** | CTEs referenced **once** are inlined by default. Use `AS MATERIALIZED` / `AS NOT MATERIALIZED` to control. |

**Impact on this query:** The `ActiveUsers` CTE materialises every active, non-deleted user before the outer `WHERE pg.UserID IS NOT NULL` filters to only privileged users. On PostgreSQL ≤ 11 this means unnecessary work.

**Recommendation (PostgreSQL ≥ 12):** Mark the CTE as not materialised to allow predicate push-down:

```sql
WITH ActiveUsers AS NOT MATERIALIZED (
    ...
)
```

Or restructure to move the privilege filter earlier (e.g., `INNER JOIN` instead of `LEFT JOIN … WHERE IS NOT NULL`).

---

### 3.6 `LEFT JOIN … WHERE pg.UserID IS NOT NULL` → `INNER JOIN`

The query uses `LEFT JOIN PrivilegedGroups` then immediately filters with `WHERE pg.UserID IS NOT NULL`, which is semantically identical to an `INNER JOIN`. Most optimisers rewrite this automatically, but being explicit improves readability and guarantees the intent:

```sql
INNER JOIN PrivilegedGroups pg ON pg.UserID = au.UserID
```

---

## 4. Summary of Changes for Migration

| # | MSSQL Feature | PostgreSQL Replacement | Performance Note |
|---|---|---|---|
| 1 | `ISNULL()` | `COALESCE()` | — |
| 2 | `DATEDIFF` / `DATEADD` / `GETDATE()` | `date` subtraction / interval arithmetic / `NOW()` | ⚠️ Do NOT use `EXTRACT(DAY FROM interval)` |
| 3 | `CONVERT(varchar, …, 103)` | `TO_CHAR(…, 'DD/MM/YYYY')` | — |
| 4 | `WITH (NOLOCK)` | Remove; rely on MVCC | Use RCSI in MSSQL as safer alternative |
| 5 | `STUFF(… FOR XML PATH …)` | `STRING_AGG()` | Eliminate correlated subquery |
| 6 | `+` (string concat) | `\|\|` | — |
| 7 | `FOR XML PATH(''), TYPE` | N/A (removed with `STRING_AGG`) | — |
| 8 | `SUM(CASE … END)` | `COUNT(*) FILTER (WHERE …)` (optional) | Both work; `FILTER` is idiomatic PG |
| 9 | `TOP (500)` | `LIMIT 500` | Add stable tiebreaker to `ORDER BY` |
| — | `dbo.` schema | `public.` or `search_path` | — |
| — | Case-insensitive identifiers | Double-quote mixed-case identifiers | Verify DDL |
| — | `IsActive = 1` (`bit`) | `"IsActive" = true` or `"IsActive"` | Verify DDL column type |
| — | `COUNT(*)` returns `int` | `COUNT(*)` returns `bigint` | Cast or update app-layer types |
| — | `STRING_AGG` (no order) | `STRING_AGG(… ORDER BY …)` | Deterministic output |
| — | Unused `LastActionAt` / `CreatedAt` | Remove from CTEs | Reduces unnecessary work |
| — | `NULL` `LastLoginAt` | `COALESCE(…, 99999)` sentinel | Prevents silent misclassification |
| — | `CASE` on nullable `FailedLogins` | `COALESCE` inside `CASE` | NULL ≥ 5 is UNKNOWN, not false |

### Required Indexes Before Production

```sql
-- 1. AuditLog — critical for the 3-month date filter
CREATE INDEX ix_auditlog_created_userid
ON public."AuditLog" ("CreatedAt", "UserID") INCLUDE ("ActionType");

-- 2. Users — supports ORDER BY on login recency
CREATE INDEX ix_users_lastlogin
ON public."Users" ("LastLoginAt" DESC)
INCLUDE ("UserID", "Username", "DisplayName", "Email", "IsActive", "DeletedAt");

-- 3. Groups — supports the IN filter
CREATE INDEX ix_groups_grouptype
ON public."Groups" ("GroupType") INCLUDE ("GroupID");

-- 4. UserGroups — supports the JOIN
CREATE INDEX ix_usergroups_userid_groupid
ON public."UserGroups" ("UserID", "GroupID");
```

---

## 5. Additional Recommendations

### 5.1 ⚠️ `EXTRACT(DAY FROM interval)` — Correctness Bug (Fixed in Section 2)

The original PostgreSQL equivalent for `DaysSinceLogin` was **incorrect**:

```sql
EXTRACT(DAY FROM (NOW() - u."LastLoginAt"))::int
```

`EXTRACT(DAY FROM interval)` returns only the **days component** of the interval, not the total number of days. For example, if a user last logged in 3 months and 15 days ago, the interval is `3 mons 15 days` and `EXTRACT(DAY ...)` returns **15**, not ~105.

**Fix:** Use date-level subtraction, which returns a plain integer in PostgreSQL:

```sql
-- Correct: returns total number of days as an integer
(NOW()::date - u."LastLoginAt"::date) AS "DaysSinceLogin"
```

Or use epoch-based arithmetic for sub-day precision:

```sql
(EXTRACT(EPOCH FROM (NOW() - u."LastLoginAt")) / 86400)::int AS "DaysSinceLogin"
```

> **Impact:** This bug silently produces wrong values for any user who last logged in more than one month ago, which directly affects the `INACTIVE` / `AT_RISK` classification in `RiskStatus`. **Already corrected in the Section 2 query.**

---

### 5.2 `IsActive = 1` — Boolean Semantics

In MSSQL, `IsActive` is typically a `bit` column compared with `= 1`. In PostgreSQL, if the column is defined as `boolean` (the idiomatic choice), the comparison should use a boolean literal or be implicit:

```sql
-- MSSQL
WHERE u.IsActive = 1

-- PostgreSQL (if boolean column)
WHERE u."IsActive" = true
-- or simply:
WHERE u."IsActive"
```

If the column is kept as `integer` (smallint/int) in PostgreSQL, `= 1` still works. Verify the DDL and adjust accordingly.

---

### 5.3 `STRING_AGG` — Non-Deterministic Ordering (Fixed in Section 2)

Without an explicit `ORDER BY`, `STRING_AGG` output may vary between executions. Both PostgreSQL and MSSQL 2017+ support ordering within the aggregate:

```sql
-- PostgreSQL
STRING_AGG(g."GroupName", ', ' ORDER BY g."GroupName") AS "GroupList"

-- MSSQL 2017+
STRING_AGG(g.GroupName, ', ') WITHIN GROUP (ORDER BY g.GroupName) AS GroupList
```

This makes the output deterministic and easier to compare in audit logs or UI. **Already applied in the Section 2 query.**

---

### 5.4 Unused Columns: `LastActionAt` and `CreatedAt` (Fixed in Section 2)

The original `AccessStats` CTE computes `MAX(al."CreatedAt") AS "LastActionAt"` and the `ActiveUsers` CTE selects `u."CreatedAt"` — neither is referenced in the outer `SELECT`. This forces the engine to track unnecessary aggregates and carry extra columns.

**Recommendation:** Remove them to reduce work, or add them to the final output if useful for the audit report.

```sql
-- Removed from AccessStats CTE:
--  MAX(al."CreatedAt") AS "LastActionAt"   -- unused

-- Removed from ActiveUsers CTE:
--  u."CreatedAt"                            -- unused
```

**Already removed in the Section 2 query.**

---

### 5.5 `RiskStatus` Priority — Overlapping Conditions

The `CASE` expression evaluates conditions in order:

```sql
CASE
    WHEN au."DaysSinceLogin" > 90  THEN 'INACTIVE'
    WHEN ast."FailedLogins" >= 5   THEN 'AT_RISK'
    ELSE 'OK'
END
```

A user who is both inactive (> 90 days) **and** has ≥ 5 failed logins is classified as `INACTIVE` only — the `AT_RISK` condition is never reached. If the intent is to flag combined risk, consider a fourth status or reorder the priority:

```sql
CASE
    WHEN au."DaysSinceLogin" > 90 AND ast."FailedLogins" >= 5 THEN 'INACTIVE_AT_RISK'
    WHEN ast."FailedLogins" >= 5                               THEN 'AT_RISK'
    WHEN au."DaysSinceLogin" > 90                              THEN 'INACTIVE'
    ELSE 'OK'
END
```

Document whichever priority is chosen so future reviewers understand the intent.

---

### 5.6 Timezone Awareness — `GETDATE()` vs. `NOW()`

- `GETDATE()` in MSSQL returns server-local time as `datetime` (no timezone info).
- `NOW()` in PostgreSQL returns `timestamp with time zone`.

If `LastLoginAt` / `CreatedAt` columns are `timestamp without time zone` in PostgreSQL, subtracting a `timestamptz` (`NOW()`) from a `timestamp` value performs an implicit cast that depends on the session's `timezone` setting. This can cause off-by-hours errors around midnight boundaries.

**Recommendation:** Be explicit about types. Either:
- Use `LOCALTIMESTAMP` (returns `timestamp without time zone`) if columns are `timestamp`, or
- Ensure all timestamp columns are `timestamptz` and continue using `NOW()`.

```sql
-- If columns are 'timestamp without time zone':
WHERE al."CreatedAt" >= LOCALTIMESTAMP - INTERVAL '3 months'
```

---

### 5.7 `COUNT(*)` Returns `bigint` in PostgreSQL

In MSSQL, `COUNT(*)` returns `int` (max ~2.1 billion). In PostgreSQL, `COUNT(*)` returns `bigint`. If the application layer (e.g., C#/Npgsql) maps the result to `int`, this type mismatch can cause runtime exceptions when the value exceeds `int.MaxValue`, or simply unexpected type mapping.

**Recommendation:** Either cast explicitly in the query:

```sql
COUNT(*)::int AS "TotalActions"
```

Or update the application-layer DTOs to use `long` instead of `int`.

---

### 5.8 AuditLog Table Partitioning (10 M+ Rows)

Section 3.1 recommends an index on `CreatedAt`, which is essential. However, at 10 M+ rows with a rolling 3-month window, **range partitioning** on `"CreatedAt"` gives additional benefits:

- **Partition pruning:** The planner skips entire partitions outside the date range — faster than even an index scan.
- **Maintenance:** Old partitions can be dropped instantly instead of running expensive `DELETE` statements.
- **Vacuum efficiency:** Only active partitions need frequent vacuuming.

```sql
-- PostgreSQL: partitioned table
CREATE TABLE public."AuditLog" (
    "UserID"     int          NOT NULL,
    "ActionType" varchar(50)  NOT NULL,
    "CreatedAt"  timestamptz  NOT NULL,
    -- other columns ...
) PARTITION BY RANGE ("CreatedAt");

-- Monthly partitions
CREATE TABLE public."AuditLog_2025_04" PARTITION OF public."AuditLog"
    FOR VALUES FROM ('2025-04-01') TO ('2025-05-01');
-- ... one per month, automated via pg_partman or cron
```

Indexes created on the partitioned parent are automatically propagated to each partition.

---

### 5.9 Snapshot Consistency Across CTEs

This report reads from three tables (`Users`, `UserGroups`/`Groups`, `AuditLog`) in three CTEs. Under PostgreSQL's default `READ COMMITTED`, each **statement** sees a consistent snapshot, so a single `SELECT` (even with CTEs) is safe.

However, if the report is ever split into multiple statements (e.g., for pagination or caching), wrap it in an explicit transaction:

```sql
BEGIN ISOLATION LEVEL REPEATABLE READ;
-- run the report query
COMMIT;
```

This guarantees a consistent point-in-time view across all reads — important for audit accuracy.

---

### 5.10 `NULL` Safety — `LastLoginAt` and `FailedLogins` in `CASE` (Fixed in Section 2)

Two NULL-related logic issues exist in the original query:

#### `LastLoginAt IS NULL` → `DaysSinceLogin` is `NULL`

If a user has never logged in, `LastLoginAt` is `NULL`. The date subtraction `NOW()::date - NULL::date` returns `NULL`, so `DaysSinceLogin > 90` evaluates to `UNKNOWN` (treated as false). That user falls through to `'OK'` — almost certainly wrong for a privileged account that has *never authenticated*.

**Fix:** Wrap in `COALESCE` with a large sentinel value so never-logged-in users are always classified as `INACTIVE`:

```sql
COALESCE(NOW()::date - u."LastLoginAt"::date, 99999) AS "DaysSinceLogin"
```

Also format the display column defensively:

```sql
COALESCE(TO_CHAR(u."LastLoginAt", 'DD/MM/YYYY'), 'Never') AS "LastLoginFormatted"
```

#### `ast."FailedLogins"` is `NULL` in the `CASE`

When `AccessStats` produces no row for a user (LEFT JOIN miss), `ast."FailedLogins"` is `NULL`. The `COALESCE(ast."FailedLogins", 0)` on the SELECT-list output alias does **not** affect the `CASE` expression — SQL evaluates `CASE` against the raw column, not the alias. So `NULL >= 5` is `UNKNOWN` → the `AT_RISK` branch is silently skipped.

**Fix:** Use `COALESCE` inside the `CASE`:

```sql
CASE
    WHEN au."DaysSinceLogin" > 90           THEN 'INACTIVE'
    WHEN COALESCE(ast."FailedLogins", 0) >= 5 THEN 'AT_RISK'
    ELSE 'OK'
END
```

> Both fixes are **already applied in the Section 2 query**.
