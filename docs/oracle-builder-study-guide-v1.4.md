# Oracle Builder — Study Guide v1.4 ("SQLite")

**A follow-up to [`oracle-builder-study-guide.md`](./oracle-builder-study-guide.md) (v1.0),
[`oracle-builder-study-guide-v1.1.md`](./oracle-builder-study-guide-v1.1.md),
[`oracle-builder-study-guide-v1.2.md`](./oracle-builder-study-guide-v1.2.md) and
[`oracle-builder-study-guide-v1.3.md`](./oracle-builder-study-guide-v1.3.md).**
All four are frozen as the record of what they described. This document is the pass that
**moved the app off SQL Server and onto SQLite.**

Read v1.0 through v1.3 first. Everything here assumes them.

**This is again not the pass the previous guide predicted.** v1.3 §12 expected v1.4 to be the
roll/draw play page. It is not — that work moves to **v1.5**, and v1.2 §11 remains its brief.
This pass came in ahead of it for a reason worth stating: the tool is meant to be *distributed*.
Anyone should be able to run their own instance and take a copy of someone else's. A database
server you must install first is a tax on every one of those people, and SQL Server was buying
us nothing — no feature in this app needs it.

---

## 0. Catch-up — the parts of the earlier guides that matter here

**Store the specific thing, derive the general thing.** (v1.1 §1) It shows up here as a
question about seed data: are the five game systems part of the *schema* or part of a
*database*? This pass answers "neither — they were test fixtures wearing a schema costume."

**Export/import carries no database IDs.** (v1.1 §4, v1.3 §5) That decision is what let this
migration be verified honestly: an export written by the SQL Server build imports into the
SQLite build unchanged, because the file never depended on the database that wrote it.

**App-level guard for the message, database constraint for the guarantee.** (v1.3, principle
26) §3 below is a case where the *database constraint* silently changed meaning under us, and
the app-level guard changed with it. Both had to be repaired together.

**Running lists continue:** principles (v1.3 §10 ended at 28 — this pass starts at 29) and the
appendix gotcha table.

---

## 1. The theme of this pass: what the database was actually for

Before writing any code, it's worth asking what SQL Server was providing. The honest audit:

| SQL Server feature | Used by this app? |
| --- | --- |
| Stored procedures, views, functions | No |
| `decimal`, `Guid`, `rowversion`, computed columns, sequences | No |
| Concurrency tokens | No |
| Connection resiliency / retry policies | No |
| Multiple concurrent writers | No — it is one gamemaster at a laptop |
| `DBCC CHECKIDENT` | Yes, in exactly one place |

One usage, and that one was already guarded off. The entire footprint was **one package
reference, one `UseSqlServer` call, one connection string, and one line of T-SQL.**

The tell that this was always going to work: **the test suite had been running on SQLite since
v1.0.** `Support/TestDb.cs` builds the whole schema — Identity tables included — on
`DataSource=:memory:`. The model had been proven provider-portable for four passes without
anyone framing it that way.

**Principle 29** (below) is the general form: *the size of a migration is the size of the
surface you actually touched, not the size of the thing you are migrating away from.*

---

## 2. The three parts that were genuinely mechanical

Worth listing so the interesting parts stand out by contrast.

**The package.** `Microsoft.EntityFrameworkCore.SqlServer` → `Microsoft.EntityFrameworkCore.Sqlite`,
same version. This also drops two transitive dependencies you never asked for,
`Microsoft.Data.SqlClient` and `Azure.Identity`.

**The registration.** `options.UseSqlServer(cs)` → `options.UseSqlite(cs)` in `Program.cs`.

**The connection string.**

```
"Server=(localdb)\\mssqllocaldb;Database=aspnet-GM_Toolbox-4adee...;Trusted_Connection=True"
```
became
```
"Data Source=App_Data/gmtoolbox.db"
```

A second connection string named `ApplicationDbContext` had been sitting in `appsettings.json`
since the project template and was read by nothing. Dead configuration is worse than no
configuration, because the next person to read the file has to work out which one is live.
Deleted.

### The migrations had to be regenerated, not ported

The five existing migrations were saturated with SQL Server:

```csharp
SqlServerPropertyBuilderExtensions.UseIdentityColumn(...)
.Annotation("SqlServer:Identity", "1, 1")
type: "nvarchar(max)"
filter: "[NormalizedName] IS NOT NULL"     // filtered index, from Identity's conventions
```

None of that runs on SQLite. Because nothing is deployed anywhere, the honest move is to
**delete `Data/Migrations/` entirely and generate one `InitialCreate`** — the same call v1.3 §4
made when it chose to reset the database rather than write a data migration. A migration
history is a record of how *deployed* databases got to where they are. With no deployed
databases, it was a record of nothing.

---

## 3. The bug the switch introduced: collation

**This is the part of the pass to actually remember.** Everything else either compiled or
failed loudly. This one would have compiled, passed the entire test suite, and been wrong.

SQL Server's default collation compares text **case-insensitively**. SQLite's default is
**binary**. Two places had been quietly relying on SQL Server's behaviour:

```csharp
// GameSystemsController — the duplicate-name guard
await _context.GameSystems.AnyAsync(g => g.Name == gameSystem.Name);

// ApplicationDbContext — the constraint behind it
builder.Entity<GameSystem>().HasIndex(g => g.Name).IsUnique();
```

Under SQLite, `"Cyberpunk Red" == "cyberpunk red"` is **false**. So both the guard and the
unique index would have let the second one through — and note that this is exactly the pair
v1.3's principle 26 says to keep in sync. They stayed in sync; they were just both wrong.

Worse, a third place disagreed with both:

```csharp
// OracleImporter — find-or-create by name
new Dictionary<string, GameSystem>(StringComparer.OrdinalIgnoreCase)
```

The importer had always matched names case-insensitively **in memory**, and would go on
treating the two rows as one system. So you could end up with two game-system rows that the
importer believes are a single system. Silent, and data-corrupting in a slow way.

The fix is one line, and it belongs in the model rather than in the queries:

```csharp
builder.Entity<GameSystem>()
    .Property(g => g.Name)
    .UseCollation("NOCASE");
```

which generates:

```sql
CREATE TABLE "GameSystems" (
    "ID" INTEGER NOT NULL CONSTRAINT "PK_GameSystems" PRIMARY KEY AUTOINCREMENT,
    "Name" TEXT COLLATE NOCASE NOT NULL,
    ...
```

Putting it on the column rather than writing `EF.Functions.Collate(...)` at each call site
means the unique index inherits it too. One declaration, and the guard and the constraint agree
again — which was the point of principle 26 in the first place.

**Principle 30:** *a provider swap changes the semantics of your queries, not just their
dialect.* The dialect differences fail loudly at the first execution. The semantic ones —
collation, null ordering, integer division, string comparison — compile, run, and return
plausible wrong answers. Those are the ones to go looking for deliberately, because nothing
will tell you.

---

## 4. `DBCC CHECKIDENT` becomes `sqlite_sequence`

Clear All resets the identity counters so a cleared toolbox starts from ID 1 again. The old
code:

```csharp
if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
{
    await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('OracleEntries', RESEED, 0);");
    await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Oracles', RESEED, 0);");
}
```

That `if` deserves a moment. It was added so the **tests** — which run on SQLite — would not
hit T-SQL. It worked. It also meant the reseed was the one piece of the app that no test ever
executed, which v1.1 §5 and the `ClearAllActionTests` class comment both admitted in writing.

Under SQLite, EF declares int keys as `INTEGER PRIMARY KEY AUTOINCREMENT`, which parks a
high-water mark in the internal `sqlite_sequence` table. **That mark survives deleting every
row** — so without a reseed, clearing a well-used toolbox and starting over would hand the next
oracle ID 84. The equivalent:

```csharp
await _context.Database.ExecuteSqlRawAsync(
    "DELETE FROM sqlite_sequence WHERE name IN ('Oracles', 'OracleEntries');");
```

The `if` is gone. The app and the tests now run the same provider, so **the reseed is finally
covered by a test** — `Post_ReseedsIdentityCounters` seeds enough rows to push the counter past
1, clears, and asserts the next oracle *and* the next entry both come back as ID 1.

### The test that had encoded the bug

`EndToEndTests` asserted:

```csharp
// The row is a new one. IDs belong to the database that issued them and are
// deliberately absent from the file.
Assert.NotEqual(weatherId, restored.ID);
```

The *stated* intent is right: the file carries no IDs, so the restored row is freshly issued.
But the assertion only ever passed because the reseed silently no-opped under SQLite. Make the
reseed work and the freshly-issued ID after a full clear **is** 1 — the same value — and the
test goes red.

The failure was correct and the assertion was wrong. It now reads `Assert.Equal(1, restored.ID)`,
which proves strictly more: not just that a new row was issued, but that the counter restarted.

**Principle 31:** *a guard that switches on the provider is an untested branch wearing a safety
jacket.* It keeps the suite green by ensuring the interesting code never runs. When the guard's
condition becomes permanently false — which is what a provider swap does — you inherit code
that has never executed anywhere.

---

## 5. Seed data: whose data was it?

`ApplicationDbContext` seeded five game systems via `HasData` (v1.3 §3). This pass removed
them: a distributed tool has no business deciding that your campaign involves D&D.

But the tests leaned on them — ten references to `TestData.SeededGameSystemId`, plus
`GameSystemsControllerTests` asserting `Assert.Equal(5, model.Count())` and renaming things
onto "Call of Cthulhu" by name. The tempting fix is to rewrite forty call sites.

The better fix is to notice **what the seed had always been**: test fixture data that had been
parked in the schema. So it moved to where fixtures belong — `TestDb`'s constructor, which
inserts the same five rows with the same five IDs right after `EnsureCreated()`. Not one
assertion changed. A real database now starts empty; the test database starts the way the
tests need.

**Principle 32:** *`HasData` is schema, not setup.* It ships in a migration and lands in every
database anyone ever creates, including other people's. If the only thing that genuinely needs
those rows is your test suite, the test suite should be the thing that creates them.

### The consequence in the UI

An empty `GameSystems` table makes Create Oracle a dead end: the dropdown is filled from the
table, and `[Range(1, int.MaxValue)]` on `GameSystemID` (v1.3, principle 24) means an empty
dropdown can never post a valid value. The user gets a form that refuses to submit and no
explanation.

`Views/Oracles/Create.cshtml` now branches: with no game systems it shows a short message and
a link to create one, instead of a form that cannot work.

```cshtml
var gameSystems = ViewBag.GameSystemID as SelectList;
var hasGameSystems = gameSystems is not null && gameSystems.Any();
```

Note it is an `if`/`else`, not an early `return`. A `return` in a Razor body skips the
`@section Scripts { }` registration further down the file, and the section silently never gets
defined.

**Edit was deliberately left alone.** An oracle cannot exist without a game system, so the list
on its Edit form always has at least one entry. An empty-state branch there would be
unreachable code.

**Principle 33:** *removing seed data is a UI change.* Every "create" form whose dropdown comes
from a table now has a reachable empty state, and it is a state no developer with a working
database ever sees.

---

## 6. WAL: the bug that only exists because of what the app is *for*

This one was found by verification, not by reasoning, and it is the most interesting failure of
the pass.

The whole justification for SQLite here is: **your toolbox is one file; copy it to share it.**
So that claim got tested — stop the app, copy `App_Data/gmtoolbox.db` to a second instance,
start it up.

The second instance came up **completely empty.**

EF's SQLite provider turns on WAL journalling (`PRAGMA journal_mode = 'wal'`). Recent writes go
to a `gmtoolbox.db-wal` sidecar rather than into the `.db` itself. While the app runs this is
invisible — SQLite reads the pair as one database. But on disk:

```
gmtoolbox.db        4,096 bytes     <- what you copied
gmtoolbox.db-wal  300,792 bytes     <- where all your oracles actually were
```

Every oracle, every game system, the user account — all of it in the file nobody thought to
copy. This is a data-loss bug in the *headline feature*, and it does not exist in any code
path. It exists in the gap between how SQLite stores data and what the README tells people to
do.

The fix is a checkpoint on shutdown:

```csharp
app.Lifetime.ApplicationStopping.Register(() =>
{
    try
    {
        using var scope = app.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
            .Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(TRUNCATE);");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "Could not checkpoint the SQLite write-ahead log on shutdown.");
    }
});
```

After a clean stop, the `-wal` and `-shm` files are gone and `gmtoolbox.db` holds everything —
verified: 4 KB before, 139 KB after, and the copied file brought up a second instance with all
three oracles and all four game systems.

Two things worth keeping:

- **The `try`/`catch` is not defensive padding.** A tidy-up step must never turn a clean
  shutdown into a crash. Log it and let the process exit.
- **It cannot help a killed process.** `ApplicationStopping` does not run if the process is
  terminated outright, and the `-wal` then still holds live data. The README says so plainly
  rather than pretending the single-file story is unconditional.

**Principle 34:** *verify the feature you justified the change with.* Every unit test passed
before this bug was found, because no unit test copies a file. The claim being made to users
was "copy this one file", so the thing to actually do was copy the one file and see what
happened.

---

## 7. Anchoring the database path

`"Data Source=App_Data/gmtoolbox.db"` is a relative path, and SQLite resolves it against the
**process working directory** — which is not reliably the content root. IIS Express and
`dotnet run` from a parent folder disagree about it, and the symptom is a stray empty `.db`
appearing somewhere unexpected while the app reports no data.

`Program.cs` now resolves it before handing it over:

```csharp
static string ResolveSqlitePath(string connectionString, string contentRootPath)
{
    var csb = new SqliteConnectionStringBuilder(connectionString);

    if (string.IsNullOrWhiteSpace(csb.DataSource) || csb.DataSource == ":memory:")
        return connectionString;

    if (!Path.IsPathRooted(csb.DataSource))
        csb.DataSource = Path.GetFullPath(Path.Combine(contentRootPath, csb.DataSource));

    var folder = Path.GetDirectoryName(csb.DataSource);
    if (!string.IsNullOrEmpty(folder))
        Directory.CreateDirectory(folder);

    return csb.ConnectionString;
}
```

Three details:

- **Parse it, don't string-munge it.** `SqliteConnectionStringBuilder` handles quoting and the
  other keywords; a `Replace("Data Source=", ...)` would break on the first connection string
  that carried a second setting.
- **`:memory:` is passed through untouched**, or the in-memory database becomes a file called
  `:memory:`.
- **An absolute path is respected as-is**, which is what makes the override work — and it is
  the path the second-instance test exercised.

`Directory.CreateDirectory` is a no-op on an existing folder, so it cannot disturb the export
archives that also live under `App_Data/` (v1.1 §4).

### Migrate on startup

```csharp
using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
}
```

The database used to be created out of band with `dotnet ef database update`. For a tool whose
pitch is "run your own instance", a mandatory CLI step before first launch is a real barrier.
SQLite has nothing to connect to and nothing to provision, so the app can just make its own
database. `dotnet run` on a fresh clone now works.

This is a deliberate trade, not a default to copy blindly: auto-migrating on startup is
reasonable for a single-user local app and is exactly the sort of thing you would think twice
about with several servers racing to migrate one shared database.

---

## 8. What changed in the test suite

**137 → 141 tests, all green.**

| Change | File | Why |
| --- | --- | --- |
| Fixture systems moved out of `HasData` | `Support/TestDb.cs` | §5 — the schema stopped shipping them |
| Doc comments corrected | `Support/TestData.cs`, `GameSystemsControllerTests.cs` | They described a seed that no longer exists |
| `Assert.NotEqual` → `Assert.Equal(1, ...)` | `EndToEndTests.cs` | §4 — the old assertion encoded the un-run reseed |
| **+1** `Post_ReseedsIdentityCounters` | `ClearAllActionTests.cs` | §4 — the reseed is testable for the first time |
| **+3** `Create_NameDifferingOnlyInCase_IsRejected` | `GameSystemsControllerTests.cs` | §3 — a `[Theory]` pinning the collation fix |
| "NOT COVERED: the identity reseed" note deleted | `ClearAllActionTests.cs` | It is covered now |

The most satisfying line in the diff is that last one. The tests got *more* honest by the app
getting *less* clever — one provider everywhere means the suite exercises the same code the app
runs, and a documented coverage hole closed itself.

### Verification beyond the suite

The tests cannot see startup, Razor rendering, or a real file. Those were walked by hand
against a running instance: first-run database creation, registering an account (Identity on
SQLite), the case-insensitive duplicate rejection, the full d6 build (bands 1–2 / 3–4 / 5–6), a
52-row card deck, Export with its server-side archive copy, Clear All, re-importing a **21 KB
export written by the SQL Server build**, and finally copying the `.db` to a second instance on
another port — which is how §6 was found.

That last import is the proof that mattered most for §9 below: a file written by the old
database loaded into the new one with no format change at all.

---

## 9. Export/import: the part that correctly did nothing

`OracleExporter`, `OracleImporter` and `OracleJson` were not modified. Not one line.

They hold no `DbContext`, issue no SQL, and know nothing about a provider — a consequence of
the layering v1.0 §7 set up and v1.3 §5 defended when `TryBuild` needed the game-system list
and took it as a parameter rather than reaching for a context.

`formatVersion` stays **1**, and it should: the format did not change, so claiming it did would
be a lie told to every file already written. Files exported before this migration import after
it. That is not luck — it is v1.1 §4's decision to keep database IDs out of the file paying off
in the exact scenario it was designed for.

One behaviour *did* change, and it is a consequence of §5 rather than of the provider: with no
seeded systems, importing an old file auto-creates its game systems with `Genre = "Unknown"`
(v1.3 §5) instead of matching pre-existing rows. Confirmed during the walkthrough. Harmless,
but it is what a first import into a fresh instance now looks like.

**Principle 35:** *the layer that does not mention the database does not change when the
database changes.* The reward for keeping the transfer layer pure is collected years later,
in one line of diff, on a day you were expecting trouble.

---

## 10. Principles this pass added

Continuing v1.3 §10, which ended at 28.

29. **The size of a migration is the size of the surface you touched, not the size of the thing
    you are leaving.** Audit before estimating: one package, one registration, one connection
    string and one line of T-SQL, because no feature of the app had ever needed more.
30. **A provider swap changes the semantics of your queries, not just their dialect.** Dialect
    differences fail loudly on first execution. Semantic ones — collation, null ordering,
    integer division — compile, run, and return plausible wrong answers. Hunt those deliberately.
31. **A guard that switches on the provider is an untested branch wearing a safety jacket.** It
    keeps the suite green precisely by ensuring the interesting code never runs. When its
    condition becomes permanently false, you inherit code that has executed nowhere.
32. **`HasData` is schema, not setup.** It ships in a migration into every database anyone ever
    creates. If only the tests need those rows, the tests should create them.
33. **Removing seed data is a UI change.** Every form whose dropdown comes from a table gains a
    reachable empty state — one no developer with a populated database ever sees.
34. **Verify the feature you justified the change with.** The WAL bug passed every unit test,
    because no unit test copies a file. "Copy this one file" was the promise, so copying the one
    file was the test.
35. **The layer that does not mention the database does not change when the database changes.**
    Purity in the transfer layer is a bet that pays out in one line of diff on migration day.

---

## 11. Still open

Carried forward:

- **The roll/draw play page — now v1.5.** Still unbuilt, still the point of the tool. v1.2 §11
  remains its brief, unchanged. Before starting it, v1.3's note still stands: promote
  `OracleListItem.NeedsSetup` to an extension method beside `Category()` / `FaceCount()` /
  `KeyLabel()`.
- **Multi-user.** v1.1 §10, plus v1.3 §9's per-user-vs-shared question for `GameSystems`. The
  `ClearAllConfirmed` TODO still applies and now reads slightly differently: under multi-user
  you would drop the `sqlite_sequence` reseed for the same reason you would have dropped
  `DBCC CHECKIDENT` — you cannot reset a counter on a table holding other people's rows.
- **Parking lot:** importing from `App_Data/Exports` server-side (needs a path-traversal guard);
  export filenames colliding within one second.
- **Deferred from v1.0:** tarot reversed, partial decks, weighted outcomes, free-form ranges,
  sub-tables, entries-form pagination.

Opened by this pass:

- **`MaxLength` is now advisory at the database.** SQL Server would reject an over-length
  string; SQLite stores it. `OracleImporter` validates 100/500 explicitly so the import path is
  covered, and the direct-edit paths rely on `[MaxLength]` model validation — which, per v1.0
  §9, does **not** run in the tests that call controllers directly. Not a live bug; a thinner
  net than there used to be.
- **The Dockerfile is untested.** Its `ENTRYPOINT` said `GM Toolbox.dll` while the assembly is
  `GM_Toolbox` (v1.3's note on the space in the folder name), so the container could never have
  started. That is fixed, along with a `VOLUME` for `/app/App_Data` and a `chown` so the
  non-root app user can create the database file. None of it has been built — the Docker daemon
  was not running.
- **Distributed sharing over HTTP.** Copying a `.db` and mailing a JSON export both work today.
  Pulling oracles from another running instance — yours or an "official" one — is the feature
  this migration was groundwork for, and it is not started.

---

## 12. Looking ahead — v1.5

**The roll/draw play page.** v1.2 §11 is still the brief and nothing in this pass changed it.

Two things this pass leaves for it:

- The page will show an oracle's game system beside it — which is why v1.3 built the table.
  There is now no seeded system to fall back on, so its empty state matters (§5).
- Nothing about rolling or drawing touches the provider. If a query in it behaves oddly, re-read
  principle 30 before assuming it is your logic.

---

## Appendix — new gotchas

| Symptom | Cause | Fix |
| --- | --- | --- |
| Duplicate rows differing only in case, past a unique index | SQLite compares `TEXT` binary; SQL Server's default collation does not | `UseCollation("NOCASE")` on the column, so the index inherits it |
| Cleared database still issues high IDs | `AUTOINCREMENT` parks a high-water mark in `sqlite_sequence` that outlives deleting rows | `DELETE FROM sqlite_sequence WHERE name IN (...)` |
| Copied `.db`, second instance is empty | WAL journalling left the data in the `-wal` sidecar | Checkpoint on shutdown; copy all three files if the process was killed |
| Stray empty `.db` in an unexpected folder | Relative `Data Source` resolves against the process working directory, not the content root | Anchor to `ContentRootPath` before handing the string to `UseSqlite` |
| `@section Scripts` silently missing | An early `return` in a Razor body skips the section registration below it | Use `if`/`else`, not an early return |
| `dotnet ef` not found | `Microsoft.EntityFrameworkCore.Tools` gives you the Package Manager Console cmdlets, not the CLI | `dotnet tool install --global dotnet-ef` |
| `migrations script --idempotent` fails | Not supported on SQLite | Script a range instead: `dotnet ef migrations script 0 InitialCreate` |
