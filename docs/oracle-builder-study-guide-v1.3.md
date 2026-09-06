# Oracle Builder — Study Guide v1.3 ("Game Systems")

**A follow-up to [`oracle-builder-study-guide.md`](./oracle-builder-study-guide.md) (v1.0),
[`oracle-builder-study-guide-v1.1.md`](./oracle-builder-study-guide-v1.1.md) and
[`oracle-builder-study-guide-v1.2.md`](./oracle-builder-study-guide-v1.2.md).**
All three are frozen as the record of what they described. This document is the pass that
turned **game system from a hard-coded enum into a table the user owns.**

Read v1.0, then v1.1, then v1.2. Everything here assumes them.

**This is not the pass v1.2 §11 predicted.** That section expected v1.3 to be the roll/draw
play page. It is not — that work moves to **v1.4**, and everything v1.2 §11 says about it is
still accurate and still the first thing the next session should read. This pass came in
ahead of it because the play page will want an oracle's game system displayed next to it, and
"game system" was still five names baked into a `switch`.

---

## 0. Catch-up — the parts of the earlier guides that matter here

**An oracle is a header plus one row per outcome.** `Oracle` holds the name, the game system
and the `OracleSubtype`; `OracleEntry` holds one row per result. (v1.0 §2, §3)

**Store the specific thing, derive the general thing.** `OracleSubtype` is stored;
`OracleCategory` is computed from it. v1.1 §1 made this the whole theme of that pass and
deleted the `DiceSize` column for breaking it. This pass applies the same idea one level up:
a game system was a *value* (an enum member) and is now an *entity* (a row with its own
fields). (v1.1 §1)

**`.Include` or the navigation comes back empty.** EF loads only what you ask for; a missing
`.Include` is not an error, just an empty collection or a null reference. v1.0 §6 recorded it,
v1.1 §4 hit it in `Export`, v1.2 §5 hit it again. **It happens a fourth time below.** (v1.0 §6)

**Load-then-assign, never `_context.Update()` on the bound object.** v1.1 §3 established this
for `Oracle.Edit` after `_context.Update()` silently nulled `DiceSize` on every save. The new
`GameSystems.Edit` follows the same rule. (v1.1 §3)

**Two of the guides' running lists continue here:** principles (v1.1 §8 ended at 14, v1.2 §9
ended at 21 — this pass starts at 22) and the appendix gotcha tables.

---

## 1. The theme of this pass: a value becomes an entity

v1.1's theme was "one source of truth." This pass is a specific, larger instance of it.

`GameSystem` started life as an `enum` in `Models/Enums/`:

```csharp
public enum GameSystem
{
    [Display(Name = "Dungeons and Dragons")] DungeonsAndDragons = 0,
    [Display(Name = "Cyberpunk Red")]        CyberpunkRed = 1,
    // ...three more
}
```

Stored on `Oracle` as a string via `HasConversion<string>()`. The dropdown on Create and Edit
was `Html.GetEnumSelectList<GameSystem>()`.

The problem with that is not technical, it is a product limit hiding in the type system:
**only a developer editing C# and shipping a build could add a game system.** The user is a
gamemaster who will want to add their own homebrew systems. An enum cannot grow at runtime; a
table can.

So the change is: `GameSystem` becomes a class with `ID`, `Name`, `Genre` and an optional
`PrimaryOracleType`; `Oracle` gets a foreign key to it; and there is a full CRUD screen —
`GameSystemsController` and `Views/GameSystems/` — reachable from a new **Game Systems** nav
tab between Privacy and Oracles.

Everything hard about this pass comes from one fact: **the enum and the entity cannot both be
called `GameSystem` in scope at the same time.** Deleting the enum was step one, and it broke
the build until every reference had been converted.

---

## 2. Deleting a type is a red-build operation

The build order for this pass, in the sequence each piece has to exist:

1. **Delete** `Models/Enums/GameSystem.cs`. (build red from here)
2. **Create** `Models/GameSystem.cs` — the entity.
3. **Rework** `Models/Oracle.cs` — the FK.
4. **Rework** `Data/ApplicationDbContext.cs` — DbSet, relationship, seed.
5. `OracleListItem` → 6. `OracleExporter` → 7. `OracleImporter` → 8. `OraclesController` →
   9. the five Oracle views. (build green again)
10. `GameSystemsController` → 11. `Views/GameSystems/*` → 12. nav link.
13. `dotnet ef migrations add` → 14. drop + `database update`.
15. Tests.

**Steps 1 through 9 are one continuous red build.** There is no way to keep the enum around
"just until the entity is ready" — they share a name and a namespace, so the moment the
entity file exists, every use of `GameSystem` is ambiguous. Deleting the enum first at least
makes the compiler errors honest: "does not exist" rather than "ambiguous reference."

This is the mirror image of v1.1 §2's "build order runs backwards when you delete." There,
deleting `DiceSize` meant fixing consumers before the property was gone. Here, deleting a
whole type means the fixes cannot be staged at all — you convert every consumer and the build
comes back in one step, at step 9.

**What made it survivable:** the compiler lists every site. Nine files, each a mechanical
swap of `GameSystem` (the value) for `GameSystemID` (the key) or `GameSystem` (the nav). The
red build is a to-do list, not a puzzle. Work down it and stop reading errors only when the
count hits zero.

One surprise worth noting: a few consumers **compiled without change** because the entity type
slots in where the enum was. `OracleListItem` had `public GameSystem GameSystem { get; set; }`
— still a legal property, now typed as the entity. `oracle.GameSystem.ToString()` in the
exporter still compiles — `ToString()` exists on every object. These are the dangerous ones:
green build, wrong behaviour (`ToString()` on an entity returns the class name). The rule is
to change every site the *design* touches, not only the ones the compiler flags.

---

## 3. The `GameSystem` entity and the foreign key

### The entity

```csharp
public class GameSystem
{
    public int ID { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string Genre { get; set; } = string.Empty;

    [Display(Name = "Primary Oracle Type")]
    public OracleSubtype? PrimaryOracleType { get; set; }
}
```

`Genre` is free-form text, required. `PrimaryOracleType` is a **nullable** `OracleSubtype` —
the system's usual die or deck, optional, and metadata only at this point (nothing reads it
yet; see §9).

### The `Oracle` side

```csharp
[Display(Name = "Game System")]
[Range(1, int.MaxValue, ErrorMessage = "Pick a game system.")]
public int GameSystemID { get; set; }
public GameSystem? GameSystem { get; set; }
```

`GameSystemID` is the column; `GameSystem` is the navigation property. This is EF's
foreign-key convention — a property named `<Nav>Id`/`<Nav>ID` paired with a reference nav is
recognised automatically.

**Why `[Range(1, int.MaxValue)]` and not `[Required]`.** An `int` is a value type. It is
never "missing" — an unset `int` is `0`. `[Required]` on it does nothing useful. But `0` is
not a valid primary key, so an untouched `<select>` (or a tampered form) posts `0`, and
without a check that reaches the database as a `DbUpdateException` — a 500, not a validation
message. `[Range(1, …)]` rejects `0` in `ModelState` before the save. This is the same class
of problem as v1.1 §2's "`int`, not `int?`, on the view model", pointing the other way: there
the default value was the safe one, here it is the broken one.

### The relationship, configured explicitly

```csharp
builder.Entity<Oracle>()
    .HasOne(o => o.GameSystem)
    .WithMany()
    .HasForeignKey(o => o.GameSystemID)
    .OnDelete(DeleteBehavior.Restrict);

builder.Entity<GameSystem>()
    .HasIndex(g => g.Name)
    .IsUnique();

builder.Entity<GameSystem>()
    .Property(g => g.PrimaryOracleType)
    .HasConversion<string>();
```

**`OnDelete(DeleteBehavior.Restrict)` is the important line.** A *required* relationship —
which this is, because `GameSystemID` is non-nullable — defaults to `DeleteBehavior.Cascade`
in EF Core. Left at the default, deleting a game system would delete every oracle that used
it, silently, as a side effect. `Restrict` makes the database refuse the delete instead.
There is an app-level guard too (§6), but this is the guarantee; the guard is only the
message.

**The unique index on `Name`** exists because the importer (§5) treats the name as the
system's identity — it looks a system up by name, so two rows with the same name would make
that lookup ambiguous.

**`HasConversion<string>()` on a nullable enum** works — EF writes a nullable string column.
It came out as `nvarchar(max)` because no length was configured; harmless, and not worth a
migration to tighten. Noted in §9.

### Seeding with `HasData`

```csharp
builder.Entity<GameSystem>().HasData(
    new GameSystem { ID = 1, Name = "Dungeons and Dragons", Genre = "Fantasy" },
    new GameSystem { ID = 2, Name = "Cyberpunk Red",        Genre = "Sci-fi" },
    new GameSystem { ID = 3, Name = "Call of Cthulhu",      Genre = "Horror" },
    new GameSystem { ID = 4, Name = "Riftbreakers",         Genre = "Fantasy" },
    new GameSystem { ID = 5, Name = "Runescape Kingdoms",   Genre = "Fantasy" }
);
```

`HasData` bakes the rows into the migration, with fixed IDs so re-running is deterministic.
The five names are the old enum members, so nothing a user could pick before disappeared.
Genres are a starting guess and editable through the new screen.

Two things `HasData` gives for free:
- The test database gets the same five rows. `EnsureCreated()` — how `TestDb` builds the
  schema — applies `HasData` seeds, so every test starts with systems 1–5 present.
- IDs 1 and 2 are stable references the tests can name (`TestData.SeededGameSystemId`).

---

## 4. The migration, and choosing to reset the database

Converting a stored value into a foreign key on a table that already has rows is a genuine
data migration: the existing `GameSystem` string on each oracle would need mapping to a new
`GameSystemID`. The generated migration does the mechanical part —

```csharp
migrationBuilder.DropColumn(name: "GameSystem", table: "Oracles");
migrationBuilder.AddColumn<int>(name: "GameSystemID", table: "Oracles",
    nullable: false, defaultValue: 0);
```

— but that `defaultValue: 0` is a broken foreign key for any oracle row that already exists.
On a populated database this migration needs a hand-written `UPDATE` between those two
statements, mapping `"DungeonsAndDragons"` → `1` and so on.

**The decision was to not write that**, because the dev database held nothing worth keeping:

```
dotnet ef database drop -f
dotnet ef database update
```

`update` replays every migration from scratch onto an empty database, `HasData` seed
included. This is the right call *for a solo project with throwaway data* and the wrong call
the moment there is a real database behind it — worth being clear which situation you are in
before running `drop`.

The rest of the migration is unremarkable: create the `GameSystems` table, insert the five
seed rows, index `GameSystemID` on `Oracles`, unique-index `Name` on `GameSystems`, add the
FK with `ReferentialAction.Restrict`.

---

## 5. Import: a pure function that needed data it could not reach

`OracleImporter.TryBuild` was **pure** — no `DbContext`, no database. v1.1 §4 made a point of
that: it returns an error string or fills an `out` list, and the controller owns everything
stateful.

Find-or-create breaks that cleanly if you let it: the importer now needs to know which game
systems already exist so it can reuse a row rather than create a duplicate. The wrong fix is
to hand it a `DbContext`. The right fix is to **pass the data in**:

```csharp
public static string? TryBuild(
    OracleExportFile? file,
    IEnumerable<GameSystem> existingSystems,
    out List<Oracle> oracles)
```

Inside, a case-insensitive lookup, seeded with what exists:

```csharp
var systemsByName = existingSystems.ToDictionary(
    g => g.Name, g => g, StringComparer.OrdinalIgnoreCase);

// ...per oracle:
if (string.IsNullOrWhiteSpace(dto.GameSystem))
    return $"{label} (\"{dto.Name}\"): game system is missing.";

if (!systemsByName.TryGetValue(dto.GameSystem, out var system))
{
    system = new GameSystem { Name = dto.GameSystem, Genre = "Unknown" };
    systemsByName[dto.GameSystem] = system;
}
```

Three properties fall out of this shape:

1. **An unknown system is no longer an error.** v1.0's importer rejected anything it did not
   recognise. Now it creates the system with `Genre = "Unknown"`, so importing a file from
   another instance just works and the user tidies the genre afterward. The old
   `TryBuild_UnknownGameSystem_ReturnsError` test was deleted for asserting the old rule.

2. **A missing name is still an error.** Blank or whitespace `gameSystem` returns before
   anything is built — the file is malformed, not merely unfamiliar.

3. **Two oracles naming the same new system share one instance.** The dictionary is written
   back on creation, so the second oracle finds the first's object. Without that, EF would
   try to `INSERT` the system twice and the second hit the unique index on `Name`.

The controller supplies the tracked rows and lets EF's graph traversal do the rest:

```csharp
var existingSystems = await _context.GameSystems.ToListAsync();
var error = OracleImporter.TryBuild(parsed, existingSystems, out var oracles);
// ...
_context.Oracles.AddRange(oracles);
await _context.SaveChangesAsync();   // one call
```

`AddRange` walks each oracle's graph. A `GameSystem` already tracked (from `ToListAsync`) is
left as `Unchanged` and only its key is written to the FK; a `GameSystem` with `ID == 0` is
`Added` and inserted. One `SaveChanges`, so the file lands whole or not at all — the
all-or-nothing rule from v1.1 §4 is intact.

**The export side is one line.** `OracleExporter` now writes `oracle.GameSystem!.Name` instead
of `oracle.GameSystem.ToString()`. The file carries the system *name*, which is exactly what
the importer looks up. "Import must accept everything Export writes" (v1.1 §4) still holds:
export writes a name, import resolves or creates a system from a name.

---

## 6. The `.Include` gotcha, a fourth time — and the SelectList

### The load

`OracleExporter` reads `oracle.GameSystem!.Name`. That `!` is a promise the value is not
null, and it is only kept if the query loaded the navigation:

```csharp
var oracles = await _context.Oracles
    .Include(o => o.OracleEntries)
    .Include(o => o.GameSystem)     // <- without this, GameSystem is null, NRE at export
    .OrderBy(o => o.Name)
    .ToListAsync();
```

The same `.Include(o => o.GameSystem)` was added to `LoadOracleWithEntries` (which the
`Details` page uses) and to the `Delete` GET query. This is the fourth appearance of the same
root cause across four guides — v1.0 §6, v1.1 §4, v1.2 §5, here. At this point it is less a
gotcha than a habit to build: **when a view or a serialiser reaches through a navigation
property, find the query and check the `.Include` is there.**

### The dropdown

A foreign-key `<select>` needs a list of options built server-side:

```csharp
// controller
private async Task<SelectList> GameSystemSelectList(int? selected = null)
{
    var systems = await _context.GameSystems.OrderBy(g => g.Name).ToListAsync();
    return new SelectList(systems, "ID", "Name", selected);
}
// ...
ViewData["GameSystemID"] = await GameSystemSelectList(oracle.GameSystemID);
```

```html
<!-- view -->
<select asp-for="GameSystemID" class="form-control" asp-items="ViewBag.GameSystemID"></select>
```

`ViewBag.GameSystemID` and `ViewData["GameSystemID"]` are the same dictionary under two
syntaxes. The tag helper reads the `SelectList` from there and renders the `<option>`s.

**The rule that is easy to miss:** the `SelectList` has to be rebuilt on *every* path that
redisplays the form, and the failure paths are the ones you forget. Both `Create` POST and
`Edit` POST have a `if (!ModelState.IsValid) return View(model);` branch — and if that branch
does not rebuild `ViewData["GameSystemID"]`, the redisplayed form has an **empty dropdown**
and the user's other field errors are now unfixable. The private helper exists so the four
call sites (Create GET, Create POST invalid, Edit GET, Edit POST invalid) cannot drift apart.

### The optional-enum dropdown

`PrimaryOracleType` on the `GameSystems` form is nullable. `Html.GetEnumSelectList<T>()` does
**not** produce a blank option, so "none" needs one written by hand as child content of the
`<select>`:

```html
<select asp-for="PrimaryOracleType" class="form-control"
        asp-items="Html.GetEnumSelectList<OracleSubtype>()">
    <option value="">— None —</option>
</select>
```

The tag helper renders child content first, then appends the `asp-items` options, so `— None —`
sits at the top. Its `value=""` is what posts back as `null` for the nullable property.

---

## 7. The Game Systems CRUD, and two rules of its own

`GameSystemsController` is a plain scaffold-shaped controller — `Index`, `Create`, `Edit`,
`Delete`, each with the GET/POST pair. Only its own dependency, an `ApplicationDbContext`; no
`OracleConfigurator`, no `IWebHostEnvironment`. Two things in it are not boilerplate.

### Unique name: guard *and* constraint

```csharp
if (await _context.GameSystems.AnyAsync(g => g.Name == gameSystem.Name))
    ModelState.AddModelError(nameof(GameSystem.Name),
        "A game system with that name already exists.");
```

The unique index (§3) already refuses a duplicate — but as a `DbUpdateException`, i.e. a 500.
The `AnyAsync` check turns it into a field-level validation message. On `Edit` the check adds
`&& g.ID != gameSystem.ID`, so a system keeping its own name is not reported as colliding
with itself.

### No delete while an oracle points at it

```csharp
if (await _context.Oracles.AnyAsync(o => o.GameSystemID == id))
{
    ViewData["OracleCount"] = await _context.Oracles.CountAsync(o => o.GameSystemID == id);
    ModelState.AddModelError("",
        "That game system is still used by one or more oracles and cannot be deleted.");
    return View(gameSystem);
}
```

The Delete GET passes the referencing-oracle count to the view, which hides the Delete button
and shows a warning when it is non-zero. The POST re-checks anyway — the button being hidden
is not a security boundary. And behind both, `OnDelete(Restrict)` would still stop the delete
at the database if it somehow got that far.

**This is the same two-layer pattern as the unique-name check, and it is deliberate, not
redundant:** the application-level check exists to produce a good message; the database-level
constraint exists to be correct. Removing either one is a bug — drop the guard and users get
500s, drop the constraint and a race between two requests can orphan an oracle.

### Load-then-assign on Edit

```csharp
var existing = await _context.GameSystems.FindAsync(id);
if (existing == null) return NotFound();

existing.Name = gameSystem.Name;
existing.Genre = gameSystem.Genre;
existing.PrimaryOracleType = gameSystem.PrimaryOracleType;
await _context.SaveChangesAsync();
```

Not `_context.Update(gameSystem)`. Every field on this form is bound, so `Update` would
happen to be safe here — but v1.1 §3 lost data to exactly that call once, and the convention
is now "copy onto a loaded row" everywhere, so a property added later that the form does not
post cannot be nulled by the save.

---

## 8. What changed in the test suite

**Status: done. 114 → 137, all green.**

### Support (`Support/`)

| File | Change |
|---|---|
| `TestData.cs` | `SeedOracle` sets `GameSystemID = SeededGameSystemId` (1) instead of the enum. Two `const int` — `SeededGameSystemId` (1, "Dungeons and Dragons") and `OtherSeededGameSystemId` (2, "Cyberpunk Red") — name the `HasData` rows every test DB has. |
| `TestFiles.cs` | `TestExport.Dice` / `Cards` take a `string gameSystem` parameter, default `"Dungeons and Dragons"` — a name that matches a seeded row, so an import resolves rather than creates. |
| `TestControllers.cs` | New `GameSystems(ctx)` factory, same hand-wired `ControllerContext` / `MetadataProvider` shape as `Oracles(ctx)`. |

### Existing tests adjusted for the foreign key

- **`EditActionTests`** — `Posted(...)` takes an `int gameSystemId`; the assertion is now
  `oracle.GameSystemID`, not `oracle.GameSystem`.
- **`ExportActionTests`** — `Export_WritesEnumsAsNames` became
  `Export_WritesSubtypeNameAndGameSystemName`; it asserts `oracle.GameSystem == "Dungeons and
  Dragons"` (the row name), with the subtype still checked as an enum name.
- **`EndToEndTests`** — the round-trip check loads `check.Oracles.Include(o => o.GameSystem)`
  and asserts the name resolved; `CreateOracle` posts `GameSystemID`.

### `OracleImporterTests` — the behaviour that changed

`TryBuild_UnknownGameSystem_ReturnsError` deleted. A private `Import(file, out oracles)`
wrapper passes an empty system set for the tests that predate the new argument. Five tests in
its place:

| Test | Locks in |
|---|---|
| `TryBuild_MissingGameSystem_ReturnsError` (`""`, `"   "`) | Blank name is still rejected. |
| `TryBuild_UnknownGameSystem_CreatesItWithPlaceholderGenre` | Unknown name → new `GameSystem`, `Genre == "Unknown"`, `ID == 0`. |
| `TryBuild_KnownGameSystem_ReusesTheExistingRow` | A matching name → `Assert.Same` the passed-in instance. |
| `TryBuild_GameSystemMatch_IsCaseInsensitive` | `"cyberpunk red"` matches `"Cyberpunk Red"`. |
| `TryBuild_TwoOraclesSameNewSystem_ShareOneInstance` | Both oracles' `GameSystem` is the same object. |

### New — `ImportActionTests` (2) and `GameSystemsControllerTests` (11)

`ImportActionTests` gains `Post_FileWithNewGameSystem_CreatesTheSystemRow` (the row lands, the
oracle links to it) and `Post_FileWithSeededGameSystem_DoesNotCreateADuplicate` (still five
systems afterward).

`GameSystemsControllerTests` covers the controller end to end: `Index` lists the five seeded;
`Create` happy path, no-primary-type, duplicate-rejected; `Edit` all-fields, id-mismatch →
404, rename-onto-existing rejected, keep-own-name allowed; `Delete` GET passes the count,
`DeleteConfirmed` blocked while referenced, `DeleteConfirmed` removed when not.

### The coverage boundary is unchanged

v1.2 §7 stated it and it still holds: **this suite cannot reach Razor markup.** The new views,
the nav tab, the `— None —` option, the hidden Delete button — all verified by loading them in
a browser, and by nothing else. What the suite *does* now cover is every branch of
`GameSystemsController` and the importer's system-resolution logic, because those are C# a
test can call.

The direct-invocation limit from v1.2 §7 / principle 19 applies to the new controller tests
too: they build a `GameSystem` in C# and call the action, so `[Bind]`, `[Required]` and
`[MaxLength]` never run. The tests assert the errors the controller adds *itself* — the
duplicate-name check, the delete guard, the id mismatch — not the annotation-driven ones.

---

## 9. Corrections to v1.0, v1.1 and v1.2

**v1.0 §3, v1.1 §0, v1.2 §0 — the `Oracle` shape.** `Oracle.GameSystem` is no longer an
`enum` stored as a string. It is `int GameSystemID` (a required FK, `[Range(1, int.MaxValue)]`)
plus a `GameSystem?` navigation property. `Models/Enums/GameSystem.cs` is deleted;
`Models/GameSystem.cs` is the entity. `OracleSubtype` and `OracleCategory` in `Models/Enums/`
are untouched.

**v1.0 §3 — `ApplicationDbContext.OnModelCreating`.** Add: a `HasOne/WithMany/HasForeignKey`
for the `Oracle`→`GameSystem` relationship with `OnDelete(DeleteBehavior.Restrict)`; a unique
index on `GameSystem.Name`; a `HasConversion<string>()` on `GameSystem.PrimaryOracleType`;
and a `HasData` block seeding five systems. The `GameSystem` `HasConversion<string>()` that
used to sit on `Oracle` is gone.

**v1.0 §5, v1.1 §7, v1.2 §8 — routes table.** New rows:

| Verb | Route | What it does |
|---|---|---|
| GET | `/GameSystems` | Lists every game system. |
| GET/POST | `/GameSystems/Create` | Add a system — name, genre, optional primary oracle type. |
| GET/POST | `/GameSystems/Edit/5` | Edit those three fields. Load-then-assign. |
| GET/POST | `/GameSystems/Delete/5` | Confirm and delete — blocked, with a count, while any oracle references it. |

**v1.2 §11 — "v1.3, rolling and drawing".** The section is sound; only its number is wrong.
Rolling and drawing is **v1.4**. Read v1.2 §11 as-is before starting it — the analysis of
contiguous bands, where the randomness lives, GET vs POST, and draw-with-replacement is all
still the right first reading. One thing it can now assume: an oracle's game system is a
loadable row, so the play page can show "Weather — Dungeons and Dragons" without another
lookup.

**v1.1 §10 — multi-user notes.** `GameSystems` is a new global table. When accounts arrive
there is a real decision here, not just a filter to add: are game systems **per-user** (my
homebrew is mine) or **shared** (there is one "Dungeons and Dragons" for everyone)? Shared is
defensible and simpler — the seeded five clearly should be. A hybrid (shared seeds, plus
per-user additions) is the likely answer and is more design than plumbing. Add it to the v1.1
§10 list as an open question, not a known change.

**v1.2 §6 / principle 18 — "consistency is not uniformity".** Confirmed again this pass, both
directions. `GameSystems/Edit.cshtml` was handed over *missing* the `@section Scripts` block
that `Create.cshtml` has and needs — a real omission, caught on review.
`GameSystems/Delete.cshtml` correctly has **no** such block: its only form is a hidden `ID`
and a submit button, and there is nothing for client-side validation to check. Same principle,
opposite calls: match the sibling's *purpose*.

---

## 10. Principles this pass added

Continuing v1.2 §9, which ended at 21.

22. **Deleting a widely-used type is a red-build operation by nature.** You cannot stage the
    fixes — the name clash means the old and new type cannot coexist in scope. Delete first
    so the errors say "missing" not "ambiguous", then work the compiler's list to zero. This
    is not a process failure; it is the shape of the change.
23. **The compiler flags the sites that break, not the sites that matter.** When a new type
    slots in where an old one was, some consumers compile unchanged and are now wrong
    (`entity.ToString()` returning a class name). Change every site the design touches, then
    let the build confirm you missed none — not the other way round.
24. **An `int` foreign key has no "unset" state.** It defaults to `0`, which is not a valid
    key. `[Required]` is meaningless on it; `[Range(1, int.MaxValue)]` is the check that keeps
    a blank dropdown out of the database.
25. **A required EF relationship cascades on delete by default.** If deleting the principal
    should *not* delete the dependents, say `OnDelete(DeleteBehavior.Restrict)` explicitly.
    The default is a data-loss footgun that produces no warning.
26. **App-level guard for the message, database constraint for the guarantee — keep both.**
    They are not redundant. The guard turns a 500 into a sentence; the constraint is what is
    actually true under a race. Removing either is a bug.
27. **When a pure function needs data it cannot see, pass the data in.** `TryBuild` needed the
    existing game systems; it took an `IEnumerable<GameSystem>` parameter and stayed pure. It
    did not take a `DbContext`. The caller, which is already stateful, does the loading.
28. **A `SelectList` must be rebuilt on every path that redisplays its form.** The
    validation-failure branch is the one you forget, and its symptom — an empty dropdown on a
    form the user is trying to fix — is worse than the error they were already seeing. A
    single private helper for all call sites is the cheap insurance.

---

## 11. Still open

Carried forward, unchanged:

- **The roll/draw play page — now v1.4.** The whole point of an oracle, still unbuilt. v1.2
  §11 is its brief.
- **Multi-user.** v1.1 §10, plus the new per-user-vs-shared question for `GameSystems` (§9).
- **Parking lot: importing from `App_Data/Exports`** server-side rather than by upload. Needs
  a path-traversal guard.
- **Deferred from v1.0:** tarot reversed, partial decks, weighted outcomes, free-form ranges,
  sub-tables, sharing, entries-form pagination.

Noted this pass, deliberately not acted on:

- **`PrimaryOracleType` does nothing yet.** It is stored and editable, and no code reads it.
  The obvious use is to pre-select the subtype on the "create oracle" form once a game system
  is chosen — a small nicety for v1.4 or later, not a gap.
- **`Genre` is unnormalised free-form.** "Sci-fi", "Science Fiction" and "sci fi" are three
  genres as far as the database is concerned. Fine while one person is typing; a `<datalist>`
  of existing genres would help if that changes.
- **`PrimaryOracleType` column is `nvarchar(max)`.** No length was configured on the
  conversion. Cosmetic; a `HasMaxLength(20)` and a migration would tidy it and is not worth
  doing alone.
- **The Bootstrap 5 form spacing.** `.form-group` is an inert class in Bootstrap 5 (it added
  `margin-bottom: 1rem` in v4). The scaffolded forms are tighter than the template intended.
  Fixed locally with `mt-4` on the submit button's wrapper; the thorough fix is `mb-3` on
  every field group, across all the Create/Edit views.

---

## 12. Looking ahead — v1.4

**Status: not started.** v1.4 is what v1.2 §11 already describes: roll the die or draw the
card, get an outcome. Read that section first — none of it changed.

What this pass adds to the picture:

- The play page can title itself **"Weather — Dungeons and Dragons"** with one extra
  `.Include(o => o.GameSystem)` on whatever query loads the oracle.
- If the oracle-picker on the play page groups or filters by game system, the relationship is
  there for it now.
- `PrimaryOracleType` (§11) could seed the subtype when creating an oracle for a system —
  worth a moment's thought during v1.4, since the create flow will be under the microscope
  anyway.

The one thing v1.2 §11 flags as "fix on the way in" is still outstanding and still cheap:
`NeedsSetup` lives on `OracleListItem` and the roll page needs the same question answered
about a loaded `Oracle`. Promote it to an extension method beside `Category()`, `FaceCount()`
and `KeyLabel()` **before** building the roll page, while there is one caller.

---

## Appendix — new gotchas

Append to the tables in v1.0, v1.1 and v1.2.

| Symptom | Cause | Fix |
|---|---|---|
| Deleting one `enum` produces dozens of build errors across unrelated files | An `enum` used as a property type, a generic argument and a view helper argument has a reference at every one of those sites; a replacement type with the same name cannot coexist with it | Expected. Delete the old type first (errors read "missing", not "ambiguous"), then convert consumers until the error count is 0. The build only goes green at the end |
| A consumer of a deleted type compiles unchanged but now misbehaves | The replacement type is legal where the old one was — `entity.ToString()`, an assignment, a property declaration — so the compiler has nothing to flag | Change every site the design touches, not only the flagged ones. `ToString()` on an entity returns its class name |
| `DbUpdateException` / a 500 when saving a form with a `<select>` for a foreign key | An unselected `<select>` posts `0`; a non-nullable `int` FK accepts it into `ModelState` and it fails at the database as an invalid key | `[Range(1, int.MaxValue)]` on the FK property. `[Required]` does nothing on an `int` |
| Deleting a parent row deletes child rows with no prompt and no error | A required EF relationship defaults to `DeleteBehavior.Cascade` | `.OnDelete(DeleteBehavior.Restrict)` in `OnModelCreating`. Add an app-level `AnyAsync` check too, for the message |
| A form redisplayed after a validation error has an empty dropdown | The `SelectList` in `ViewData` was built in the GET and on the success path, but not rebuilt in the `if (!ModelState.IsValid)` branch | Rebuild it on every path that calls `return View(model)`. A private helper method so the call sites cannot drift |
| A nullable-enum `<select>` has no "none" option | `Html.GetEnumSelectList<T>()` emits one option per enum member and nothing else | Add `<option value="">— None —</option>` as child content of the `<select>`; it renders before the `asp-items` options and its empty value binds as `null` |
| A class is in a file whose name does not match it, and nothing complains | C# does not tie a type's name to its filename (that is a convention the IDE scaffolder follows, not a rule) | Compiles fine. Rename the file anyway — the next person will look for `GameSystemsController` in `GameSystemsController.cs` |
| The app opens in a small, mobile-sized browser window after switching run profiles | Not a code change. Switching from IIS Express to a Kestrel (`http`/`https`) profile makes Visual Studio open a fresh browser window, and Chrome/Edge size a new window to match the last one closed | Maximise once and fully close the browser so the size sticks. Also check DevTools device-emulation is off (Ctrl+Shift+M) |
| Scaffolded form fields sit too close together; the submit button especially | `.form-group` is an inert class in Bootstrap 5 — it carried `margin-bottom: 1rem` in v4 and was kept only for compatibility | Bootstrap 5 spacing utilities: `mb-3` on each field group, or `mt-4` on the button's wrapper for a targeted fix |
