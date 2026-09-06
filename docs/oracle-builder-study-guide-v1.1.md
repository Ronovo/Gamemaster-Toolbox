# Oracle Builder — Study Guide v1.1

**A follow-up to [`oracle-builder-study-guide.md`](./oracle-builder-study-guide.md) (v1.0, 2026-09-01/02).**
v1.0 is left unchanged as the record of how the feature was built. This document is the
polish pass on top of it: what we found wrong with the original plan on 2026-09-02
(evening), what we changed, and why.

It covers two batches of work. Sections 2 and 3 are the correctness pass — deleting a
column that should never have existed, and fixing the action that had been quietly
nulling it. Sections 4 and 5 are the feature work that followed: portable export and
import, a Clear All that resets the database, and the list-page changes that make an
unfinished oracle visible.

Read v1.0 first if you haven't. Everything here assumes it.

---

## 0. Catch-up — the parts of v1.0 that matter here

Four decisions from the original build are the ones this pass revisits.

**An oracle is a header plus one row per outcome.** `Oracle` holds the name, game
system and `OracleSubtype`; `OracleEntry` holds one row for each possible result. A
`d6` oracle has 6 entry rows, a tarot oracle has 78. (v1.0 §2, §3)

**Store the specific thing, derive the general thing.** v1.0 §2 set this rule
explicitly for the type: `OracleSubtype` (`D4`…`D100`, `PlayingCards`, `TarotCards`) is
what gets written to the database, and `OracleCategory` (`Dice`/`Cards`) is *computed*
from it by an extension method. The reason given was that stored-plus-derived can never
contradict each other if only one of them is stored. Hold on to that sentence — this
whole pass is about the one place we broke it.

**`FaceCount()` is an extension method on the subtype.** `OracleSubtype.D6.FaceCount()`
returns 6. It throws for card subtypes, so every caller guards with
`Category() == OracleCategory.Dice` first. (v1.0 §4)

**Diff-and-keep.** Re-running `Configure` on an existing oracle reconciles the entry set
instead of wiping it, so text the user already typed survives. Dice match by position,
cards match by `CardCode`. This lives in `OracleConfigurator.Reconcile`. (v1.0 §7, §8)

And one loose end v1.0 recorded but did not close, at the end of §9:

> The stale `DiceSize` column is now harmless for validation.

"Harmless" was doing a lot of work in that sentence. It is the thread we pull on below.

---

## 1. The theme of this pass: one source of truth

v1.0 §3 gave `Oracle` a `DiceSize` column — `int?`, the die's face count, `null` for
card oracles. It looked harmless. It is the same value `OracleSubtype.FaceCount()`
already computes, stored a second time in a second place.

That is exactly the arrangement v1.0 §2 rejected for `OracleCategory`. We applied the
rule to the category and forgot it for the size. Two copies of one fact will eventually
disagree, and in §9 they did: validation read the stored copy, the stored copy was
`null`, and a `d4` accepted a roll band starting at 9.

v1.0 fixed the *symptom* — read `FaceCount()` at the point of use — and left the
duplicate column in place. v1.1 removes the duplicate. **When a bug is caused by two
copies of one fact, deleting a copy is a better fix than keeping them in sync.**

---

## 2. Change 1 — `DiceSize` is deleted

**Status: done.** Migration `20260902104042_v1.1RemoveDiceSize` applied.

### The diagnosis, which was not what v1.0 assumed

v1.0 §9 blamed the null `DiceSize` on "old data, or an `Edit` that changed the subtype
without updating `DiceSize`." Both guesses were wrong. The real cause is in the `Edit`
POST action, and it fires on *every single save*.

```csharp
public async Task<IActionResult> Edit(int? id, [Bind("ID,Name,OracleSubtype,GameSystem")] Oracle oracle)
{
    ...
    _context.Update(oracle);
    await _context.SaveChangesAsync();
```

Two things combine:

1. `oracle` is not the row from the database. Model binding built a **brand-new,
   detached `Oracle` object** out of the posted form fields. The `[Bind(...)]` list names
   four properties, so those four get values and everything else is left at its default.
   `DiceSize` is `int?`, so it is `null`.

2. `_context.Update(entity)` attaches that object and marks **every scalar property as
   `Modified`**. EF has no way to know which properties the form actually carried, so it
   assumes all of them. The generated `UPDATE` therefore includes `DiceSize = NULL`.

So editing an oracle's *name* silently wiped its die size. Not legacy data — a live bug
on the happy path, and one the tests never caught because no test called `Edit`.

**The general lesson:** `_context.Update()` on a detached object writes the *whole*
entity. Anything the bind list leaves out gets written as its default. A partial bind
list plus `Update()` is a data-loss bug waiting for a column to be added.

### The change

`DiceSize` held nothing that `OracleSubtype` doesn't already imply, so it was deleted
outright rather than repaired.

| File | Change |
|---|---|
| `Views/Oracles/Configure.cshtml` | Local `var faceCount = Model.OracleSubtype.FaceCount();` inside the `Dice` case; the two `Model.DiceSize` uses now read `faceCount`. |
| `Models/ViewModels/EditEntriesViewModel.cs` | `int? DiceSize` → `int FaceCount`. |
| `Controllers/OraclesController.cs` (`EditEntries` GET) | `FaceCount = isDice ? oracle.OracleSubtype.FaceCount() : 0`. |
| `Controllers/OraclesController.cs` (`EditEntries` POST) | Re-show block sets `vm.FaceCount = faceCount` — the local the validation already computed. |
| `Views/Oracles/EditEntries.cshtml` | `max="@Model.FaceCount"`. |
| `Controllers/OraclesController.cs` (`Create` POST) | The `if (…Category() == Dice) oracle.DiceSize = …` block deleted. |
| `Models/Oracle.cs` | Property deleted. |
| Migration | `Add-Migration RemoveDiceSize` → `Update-Database`. |

### Build order runs backwards when you delete

v1.0 §10 rule 2 says build in dependency order: leaf types first, then the code that uses
them. **Removing** a member inverts that. The property is the leaf everything else
depends on, so it goes *last* — every consumer has to stop referencing it before it can
disappear, or the project won't compile.

That ordering isn't optional here, because of a gotcha already in v1.0 §3: EF tooling
builds the project before it will generate a migration. A half-finished deletion means
`Add-Migration` fails on a compiler error rather than on anything to do with EF.

### `int`, not `int?`, on the view model

`FaceCount` is a plain `int`. v1.0's appendix warns that a non-nullable property on a
bound model is implicitly required and will fail validation with "The X field is
required" — but that rule applies only to non-nullable **reference** types under nullable
reference types. Value types like `int` are unaffected, which is why `EntryRow.StartsAt`
was already a plain `int`. The `string?` rule from v1.0 still stands for `Name` and
`Label`.

---

## 3. Change 2 — `Edit` reconciles when the subtype changes

**Status: done.**

The second half of the same bug. Nothing in `Edit` ever looked at whether `OracleSubtype`
changed, so you could turn a 78-row tarot oracle into a `d6` and keep all 78 rows, each
still carrying a `CardCode` and a card name.

### The rewritten action

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Edit(int? id, [Bind("ID,Name,OracleSubtype,GameSystem")] Oracle oracle)
{
    if (id != oracle.ID)
    {
        return NotFound();
    }

    if (!ModelState.IsValid)
    {
        return View(oracle);
    }

    var existing = await LoadOracleWithEntries(id);
    if (existing == null)
    {
        return NotFound();
    }

    bool subtypeChanged = existing.OracleSubtype != oracle.OracleSubtype;

    existing.Name = oracle.Name;
    existing.GameSystem = oracle.GameSystem;
    existing.OracleSubtype = oracle.OracleSubtype;

    if (subtypeChanged)
    {
        _context.OracleEntries.RemoveRange(existing.OracleEntries.ToList());
    }

    await _context.SaveChangesAsync();

    if (subtypeChanged)
    {
        return RedirectToAction(nameof(Configure), new { id = existing.ID });
    }

    return RedirectToAction(nameof(Index));
}
```

### Load-then-assign

The posted object is now used only as a **carrier of the submitted values**. The row we
actually modify is `existing`, loaded from the database and therefore tracked by EF's
change tracker. Assigning to its properties is enough — `SaveChangesAsync` compares
against the values EF remembers loading and writes only the columns that really moved.

This is the standard EF edit pattern, and it is the structural fix for §2's diagnosis: a
column the bind list doesn't mention is simply never touched, instead of being
overwritten with a default.

### The disappearing `try/catch`

The old action wrapped its save in `catch (DbUpdateConcurrencyException)`. That existed
for one case: the row was deleted between the GET and the POST. With the detached
`Update()`, EF only discovered that when its `UPDATE` affected zero rows, and threw.

Loading the row up front turns the same case into `existing == null` → `NotFound()`. The
catch is now unreachable, so it goes. **Moving a load earlier can make an exception
handler dead code — when you restructure, check whether the handler still has a path to
it.**

It took a helper with it. The scaffolder's `private bool OracleExists(int? id)` existed
for one caller — the `if (!OracleExists(oracle.ID))` inside that catch — and was orphaned
the moment the catch went. It sat unreferenced for a while afterwards because **C# does
not warn about an unused private method**: unused private *fields* get a warning,
unreachable *statements* get a warning, but a method nobody calls compiles clean forever.
**After deleting a caller, go looking for what it was the only caller of.** The compiler
will not do it for you.

### `.ToList()` is load-bearing

```csharp
_context.OracleEntries.RemoveRange(existing.OracleEntries.ToList());
```

Marking entries `Deleted` makes EF's change tracker run *fixup*, which removes them from
the parent's navigation collection. Passing `existing.OracleEntries` directly means
iterating the very collection being mutated — `InvalidOperationException: Collection was
modified`. `.ToList()` snapshots it first.

### Why clear, and not diff-and-keep

Diff-and-keep is the right behaviour for `Configure`, where the oracle's *type* is fixed
and only the row count moves. A subtype change is a different event, and reconciling
across it breaks in a way that is worse than losing text:

- **Dice → Cards** self-heals by luck. `ReconcileCards` deletes any entry whose
  `CardCode` isn't in the target deck, and dice rows have a null `CardCode`, so they all
  go.
- **Cards → Dice** does not. `ReconcileDice` trims by *position*, so asking for 6
  outcomes keeps the first 6 rows — still carrying `CardCode` and `CardLabel`, still with
  no ranges. `EditEntries` would render a d6 whose outcomes are labelled "Ace of Wands",
  "Two of Wands"…
- **D20 → D6** leaves 20 rows on a 6-face die, some with `RangeStart` above the new
  ceiling.

One rule covers all three: **subtype changed → entries are cleared, and you land on
`Configure` to rebuild.** Simple beats clever when the clever version is only correct in
one direction.

### The step that makes it honest

Wiping a user's outcomes with no warning is a trap. `Views/Oracles/Edit.cshtml` gets a
line under the subtype `<select>`:

```cshtml
<small class="text-muted">Changing the type clears this oracle's outcomes and sends you back to Configure.</small>
```

v1.0 §10 rule 4 said navigation is part of the feature. The same applies to a destructive
side effect: the change isn't finished until the UI tells the user it is going to happen.

---

## 4. Change 3 — export, import and Clear All

**Status: done.**

Up to this point an oracle could only ever exist inside one database. This adds a
portable file format, a way back in from it, and a reset.

### The format, and why it is not the entities

The obvious shortcut is to serialise `Oracle` straight to JSON. It is the wrong move for
the same reason `EditEntriesViewModel` exists (v1.0 §6): it welds the file format to the
database schema, so renaming a column silently breaks every file anyone already exported.
Three DTOs in `Models/Transfer/OracleExportFile.cs` describe the file instead.

```json
{
  "formatVersion": 1,
  "exportedUtc": "2026-09-02T13:34:08.69Z",
  "oracles": [
    {
      "name": "Weather",
      "gameSystem": "DungeonsAndDragons",
      "subtype": "D6",
      "entries": [
        { "sortOrder": 1, "result": "Clear skies", "rangeStart": 1, "rangeEnd": 3 }
      ]
    }
  ]
}
```

Four decisions are baked into that shape.

**No database IDs.** An `ID` is meaningful only inside the database that issued it.
Carrying one makes the file non-portable and makes import ambiguous about whether it is
creating or updating. Import always creates.

**Enums as names, not numbers.** `"D6"`, not `1` — matching what the database already
stores via `HasConversion<string>()`. Reordering an enum later cannot silently
reinterpret an old file.

**`formatVersion`.** One integer now; the only thing that will let a future import read
today's files after the shape changes.

**Nulls omitted on write.** A dice entry carries no empty `cardCode`; a card entry
carries no empty `rangeStart`. The two halves of `OracleEntry` stay visibly separate in
the file.

`Services/OracleJson.cs` holds one `static readonly JsonSerializerOptions` used by both
directions. That sharing is not tidiness — if export wrote camelCase and import expected
PascalCase, every round trip would silently produce empty objects rather than an error.

### Export

`OracleExporter.Build` is pure: it takes loaded oracles and returns the file shape, with
no `DbContext`, exactly like `DeckFactory`. The action loads, serialises, writes a
server-side copy, and returns a download.

Two things in it are easy to get wrong.

**`.Include(o => o.OracleEntries)` is load-bearing.** Without it every oracle exports with
an empty `entries` array — a perfectly valid-looking file containing none of the data, and
no error anywhere. `Export_IncludesTheEntriesOfEachOracle` exists for that one line.

**`System.IO.File` must be fully qualified.** Inside a controller, `File` already means
`ControllerBase.File(...)` — the method used on the very next line to return the download.
`File.WriteAllBytesAsync` produces a confusing error about a method group.

The server-side copy goes to `App_Data/Exports` under the content root. Deliberately not
`wwwroot`: everything under `wwwroot` is served, so exports there would be fetchable by
URL. Nothing outside it is served at all.

### Import

`OracleImporter.TryBuild` returns an error string or null, the same contract
`OracleConfigurator.Reconcile` uses, so the service never touches `ModelState`.

The rules it enforces, and why each one is there:

| Rule | Why |
|---|---|
| `formatVersion` not newer than supported | Better to refuse than half-understand |
| Name present, ≤ 100 chars | Matches `[MaxLength(100)]`; otherwise `SaveChanges` throws |
| Subtype and GameSystem parse **and** are defined | See below |
| Dice: outcome count ≤ `FaceCount()` | A d4 cannot have five distinct outcomes |
| Cards: every `CardCode` in the deck, no duplicates | A code not in the deck is a corrupt file |
| Result ≤ 500 chars | Matches `[MaxLength(500)]` |

**`Enum.TryParse` alone is not validation.** `Enum.TryParse<OracleSubtype>("99", out var s)`
returns **true** and hands back `(OracleSubtype)99` — a value no `switch` case matches.
It accepts any numeric string, defined or not. Only `Enum.IsDefined` rejects it. Without
that second check a corrupt file passes validation and throws later inside `FaceCount()`,
far from the cause.

**Ranges are copied as-is, nulls included.** A dice oracle that was configured but never
band-edited exports with null ranges — a state the app itself produces in three clicks.
Demanding bands on import would mean Export → Clear All → Import destroys data the app
created. **Import must accept everything export can write, or the backup is not a backup.**

**Card labels are regenerated from `DeckFactory`, not trusted.** The code is the identity;
the label is display text derived from it. A hand-edited file cannot end up with `"H-02"`
labelled "King of Spades".

The action itself is short. `enctype="multipart/form-data"` on the form is what makes
`IFormFile` arrive at all — without it the browser posts only the filename as text, the
parameter is null, and nothing anywhere reports an error. Deserialisation is wrapped in
`catch (JsonException)` so a malformed file is a message, not a 500.

Then one line does the insert:

```csharp
_context.Oracles.AddRange(oracles);
await _context.SaveChangesAsync();
```

No entry ever has its `OracleID` set by hand. Adding entries to `oracle.OracleEntries` and
adding the oracle is enough — EF works out the insert order and fills the foreign keys
from the navigation property. One `SaveChanges`, so the whole file lands or none of it does.

### Clear All

A confirmation page reporting exactly what is about to be destroyed, then:

```csharp
using (var transaction = await _context.Database.BeginTransactionAsync())
{
    await _context.OracleEntries.ExecuteDeleteAsync();
    await _context.Oracles.ExecuteDeleteAsync();
    await transaction.CommitAsync();
}

if (_context.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer")
{
    await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('OracleEntries', RESEED, 0);");
    await _context.Database.ExecuteSqlRawAsync("DBCC CHECKIDENT ('Oracles', RESEED, 0);");
}
```

**There is no `SaveChangesAsync`, and that is not an omission.** `ExecuteDeleteAsync`
translates straight to `DELETE FROM [OracleEntries]` and runs immediately, never loading a
row or touching the change tracker. Every other action in the controller loads-changes-saves;
this one deliberately does not, because loading 78 tarot entries into memory to mark them
deleted would be absurd.

**Children before parents, explicitly.** The FK cascade would probably handle it, but
"probably" depends on provider configuration — and the tests run on SQLite while production
runs on SQL Server. Two statements in the right order behave identically everywhere.

**The reseed sits outside the transaction on purpose.** `DBCC CHECKIDENT` is *not* undone
by a rollback. Inside the transaction, a failure would restore the rows **and** leave the
counter reset — the one combination that breaks the next insert. Outside it, the worst case
is that the data is gone (intended) and IDs simply continue.

**The provider guard is what keeps the test suite green.** `DBCC` is SQL Server syntax and
would throw on SQLite. The consequence, stated plainly: that reseed line is never executed
by any test and can only be verified by hand.

### Answering the question that started it

Yes, an identity column can be reset without dropping the table — that is what
`DBCC CHECKIDENT (…, RESEED, 0)` does. Two caveats worth carrying: it is only safe on an
already-empty table (reseeding with rows present causes primary-key collisions on the next
insert), and `TRUNCATE TABLE`, the other way to get a free reset, is blocked on any table
referenced by a foreign key — so it can never be used on `Oracles`.

---

## 5. Change 4 — the "Needs setup" badge and Configure's return

**Status: done.**

### The state nobody could see

An oracle's creation spans three pages, and each one commits. Walk away after **Create**
and you have an oracle with zero entries. Walk away after **Configure** and you have
entries with no bands. Both look completely normal on the Index page.

A tempting fix is to not write anything until the flow finishes — but the Back button does
not undo a database write, and `Create` POST has already called `SaveChangesAsync` before
`Configure` ever renders. Genuinely preventing the state would mean holding a draft in
session across three requests, with expiry and abandonment to handle. **The cheaper honest
answer is to make the state visible instead of pretending it cannot happen.**

The other tempting fix is an `IsComplete` column on `Oracle`, set when EditEntries saves.
That is §1 of this very document repeating itself: a second copy of something the entries
already say, with a new way to fall out of sync.

### Projection, not `.Include`

`Index` needed two facts about each oracle's entries without loading any of them:

```csharp
.Select(o => new OracleListItem
{
    ...
    EntryCount = o.OracleEntries.Count,
    HasUnbandedEntries = o.OracleEntries.Any(e => e.RangeStart == null)
})
```

Both become correlated subqueries in one SQL statement. The `.Include` alternative would
drag 52 entry rows per card oracle into memory purely to render a list of names.

**`NeedsSetup` is a computed property on the view model, not part of the `Select`.** It
calls `OracleSubtype.Category()`, our own C# extension method, and everything inside a
`Select` on an `IQueryable` has to become SQL. EF cannot translate it and only says so at
runtime. Project the raw values; decide in memory.

### `returnTo` belongs on cancel, not on submit

The first version of "Configure returns to its caller" branched on the **success** path:
submit from Index, land back on Index. In practice that was wrong — after changing an
outcome count you have just created blank rows, and being dropped on the list means
nothing offers you the chance to fill them in.

**Submitting a form means "continue"; returning to the caller is for backing out.** So the
success path always goes to `EditEntries`, and `returnTo` drives a Cancel link instead.

Worth noting the shape of the security question even though it does not bite here.
"Send the user where the caller said" is a classic open-redirect vulnerability when the
caller supplies a *URL*. Here `returnTo` is only ever compared against a known string and
both destinations are `nameof()` constants, so `?returnTo=https://evil.example` does
nothing.

### The hole where two correct changes met

Both changes were individually right, and together they opened a gap. While Configure's
success path returned to Index, you could re-configure a fully-banded d6 from 3 outcomes to
5, get two new unbanded rows, and land back on the list **without passing through
EditEntries and its validation**. The badge did not catch it either, because the first
version asked "is at least one row banded?" — and three of the five were.

Two fixes, both kept:

- The predicate became **"is any row missing a band?"** (`HasUnbandedEntries`), which
  catches never-banded, partially-banded and fully-banded alike.
- Configure's success path stopped skipping EditEntries at all, closing the route
  structurally.

**A new shortcut past a validating page inherits responsibility for whatever that page was
protecting.** Neither change was wrong on its own; the defect existed only at the seam.

### A boolean that changed polarity

Renaming `HasBands` to `HasUnbandedEntries` inverted its meaning, and the `!` at the use
site survived the rename:

```csharp
if (OracleSubtype.Category() == OracleCategory.Dice && !HasUnbandedEntries)
```

That reads "needs setup when no row is missing a band" — backwards for every dice oracle,
badging the finished ones and clearing the broken ones. It compiled fine.
**When a bool flips polarity, the negations that depended on the old sense live somewhere
else. Check every use site, not just the declaration.**

---

## 6. What changed in the test suite

v1.0 §9 ended at 65 tests. This document ends at **114, all green** — two deleted,
fifty-one added across six new files.

### Batch 1 — the `DiceSize` removal

**`Support/TestData.cs`** — `SeedOracle` loses its `populateDiceSize` parameter, the
`DiceSize` initialiser, and the `isDice` local that only existed to compute it. All four
remaining call sites already passed two arguments, so none needed changing.

**Deleted:**

- `Reconcile_DiceWithNullDiceSize_StillRejectsCountAboveFaceCount`
- `Post_Dice_StartAboveFaceCount_RejectedEvenWhenDiceSizeNull`

Both existed to prove the ceiling check survived a null `DiceSize`. That state is no
longer representable, so the tests can't be written any more, let alone fail. The
behaviour they protected is still covered by `Post_Dice_StartAboveDieSize_ReturnsView`
and `Reconcile_DiceInvalidCount_ReturnsErrorAndChangesNothing`, which exercise the same
ceiling on an ordinary oracle.

**A regression test is only worth keeping while the regression is possible.** Delete a
test when the change makes its scenario unreachable; keep it when the change merely makes
the scenario unlikely. This pass is the first kind — the column is gone.

**Added — `EditActionTests.cs`**, the first coverage the `Edit` action has ever had:

| Test | Locks in |
|---|---|
| `Post_IdMismatch_ReturnsNotFound` | Route id vs posted id disagreeing is refused. |
| `Post_UnknownId_ReturnsNotFound` | A row deleted between GET and POST is a null load, not the `DbUpdateConcurrencyException` the old version threw. |
| `Post_SubtypeUnchanged_KeepsEntriesAndRedirectsToIndex` | A rename saves the new values and touches no entry. |
| `Post_SubtypeChanged_ClearsEntriesAndRedirectsToConfigure` | The clear-and-rebuild rule. |
| `Post_CardsChangedToDice_ClearsTheWholeDeck` | The 52-card case that ruled out reconciling across a subtype change (§3). |

That v1.0's suite had no `Edit` coverage at all is precisely why the `Update()` bug sat
there unnoticed. **A scaffolded action is still an action — "the tool generated it" is not
a reason to leave it untested.**

The new tests earned their keep on the first run. `Edit` had been typed with both
branches returning `RedirectToAction(nameof(Configure), …)`, so the `if (subtypeChanged)`
guard above them was dead and an ordinary rename bounced the user into Configure. It
compiled cleanly, it survived a read-through, and
`Post_SubtypeUnchanged_KeepsEntriesAndRedirectsToIndex` failed on it in one second with
`Expected: "Index" / Actual: "Configure"`. **A wrong redirect is invisible to the
compiler and cheap for a test — assert the `ActionName`, not just that you got a
`RedirectToActionResult`.**

### Batch 2 — export, import and Clear All

| File | Covers |
|---|---|
| `OracleImporterTests` | Every rejection rule, plus the acceptances that matter: null ranges round-trip, labels are regenerated, `SortOrder` is renumbered. No database needed — `TryBuild` is pure. |
| `ExportActionTests` | Content type and filename, enums as names, dice and card halves kept apart, `SortOrder` ordering, and the missing-`.Include` trap. |
| `ImportActionTests` | No file, empty file, unparseable JSON, the happy path, all-or-nothing on rejection, and append semantics. |
| `ClearAllActionTests` | Confirmation counts, both tables emptied, no orphans, clearing an already-empty database. |
| `EndToEndTests` | One twelve-phase walkthrough (below). |

### Supporting a controller the framework no longer builds

Two constructor-shaped changes broke the hand-built test controller, and both are worth
recognising the next time a dependency is added:

- `IWebHostEnvironment` (for Export's server-side copy) needed a stub —
  `Support/TestEnvironment.cs`, pointing `ContentRootPath` at a throwaway temp folder so
  nothing is ever written into the repo.
- `TempData` (for Import's success message) throws outside the MVC pipeline, because
  nothing supplies an `ITempDataDictionaryFactory`. `TestControllers` now assigns a
  `TempDataDictionary` over a no-op provider.

A third, `Configure`'s new `returnTo` parameter, broke eight call sites — fixed in the
**controller** rather than the tests, by giving the parameter a `= null` default. It
genuinely is optional, so saying so in the signature was the honest fix and the tests
needed no edit at all.

### The end-to-end walkthrough

`EndToEndTests` is a single method with twelve numbered phases against one database:
empty → create → configure → type outcomes → export → clear → import → verify identical →
clear → import one oracle of **every** subtype → re-configure a d20 down to 5 and confirm
diff-and-keep preserved the text → re-edit a d6 → confirm the tarot deck arrived with
`T-MAJ-00` still labelled "The Fool".

**Why it exists next to the focused tests.** Every other file pins down one action in
isolation, which catches a broken action. It does not catch a broken *seam* — Export
writing something Import cannot read, a redirect pointing at the wrong step, Clear All
leaving state that breaks the next insert. This test only passes if the pieces still fit
together.

**Why it is one method and not twelve.** Each phase depends on the state the last one
left, and xUnit deliberately gives no ordering guarantee between test methods; splitting
it would let the phases run in any order and fail at random. Shared *logic* lives in four
private helpers (`CreateOracle`, `ConfigureOracle`, `FillDiceEntries`, `ImportFile`) that
drive the same controller actions a browser would. Shared *state* stays inside the one
method.

**On tests calling other tests** — possible, but not worth doing. A failure gets reported
against whichever test called the broken one, and neither stays independently runnable.
Extract a helper instead.

### What batch 2's tests found

**A half-filled `out` parameter.** `TryBuild` accumulated each valid oracle into `oracles`
and returned on the first bad one, leaving the good ones in the list — while its own doc
comment promised "nothing is ever half-imported". Harmless today, because `Import` checks
the error first and never reads the list on that path. But the contract said one thing and
the behaviour said another, which is a trap set for the next caller. Fixed by accumulating
into a local and assigning the `out` parameter only on the success path.

**A filename with only second resolution.** Two exports inside the same second compute the
same server-side path. Surfaced as parallel test classes colliding; in the app it is a
double-click on "Export All", where the second request throws `IOException`. The tests now
give each `TestEnvironment` its own directory. The app-side sharpening is still open.

---

## 7. Corrections to v1.0

Apply these on top of v1.0's reference sections.

**§3, `Oracle` table** — drop the `DiceSize` row entirely. Face count now comes from
`OracleSubtype.FaceCount()` at the point of use, and nowhere else.

**§5, routes table** — the changed and added rows:

| Verb | Route | What it does now |
|---|---|---|
| GET | `/Oracles` | Projects `OracleListItem` per row, including a "needs setup" flag. |
| POST | `/Oracles/Create` | Insert, then redirect. (No longer sets `DiceSize`.) |
| POST | `/Oracles/Edit/5` | Update the fields. If `OracleSubtype` changed, clear all entries and redirect to `Configure`; otherwise redirect to `Index`. |
| GET/POST | `/Oracles/Configure/5` | Takes an optional `returnTo`, used only for the Cancel link. Success always continues to `EditEntries`. |
| GET | `/Oracles/Export` | Serialises every oracle, writes a copy to `App_Data/Exports`, returns a download. |
| GET | `/Oracles/Import` | The upload form. |
| POST | `/Oracles/Import` | Parse, validate, insert the whole file or none of it. Redirect → `Index`. |
| GET | `/Oracles/ClearAll` | Confirmation page with counts. |
| POST | `/Oracles/ClearAll` | Delete both tables, reseed identities on SQL Server. Redirect → `Index`. |

**§5, the flow diagram** — gains two branches:

```
Create ──▶ Configure ──▶ EditEntries ──▶ Details
              ▲
              │  (subtype changed)
            Edit

Export ──▶ .json ──▶ Import ──▶ Index
             ▲                    │
             └──── Clear All ◀────┘
```

**§6, `EditEntriesViewModel`** — `public int? DiceSize` becomes `public int FaceCount`.

**§6, ViewModels** — there are now three: `EditEntriesViewModel`, `ClearAllViewModel`,
`OracleListItem`.

**§9, "The bug the tests found"** — the stated cause is wrong. It was not old data and not
a subtype change; see §2 above. The fix described there (read `FaceCount()` at the point
of use) was correct and still stands, but it treated the symptom.

---

## 8. Principles this pass added

Continuing v1.0 §10.

7. **Never store what you can derive.** v1.0 §2 got this right for `OracleCategory` and
   wrong for `DiceSize` in the very next section. If a column can be computed from another
   column, the column is a bug with a delay on it.
8. **Fix the cause, not the reachable symptom.** v1.0 §9 made the validation read the
   right source and called the duplicate "harmless". It wasn't; it was just quiet. When a
   fix leaves the bad state representable, it isn't finished.
9. **Deleting inverts dependency order.** Consumers first, the definition last —
   otherwise nothing compiles in between, and tooling that builds before it runs (EF
   migrations) fails with an error about the wrong thing.
10. **Check what the scaffolder actually wrote.** `Edit` came from the scaffolder and was
    never read closely. `Update()` on a partially-bound detached entity is the
    scaffolder's default shape, and it is wrong the moment the entity grows a property the
    form doesn't post.
11. **A file format is an interface, not a dump of your tables.** Serialising entities
    couples every exported file to the schema. DTOs cost three small classes and buy the
    freedom to rename a column.
12. **Import must accept everything export can write.** The moment it doesn't, Clear All
    stops being a safe thing to run, because the backup can no longer restore what was
    backed up.
13. **Validate at the boundary, in the layer that can explain itself.** Import is the only
    place the app takes data it did not create, so it is the only place that needs this
    much validation — and returning a message beats throwing, because the message can name
    which oracle in the file was wrong.
14. **Defects hide at the seam between two correct changes.** The badge was right, the
    Configure redirect was right, and the combination let an unbanded oracle look
    finished. Ask what a new shortcut is now bypassing.

---

## 9. Still open

- **The roll/draw play page.** The whole point of an oracle, still unbuilt. Nothing
  currently reads the entries back for play.
- **Export filename resolution.** Two exports in the same second collide on the
  server-side path (§6). Milliseconds in the format string, or an overwrite, would close it.
- **Parking lot: importing from the vault.** Rather than uploading, list the `.json` files
  already in `App_Data/Exports` and pick one. A browser cannot be told where to open its
  file picker — that is a deliberate security boundary, and no `<input type="file">`
  attribute changes it — but the server can read its own folder, so the picker is the wrong
  side of the problem to solve. Would need a path-traversal guard: run any submitted
  filename through `Path.GetFileName()` and confirm the resolved path is still inside the
  exports folder.
- **Deferred from v1.0, unchanged:** tarot reversed, partial decks, weighted outcomes,
  free-form ranges, sub-tables, sharing, entries-form pagination.

Closed during this pass: the card form in `Configure.cshtml` carried a malformed attribute
(`<form asp-action="Configure" asp"post">`, a mistyped `method="post"`) and now matches the
dice form exactly, `asp-route-id` included. It was never actually breaking the button — see
the appendix row on tag-helper form defaults.

---

## 10. Multi-user notes

The app has Identity wired up, but `Oracle` has no owner column and `OraclesController` has
no `[Authorize]`. Everything is effectively single-user. These are the places that will
need to change, collected as they came up:

| Where | What changes |
|---|---|
| `Oracle` | Gains an owner column; every existing action filters by it. `OraclesController` gains `[Authorize]`. |
| `Index` | The projection filters to the current user. |
| `Export` | Filters to the current user's oracles instead of dumping the table. |
| `OracleDto` | **Unchanged, deliberately.** No owner field belongs in the file — ownership is a fact about a database row, and importing someone else's export must stamp it with the importing account. |
| `Import` | Stamps each new oracle with the current user's id before `AddRange`. |
| `ClearAll` | Deletes only the current user's rows — and **the identity reseed has to go**, because a table still holding other people's rows cannot be reseeded. The reseed is a single-user affordance with a hard expiry date. |
| `App_Data/Exports` | Per-user subfolders, or drop the server-side copy entirely. One shared directory would mix everyone's backups together. |

---

## Appendix — new gotchas

Append to v1.0's table.

| Symptom | Cause | Fix |
|---|---|---|
| A column is `NULL` after editing an unrelated field | `_context.Update()` on a detached, partially-bound entity marks *every* property modified and writes defaults for the ones the bind list omitted | Load the tracked row, assign the fields you mean, let change tracking write the diff |
| `InvalidOperationException: Collection was modified` while deleting children | `RemoveRange` over a navigation collection — EF's fixup removes each entity from that same collection mid-iteration | Snapshot it: `RemoveRange(parent.Children.ToList())` |
| Deleting a property breaks the build in several files at once | Consumers still reference it | Reverse the build order: update every consumer first, delete the definition last |
| `Add-Migration` fails with a C# compiler error | EF tooling builds the project before generating anything | Finish the code change and get a clean build first |
| `HTTP Error 500.30` plus a "Smart App Control has blocked part of this app" popup, intermittently on build | Windows 11 Smart App Control (enforcing) refuses to load the freshly-built, unsigned `GM_Toolbox.dll`. Confirm in Event Viewer → Applications and Services → Microsoft → Windows → **CodeIntegrity/Operational**, event ID **3077**, or from a terminal where the real error is `FileLoadException … 0x800711C7`. Not the firewall, and not fixable by switching off IIS Express — it blocks under Kestrel and plain `dotnet run` too | SAC has **no exclusion list**. Turn it off: Windows Security → App & browser control → Smart App Control settings → Off. **Irreversible without reinstalling Windows.** Clean + Rebuild afterwards |
| A 500.30 hides the real exception | It is generated by the ASP.NET Core Module under IIS Express, which only reports that startup failed | Run `dotnet run --launch-profile http` — the terminal prints the actual exception. A 500.30 also tells you the run profile is IIS Express, since Kestrel cannot produce one |
| `MSB3027: could not copy … locked by "IIS Express Worker Process"` | An orphaned worker or a live debug session still holds `bin\Debug\...\GM_Toolbox.dll`; it can outlive Visual Studio showing anything as running | Stop debugging, or kill the process. Kestrel profiles (`http`/`https`) do not leave one behind the way IIS Express does |
| Assuming a tag-helper `<form>` with no `method` defaults to GET | It doesn't. Raw HTML defaults to GET, but the Form Tag Helper emits `method="post"` whenever `asp-action` / `asp-controller` / `asp-page` is present — which is why the scaffolded Create/Edit/Delete forms work without one | Don't reason about a tag helper from raw-HTML defaults; check the rendered source, or check whether the page already works |
| A tag-helper attribute silently does nothing (`asp-rout-id`) | Misspelled `asp-*` attributes are not recognised, so Razor passes them through as literal HTML and generates no route value — the link goes to the action with no id | There is no compiler check for these. Confirm the rendered `href`, and cover the path with a test |
| An uploaded `IFormFile` parameter is always null | The form is missing `enctype="multipart/form-data"`, so the browser posts only the filename as text | Add it. Also confirm the input's `name` matches the action's parameter name |
| `File.WriteAllBytesAsync` won't compile in a controller | `File` resolves to `ControllerBase.File(...)`, the method that returns a download | Fully qualify it: `System.IO.File.WriteAllBytesAsync(...)` |
| A corrupt enum value passes validation and throws later | `Enum.TryParse("99", out OracleSubtype s)` returns **true** with an undefined value — it accepts any numeric string | Always pair it: `Enum.TryParse(...) && Enum.IsDefined(value)` |
| A method returns an error *and* a populated `out` parameter | The `out` was filled incrementally and the method returned early on failure | Accumulate into a local and assign the `out` parameter only on the success path |
| A renamed boolean inverts behaviour with no compiler error | The name changed at the declaration; the `!` that depended on the old sense lives at the use site | When a bool flips polarity, visit every use site — the type system cannot help here |
| Adding a constructor parameter breaks the test project | Tests build the controller by hand, outside DI | Either stub the new dependency in the test factory, or — if the parameter really is optional — give it a default in the signature |
| Two writes in the same second collide on one path | A generated filename with only second resolution | Add milliseconds, or open with an overwrite mode |
