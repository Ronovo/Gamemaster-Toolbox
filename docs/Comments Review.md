# Comments Review

> **Status: all findings applied, 2026-09-04.** Every item below was actioned in the same
> session this was written. The document is kept as the record of *why* each change was
> made — read it as history, not as an open list. One deviation is noted at §1.1.

An audit of every comment in the app and test projects, 2026-09-04. Migrations and
`bin`/`obj` excluded (generated). Ordered by the five categories asked for, worst first
within each.

**Headline:** the codebase is not over-commented overall — it is *unevenly* commented. The
comment budget is spent heavily on things the code already says (route verbs, property
names) and thinly on the three or four places where a reader could genuinely get it wrong.
Two comments are actively false. One of those has been false for several passes.

Comment density, for context:

| File | Comment lines | Total lines |
| --- | --- | --- |
| `Controllers/OraclesController.cs` | 69 | 463 |
| `Program.cs` | 30 | 118 |
| `Services/OracleImporter.cs` | 12 | 141 |
| `Data/ApplicationDbContext.cs` | 9 | 49 |
| `Controllers/GameSystemsController.cs` | 7 (all route markers) | 119 |

That last row is the shape of the problem: the file with two hand-written business rules
and a past-bug-avoidance pattern in it has **zero** explanatory comments.

---

## 1. Dead comments

### 1.1 `Services/OracleImporter.cs:13-14` — describes behaviour that does not exist

```csharp
/// var imported is there to catch any tables that are imported when an error is hit,
/// so that even if one oracle is bad, the partial import can still happen.
```

**This is false, and it contradicts a deliberate design decision.** Every error path in
`TryBuild` returns *before* line 76's `oracles = imported;`, so on any error the caller
receives the empty list assigned at line 22. There is no partial import — and there must
not be, because import is all-or-nothing on purpose (one `AddRange`, one `SaveChanges`) so
that Clear All remains a safe backup step.

A reader who believes this comment will think a bad file half-imports, which is the
opposite of the guarantee the feature makes.

It is also misplaced: it documents a private local variable in the *public XML summary* of
the type.

**Fix:** delete the two lines. If the all-or-nothing contract is worth stating — it is —
state that instead:

```csharp
/// All-or-nothing: any validation failure returns an error and leaves
/// oracles empty. Nothing is partially built.
```

**Deviation when applying this.** The review originally suggested collapsing `imported`
into `oracles`, on the grounds that two names for one list is what confused the comment.
That suggestion was wrong and was not carried out: adding straight to the `out` parameter
would hand the caller a half-built graph on every error path — creating the exact partial
import the false comment described. The two lists are load-bearing. They now carry a
comment saying so.

### 1.2 `GM Toolbox.Tests/EndToEndTests.cs:116` — refers to seed data that no longer exists

```csharp
// The file carried the system by name; import resolved it to the seeded row.
```

There is no seeded row. The `HasData` seed was removed in the SQLite pass and the five
fixture systems moved into `TestDb`'s constructor.

**Fix:** "…resolved it to the existing fixture row."

### 1.3 `Models/Oracle.cs:18` — mislabels what the property holds

```csharp
// Oracle Type (e.g. Dice, Card)
[Display(Name="Oracle Type")]
public OracleSubtype OracleSubtype { get; set; }
```

`Dice` and `Card` are `OracleCategory` values. This property holds an `OracleSubtype` —
`D4`…`D100`, `PlayingCards`, `TarotCards`. The comment names the wrong type's values, and
the subtype/category distinction is the single most load-bearing idea in the model.

**Fix:** delete it (the `[Display]` attribute already carries the label), or correct it to
`// D4-D100, PlayingCards or TarotCards. Category() derives Dice vs Cards from this.`

### 1.4 `Controllers/OraclesController.cs:341` — imprecise destination

```csharp
//Do a server side copy to the content root
```

It copies to `App_Data/Exports` *under* the content root, not to the content root. Minor,
but it is the sort of thing someone greps for when a file lands somewhere unexpected.

**Fix:** `// Keep a server-side copy under App_Data/Exports as well as sending the download.`

---

## 2. Leftover TODOs

**There are none to remove.** The full inventory of markers in the app and test projects:

| Location | Marker | Verdict |
| --- | --- | --- |
| `OraclesController.cs:368` | `TODO FOR MULTI-USER:` | Keep — in scope for the multi-user work |
| `OraclesController.cs:433` | `MULTI-USER:` | Keep — same |
| `Models/Transfer/OracleExportFile.cs:24` | `MULTI-USER:` | Keep — same |
| `OraclesController.cs:174` | `// NOTE:` | Not a TODO; see §3.2 |

No `FIXME`, no `HACK`, no `XXX`, no stale "temporary" markers anywhere. Nothing rotted here.

One correctness note on the one you are keeping — `OraclesController.cs:368-369` says
"drop the reseed entirely." That advice is still right under multi-user, but the reseed it
refers to is no longer `DBCC CHECKIDENT`; it is now the `sqlite_sequence` delete. The
comment reads fine as written, so this is a "still true, worth knowing" note rather than a
fix.

---

## 3. Over-explaining and verbose comments

### 3.1 The 16 scaffold route markers — `OraclesController.cs`, 7 more in `GameSystemsController.cs`

```csharp
// Serves the Create View for a new Oracle. The form will POST back to the same action.
// GET: ORACLES/Create
public async Task<IActionResult> Create()
```

Twenty-three comments across the two controllers restate what the attribute, the method
name and the routing convention already say. `// GET: GameSystems/Edit/5` above
`public async Task<IActionResult> Edit(int? id)` adds nothing a reader cannot see.

This is the single biggest block of low-value comment in the codebase, and it is scaffold
residue rather than anything anyone chose.

**Recommendation:** delete the pure `// GET: …` / `// POST: …` lines. Keep the prose lines
above them *where the prose says something non-obvious* — `Configure`'s "creates or deletes
entries to match the requested outcome count" earns its place; `Details`'s "Get Details of
a specific Oracle" does not.

Rough cut: ~23 lines removed, ~6 prose lines kept.

### 3.2 `OraclesController.cs:174-176` — says the same thing twice

```csharp
// NOTE:
// Action Name added since the Post Shares a signature with the Get (Delete(int? id))
// Method renamed to DeleteConfirmed to avoid ambiguity
```

Three lines, two of which are the same fact stated twice, and a bare `// NOTE:` header
that carries no information. Worth keeping *one* line, because the `[ActionName]` trick is
genuinely non-obvious to someone new to MVC.

**Fix:**
```csharp
// Renamed because the POST would otherwise share a signature with the GET;
// [ActionName] keeps the route as Delete.
```

Note the identical pattern in `GameSystemsController.DeleteConfirmed` has **no** comment.
Whichever way you go, make the two files agree.

### 3.3 `Program.cs:31-41` — the WAL comment, 11 lines

This one encodes a real bug found the hard way, so it should survive — but at less than
half the length. It currently explains what WAL is, what it is good for, what breaks, why
it breaks, and what the README says.

**Fix:** keep the failure and the consequence, drop the tutorial.

```csharp
// Fold the write-ahead log back into the .db on the way out.
//
// EF enables WAL journalling, so recent writes live in a gmtoolbox.db-wal sidecar.
// Copying only the .db after an un-checkpointed run yields an EMPTY toolbox — which
// breaks the one thing this app is built around. A killed process cannot be helped
// here; the -wal must then travel with the .db (see README).
```

### 3.4 `Data/ApplicationDbContext.cs:23-28` — the NOCASE comment, 6 lines

Same shape: correct, valuable, twice as long as it needs to be. It also frames the rule
historically ("keeps SQL Server's old behaviour"), which will read as noise to anyone who
never saw the SQL Server version — increasingly, everyone.

**Fix:** state the rule, not the history.

```csharp
// Game system names are case-insensitive. SQLite compares TEXT binary, so without
// NOCASE "Cyberpunk Red" and "cyberpunk red" become two rows — past the unique index
// below and past GameSystemsController's duplicate check — while OracleImporter's
// OrdinalIgnoreCase lookup still treats them as one.
```

### 3.5 `GM Toolbox.Tests/EndToEndTests.cs:124-131` — an 8-line changelog inside a test

```csharp
// It is 1 because Clear All resets the AUTOINCREMENT counter. This assertion
// used to be NotEqual(weatherId, ...) and passed only because the reseed was
// SQL-Server-only and silently no-opped under the SQLite these tests run on.
// Now that the app is SQLite too, the reseed genuinely runs.
```

A test should say what is true now. What the assertion *used to be* and why it was wrong is
history, and it is already recorded in `oracle-builder-study-guide-v1.4.md` §4.

**Fix:** keep the first three lines (the row is new, IDs are not carried in the file), then
one line: `// It is 1 because Clear All resets the AUTOINCREMENT counter.` Drop the rest.

### 3.6 `GM Toolbox.Tests/GameSystemsControllerTests.cs:88-96` — 8-line doc on a 3-case theory

Same pattern as 3.4: the rationale is sound but restates the full SQL-Server-versus-SQLite
story that already lives in `ApplicationDbContext` and the study guide. Three lines will
hold it. Once §3.4 is trimmed, this one can simply point at the model configuration rather
than re-derive the argument.

### 3.7 `Models/Oracle.cs:8-18` — four comments restating property names

```csharp
// The Autoincrementing ID
public int ID { get; set; }
// User Assigned Name
[Required, MaxLength(100)]
public string Name { get; set; } = string.Empty;
```

`// User Assigned Name` above `Name` is pure restatement. `// The Autoincrementing ID`
above `ID` likewise. `// Game System ID (Foreign Key to Game Systems Table)` is the only
one carrying information, and the `GameSystemID`/`GameSystem` pair already implies it.

**Fix:** delete all four. See §5.4 for what is worth saying in this file instead.

**Unrelated but adjacent:** lines 22-28 hold an empty `public Oracle() { }` and three blank
lines. EF does not need it and nothing calls it. Worth removing while the file is open.

---

## 4. Conversational comments

Few of these, and none egregious. Listed for completeness.

| Location | Comment | Issue |
| --- | --- | --- |
| `OraclesController.cs:69` | `// The post for the Create Oracle View Above` | "Above" is a positional reference that breaks the moment anything is reordered. Same pattern at lines 107, 204, 259. |
| `OraclesController.cs:107` | `// See note on GET call above` | Cross-reference to a comment rather than a statement of fact. Worse, the note it points to describes POST behaviour (see §5.3). |
| `Models/Transfer/OracleExportFile.cs:19` | `// DTO = Data Transfer Object` | A glossary aside for a learner, sitting at the top of a summary that otherwise carries real design rationale. Harmless, but it dilutes the paragraph that follows it. Consider moving it to the study guide. |
| `Program.cs:37` | `…they get an EMPTY toolbox` | The shouting caps read as speaking to the reader rather than describing the system. Lowercase it. |
| `OraclesController.cs:445-447` | `//*********` banner around `// Private helper methods` | Decorative divider for a two-method section. The test project uses a lighter `// ---- helpers` style; pick one and use it in both. |

---

## 5. Where comments are missing

These are the places a reader can actually get it wrong. Higher value than everything
removed above.

### 5.1 `Models/Enums/OracleSubtype.cs` — the numbering looks like a bug

```csharp
D4 = 1,
D6 = 0,
D8 = 2,
```

`D6 = 0` and `D4 = 1` is the first thing anyone notices and it currently looks like a typo.
The values are safe to leave alone *because* the enum is persisted as a string via
`HasConversion<string>()` — but that is exactly the reasoning a comment should carry, since
the alternative reading is "someone should tidy this up," and tidying it up would change
the default value of an unset `OracleSubtype` from `D6` to `D4`.

**Suggested:**
```csharp
// Stored as a string via HasConversion<string>(), so these numbers never reach the
// database and the gap is harmless. Do not renumber anyway: 0 is the value an unset
// OracleSubtype takes, and D6 is the sensible default for that.
```

### 5.2 `Models/Enums/OracleSubtypeExtension.cs` — `FaceCount()` throws, undocumented

`FaceCount()` throws `InvalidOperationException` for `PlayingCards` and `TarotCards`. Every
caller in the app guards on `Category()` first, and that contract is written down nowhere.
This is a public method on a public static class with a landmine in it.

**Suggested:** one XML summary noting card subtypes have no face count, and that callers
must check `Category()` first. `Category()` deserves a one-liner too, saying it is the
single source of truth for the dice/cards split.

### 5.3 `Controllers/OraclesController.cs:85-87` and `112-150` — the explanation is on the wrong action

The GET action carries this:

```csharp
// Allows you to edit the Oracle's Name, GameSystem, and OracleSubtype.
// If the subtype is changed, all existing entries will be deleted
// and you will be redirected to the Configure view to set up new entries.
```

The GET does none of that — it renders a form. The destructive behaviour is in the POST,
which says only "See note on GET call above."

Worse, the POST contains a genuinely important pattern with no comment at all: it does
**load-then-assign** (line 125-135) rather than `_context.Update()` on the bound object.
That is not a style preference — `_context.Update()` on the detached bound object silently
nulled `DiceSize` on every save and cost a real debugging session. Nothing in the file says
so, and the obvious "simplification" is to undo it.

Worth stating precisely, because the comment should not overclaim: the `[Bind]` list
(`ID,Name,OracleSubtype,GameSystemID`) currently covers *every* scalar on `Oracle`, so
`Update()` would not lose data today. The danger is latent, and it arms itself the moment
someone adds a property without touching `[Bind]` — which is exactly how `DiceSize` was
lost. A comment is what stops that being a silent trap.

**Suggested:** move the subtype-change paragraph down to the POST, and add:
```csharp
// Load-then-assign, never _context.Update() on the bound object. Update() marks every
// scalar modified, so any property missing from the [Bind] list above gets written back
// as its default. The list happens to be complete today; it silently stops being complete
// the first time someone adds a property and forgets. (This is how DiceSize was lost.)
```

### 5.4 `Models/OracleEntry.cs` — the central invariant is implied, never stated

The file has `// Dice Keying (Null on Card Oracles)` and `// Card Keying (Null on Dice
Oracles)`, which are good and should stay. What is missing is the rule above them: **exactly
one half is populated, and which one is decided by the parent oracle's subtype, not by
anything on this row.** `OracleEntryDto` states this ("nullable because exactly one of them
applies"); the entity it mirrors does not.

**Suggested:** a short class-level summary saying so, and noting that `KeyLabel()` is how
the two halves get rendered as one column.

### 5.5 `Controllers/GameSystemsController.cs` — two business rules, zero comments

The file has seven route markers and nothing else. Two things need saying:

- **The duplicate-name check** (lines 35, 67) is one half of a pair. The other half is the
  unique index in `ApplicationDbContext`. The app-level check exists to produce a sentence
  instead of a 500; the index is what is actually true under a race. Neither is redundant,
  and someone tidying up will be tempted to delete one.
- **The delete guard** (line 108) likewise pairs with `OnDelete(DeleteBehavior.Restrict)`
  on the FK. Same reasoning, same temptation.

`Edit` also uses load-then-assign here, for the same reason as §5.3, and says nothing about
it.

### 5.6 `Controllers/OraclesController.cs:28-44` — the projection is deliberate

`Index` projects into `OracleListItem` rather than using `.Include`, and computes
`NeedsSetup` on the materialised view model. That is not an arbitrary choice:
`NeedsSetup` calls `Category()`, which cannot be translated to SQL. Pull `NeedsSetup` into
the LINQ query and it fails at runtime, not compile time.

**Suggested:** one line above the projection saying the counts come back raw because
`NeedsSetup` has to be computed in memory.

### 5.7 `Controllers/OraclesController.cs:303-318` — the band derivation is uncommented

```csharp
entry.RangeEnd = (i < vm.Rows.Count - 1)
    ? vm.Rows[i + 1].StartsAt - 1
    : faceCount;
```

This is the heart of the dice feature — each band ends one before the next begins, and the
last runs to the die's face count — and it is the only place that rule is expressed. It
reads as an off-by-one waiting to be "fixed."

**Suggested:** `// Bands are derived, not entered: each ends one before the next starts, and the last runs to the die's face count.`

### 5.8 `Models/GameSystem.cs:16-17` — a property nothing reads

`PrimaryOracleType` is stored, editable in the UI, and read by no code. That is fine as a
staging post for a future feature, but undocumented it looks like a wiring bug.

**Suggested:** `// Stored and editable, but nothing reads it yet — intended as the default subtype when creating an oracle for this system.`

---

## Suggested order of work

1. **§1.1 and §1.2** — the two false comments. Small, and one of them actively misleads
   about a data-safety guarantee.
2. **§5.3 and §5.5** — the load-then-assign sites. These protect against re-introducing a
   bug that has already happened once.
3. **§3.1** — delete the scaffold route markers. Largest volume, lowest risk, and it makes
   the remaining prose comments visible instead of buried.
4. **§5.1, §5.2, §5.6, §5.7** — the four traps that currently look like mistakes.
5. **§3.3-3.7 and §4** — trimming. Cosmetic; do it whenever the files are open anyway.

Net effect is roughly forty fewer comment lines and about a dozen new ones, concentrated
where the code is genuinely surprising.
