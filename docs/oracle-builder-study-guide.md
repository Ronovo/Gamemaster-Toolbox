# Oracle Builder — Study Guide

A walkthrough of everything built on 2026-09-01/02: the ASP.NET Core MVC starting
point, the data-modelling discussion, the data model we landed on, the pages and
routes, the MVC concepts used along the way, the services we extracted, the test
suite, and the gotchas that cost time.

---

## 1. Where we started — the MVC layout

The project came from the ASP.NET Core **MVC template with Individual Accounts auth**.
Key pieces of that layout:

| Folder / file | What it holds |
|---|---|
| `Program.cs` | App startup: registers services (DI), builds the HTTP pipeline, maps routes. Top-level statements, no `Main` method. |
| `Controllers/` | Controller classes. Each public `Task<IActionResult>` method is an **action**. |
| `Models/` | Entity classes (the shape of the data) and, later, `ViewModels/` and `Enums/`. |
| `Views/` | Razor (`.cshtml`) files. `Views/Oracles/Index.cshtml` is the view for `OraclesController.Index`. |
| `Views/Shared/_Layout.cshtml` | The page shell (nav, `<head>`, footer). Every view renders inside it via `@RenderBody()`. |
| `Data/ApplicationDbContext.cs` | The EF Core `DbContext`. Inherits `IdentityDbContext`, so it owns the auth tables too. |
| `wwwroot/` | Static files — `css/site.css`, `lib/bootstrap/…`, etc. Served as-is. |
| `Properties/launchSettings.json` | Local run profiles (Kestrel `http`/`https`, IIS Express, Docker). |

### The MVC request cycle

```
Browser request:  GET /Oracles/Configure/5
        │
        ▼
Routing            {controller}/{action}/{id?}  →  OraclesController.Configure(id: 5)
        │
        ▼
Controller action  loads data, builds a model object, returns an IActionResult
        │                         (ViewResult / RedirectToActionResult / NotFoundResult …)
        ▼
View (if ViewResult)  Views/Oracles/Configure.cshtml renders with that model
        │
        ▼
Response           HTML (wrapped in _Layout.cshtml)
```

A **POST** action does work (writes to the database), then almost always returns
`RedirectToAction(...)` instead of a view — the **Post/Redirect/Get** pattern, so a
browser refresh doesn't re-submit the form.

### The scaffolder

Visual Studio's "MVC Controller with views, using Entity Framework" scaffolder
generated `OraclesController` plus `Index / Details / Create / Edit / Delete` views
from the `Oracle` model. That gave us basic CRUD for free; everything past that
(`Configure`, `EditEntries`, the services) we wrote by hand.

---

## 2. The modelling discussion — how we got to the data model

The feature: a user builds their own random table ("oracle"). Roll a die or draw a
card, read the outcome they wrote.

### Options considered and rejected

| Idea | Why we rejected it |
|---|---|
| One table with 100 outcome columns | Wastes space, ugly, arbitrary cap, schema change to extend. |
| A table per die type (`d4`, `d6`, …) | Explosion of near-identical tables. |
| Lookup tables for `GameSystem` / `OracleType` | Overkill — the sets are small and fixed. Enums are simpler. |

### What we landed on

A **one-to-many (parent/child) relationship**:

- **`Oracle`** — the header (name, game system, what randomiser drives it).
- **`OracleEntry`** — **one row per possible outcome**. A `d6` oracle has 6 rows, a
  `d100` has 100, a tarot oracle has 78.

"One row per outcome" is the whole trick — no wide tables, no per-type tables, and
adding a new die is one enum value, not a migration.

### Other decisions from the discussion

- **Store the specific type, derive the category.** `OracleSubtype` (`D4`…`D100`,
  `PlayingCards`, `TarotCards`) is stored; `OracleCategory` (`Dice` / `Cards`) is
  computed from it in code, so the two can never contradict.
- **Card identity is a string `CardCode`, not a stack of enum columns.** One
  `"T-MAJ-12"` string identifies any card in any supported deck. The enum-column
  approach couldn't cleanly represent tarot's 14-card minor suits.
- **Threshold-style ranges.** On the edit page each outcome has one "starts at"
  number; the end of each band is derived (`next.startsAt - 1`, last band ends at the
  die's face count). Gaps and overlaps are impossible to express.
- **Diff-and-keep.** Re-configuring an oracle reconciles the entry set instead of
  wiping and recreating, so typed text survives.

---

## 3. The data model (final)

### `Oracle`

| Field | Type | Notes |
|---|---|---|
| `ID` | `int` | PK, identity. |
| `Name` | `string` | `[Required, MaxLength(100)]`. |
| `GameSystem` | `GameSystem` enum | Stored as string (see §4). |
| `OracleSubtype` | `OracleSubtype` enum | Stored as string. Drives everything. |
| `DiceSize` | `int?` | Face count for dice; `null` for cards. **Can go stale** — see §9. |
| `OracleEntries` | `ICollection<OracleEntry>` | Navigation property. Cascade delete. |

### `OracleEntry`

| Field | Type | Notes |
|---|---|---|
| `ID` | `int` | PK. |
| `OracleID` | `int` | FK → `Oracle`. Required → cascade delete by default. |
| `SortOrder` | `int` | Display order, `1..N`. |
| `Result` | `string` | `[MaxLength(500)]`. The user's outcome text. |
| `RangeStart` / `RangeEnd` | `int?` | Inclusive dice band. Null on card oracles. |
| `CardCode` | `string?` | Stable card id (`"H-02"`, `"T-W-A"`). Null on dice oracles. |
| `CardLabel` | `string?` | Display text (`"Two of Hearts"`). |

### EF Core wiring (`ApplicationDbContext.OnModelCreating`)

```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);          // <-- MUST be first: Identity configures its tables here

    builder.Entity<Oracle>().Property(o => o.OracleSubtype).HasConversion<string>();
    builder.Entity<Oracle>().Property(o => o.GameSystem).HasConversion<string>();
}
```

- `HasConversion<string>()` stores the enum name (`"D6"`) instead of an int, so the
  raw database is readable. It has no data-annotation equivalent — it must live here.
- The one-to-many relationship is picked up **by convention** (`Oracle.OracleEntries`
  collection + `OracleEntry.OracleID` FK); no explicit config needed.
- **Migrations**: `Add-Migration <name>` → `Update-Database`. Every EF command builds
  the whole project first, so it must compile (views included) before a migration
  will generate.

---

## 4. Enums and extension methods

```csharp
public enum OracleSubtype { D4, D6, D8, D10, D12, D20, D100, PlayingCards, TarotCards }
public enum OracleCategory { Dice, Cards }
public enum GameSystem { DungeonsAndDragons, Pathfinder, /* … */ }
```

C# enums **cannot contain methods**, so the derived logic lives in a separate static
class of **extension methods**:

```csharp
public static class OracleSubtypeExtension
{
    public static OracleCategory Category(this OracleSubtype s)
    {
        switch (s)
        {
            case OracleSubtype.PlayingCards:
            case OracleSubtype.TarotCards:
                return OracleCategory.Cards;
            default:
                return OracleCategory.Dice;
        }
    }

    public static int FaceCount(this OracleSubtype s)
    {
        switch (s)
        {
            case OracleSubtype.D4:  return 4;
            // …
            case OracleSubtype.D100: return 100;
            default: throw new InvalidOperationException($"{s} has no face count");
        }
    }
}
```

`this OracleSubtype s` in the first parameter is what makes it callable as
`oracle.OracleSubtype.Category()`.

`[Display(Name = "Dungeons & Dragons")]` on an enum member gives it friendlier text in
dropdowns via `Html.GetEnumSelectList<GameSystem>()`.

---

## 5. Pages and routes

Default route: `{controller=Home}/{action=Index}/{id?}` — so every URL below is
`/Oracles/<action>/<id>`.

| Verb | Route | Action | What it does | Result |
|---|---|---|---|---|
| GET | `/Oracles` | `Index` | List all oracles. | View (list) |
| GET | `/Oracles/Details/5` | `Details` | Show one oracle. | View |
| GET | `/Oracles/Create` | `Create` | Blank create form. | View |
| POST | `/Oracles/Create` | `Create(Oracle)` | Insert; set `DiceSize` for dice; redirect. | Redirect → `Configure` |
| GET | `/Oracles/Edit/5` | `Edit` | Edit name / subtype / system. | View |
| POST | `/Oracles/Edit/5` | `Edit(id, Oracle)` | Update those fields. | Redirect → `Index` |
| GET | `/Oracles/Delete/5` | `Delete` | Confirm page. | View |
| POST | `/Oracles/Delete/5` | `DeleteConfirmed(id)` | Delete. | Redirect → `Index` |
| GET | `/Oracles/Configure/5` | `Configure(id)` | Show the "how many outcomes?" (dice) or "generate deck" (cards) form. | View |
| POST | `/Oracles/Configure/5` | `Configure(id, outcomeCount)` | Hand off to `OracleConfigurator.Reconcile`; on error re-show, else save. | Redirect → `EditEntries` |
| GET | `/Oracles/EditEntries/5` | `EditEntries(id)` | Build `EditEntriesViewModel`, one row per entry. | View |
| POST | `/Oracles/EditEntries/5` | `EditEntries(vm)` | Validate dice bands, save `Result` text, derive `RangeStart`/`RangeEnd`. | Redirect → `Details` |

### The flow

```
Create ──▶ Configure ──▶ EditEntries ──▶ Details
 (name,     (# of         (type each      (done)
  type)      outcomes /    outcome +
             generate      dice bands)
             deck)
```

`Index` has a **Configure** link on every row so an existing oracle can be
re-configured (this drives diff-and-keep).

---

## 6. MVC concepts used along the way

### ViewModels

The scaffolded `Create`/`Edit` pages bind the `Oracle` entity directly — fine, the
form *is* the entity's fields. `EditEntries` is different: it's a list of rows with a
label + textbox, which isn't the shape of `OracleEntry`. So it gets a
**`EditEntriesViewModel`** — a class written for exactly that screen:

```csharp
public class EditEntriesViewModel
{
    public int OracleID { get; set; }
    public string? Name { get; set; }          // display-only → nullable (see §9)
    public bool IsDice { get; set; }
    public int? DiceSize { get; set; }
    public List<EntryRow> Rows { get; set; } = new();
}
public class EntryRow
{
    public int EntryId { get; set; }
    public string? Label { get; set; }
    public string Result { get; set; } = string.Empty;
    public int StartsAt { get; set; }
}
```

Two payoffs: the view gets exactly what it needs, and a crafted POST can't set fields
you didn't intend on a real entity (overposting).

### Binding a list back from a form

The Razor loop **must** be an indexed `for`, not `foreach`:

```cshtml
@for (int i = 0; i < Model.Rows.Count; i++)
{
    <input type="hidden" asp-for="Rows[i].EntryId" />
    <label asp-for="Rows[i].Result">@Model.Rows[i].Label</label>
    <input asp-for="Rows[i].Result" class="form-control" />
}
```

`asp-for="Rows[i].Result"` renders `name="Rows[0].Result"`, `name="Rows[1].Result"` …
which is exactly what model binding needs to rebuild `vm.Rows` on POST. `foreach`
produces names that don't bind.

### Redirects with route values

```csharp
return RedirectToAction(nameof(Configure), new { id = oracle.ID });
```

`nameof(Configure)` → the string `"Configure"`. `new { id = oracle.ID }` is an
**anonymous object** used as route values — the property name `id` lines up with the
`Configure(int? id)` parameter and builds `/Oracles/Configure/5`.

### Forms and CSRF

Every data-changing POST action carries:

```csharp
[HttpPost]
[ValidateAntiForgeryToken]
```

The `<form>` tag helper auto-adds a hidden anti-forgery token tied to the session
cookie; `[ValidateAntiForgeryToken]` rejects any POST without a matching one. Stops a
malicious site from submitting to your app using a logged-in user's cookie.

### Eager loading

```csharp
_context.Oracles.Include(o => o.OracleEntries).FirstOrDefaultAsync(o => o.ID == id);
```

Without `.Include(...)`, `oracle.OracleEntries` comes back empty — EF only loads what
you ask for.

### Dependency injection

```csharp
// Program.cs
builder.Services.AddScoped<OracleConfigurator>();

// OraclesController
public OraclesController(ApplicationDbContext context, OracleConfigurator configurator) { … }
```

`AddScoped` = one instance per HTTP request. It must be scoped (not singleton)
because it depends on `ApplicationDbContext`, which is scoped — a longer-lived service
can't safely hold a shorter-lived dependency.

---

## 7. Services we extracted

### `DeckFactory` (pure function)

Given an `OracleSubtype`, returns `List<CardDefinition>` — the concrete cards for that
deck. Built from local **tuple arrays**, not database tables, because the data is
fixed and tiny:

```csharp
var suits = new[] { (Code: "H", Name: "Hearts"), /* … */ };
var ranks = new[] { (Code: "02", Name: "Two"),   /* … */ };
```

A shared `BuildSuitedCards(suits, ranks, codePrefix)` helper produces the suit×rank
grid for both the 52-card and 78-card decks (tarot passes `"T-"` as the prefix).

**Big-O aside:** the nested `foreach` over suits×ranks looks like it "should" be
optimised, but the input is fixed at 4×13 or 4×14 — there is no `n` that grows. Big-O
is about growth; a constant 52 iterations is not a performance concern.

### `OracleConfigurator` (the reconcile logic)

Pulled out of `Configure` POST so the controller just loads → delegates → saves →
redirects. Signature:

```csharp
public string? Reconcile(Oracle oracle, int? outcomeCount)
// returns an error message, or null on success. Caller calls SaveChanges.
```

A service can't touch `ModelState` (that's MVC's), so it **returns the error string**
and the controller decides what to do with it.

---

## 8. Diff-and-keep in detail

When an oracle is re-configured, `Reconcile` makes the entry set match the request
**without losing typed text**.

**Dice** — match by position:

- Requested < existing → remove rows from the tail (`SortOrder` high → low).
- Requested > existing → append blank rows.
- The survivors keep their `Result` and any range values.

**Cards** — match by `CardCode`:

- Build the target deck. Any stored entry whose code isn't in it → delete.
- Any deck card with no stored entry → insert blank.
- Matches stay, with `SortOrder` renumbered to deck order and `CardLabel` refreshed.

This is why `CardCode` had to be a **stable** string — change the scheme and every
stored entry orphans.

---

## 9. Testing

**`GM Toolbox.Tests`** — a sibling project (not nested; the Web SDK auto-compiles
nested `.cs` files). 65 tests, run with `dotnet test "GM Toolbox.Tests"`.

**Approach:** controller-level integration tests. Each test:

1. Creates an `ApplicationDbContext` on an **in-memory SQLite** database.
2. Calls an action / service method directly.
3. Asserts on the returned `IActionResult` type and the resulting rows.

**Why SQLite, not the EF InMemory provider:** SQLite is a real relational database —
it honours foreign keys, cascade deletes, and the enum→string conversions. The
InMemory provider ignores all of those.

**Fresh context per step** (seed / act / assert each get their own) mirrors the way
each HTTP request gets its own scoped context, and catches bugs that only appear
across that boundary.

**Coverage:** `DeckFactory`, `OracleSubtypeExtension`, `OracleConfigurator`
(diff-and-keep), `Configure` actions, `EditEntries` actions.
**Not covered:** routing, model binding from form fields, view rendering, auth, the
real SQL Server provider — those are exercised by hand.

### The bug the tests found (after the fact)

Manual testing turned up: a `d4` oracle accepted outcome "starts at" values of `9`.

**Cause:** the validation compared `startsAt > oracle.DiceSize`, and `DiceSize` was
`null` on that oracle (old data, or an `Edit` that changed the subtype without
updating `DiceSize`). In C#, `int > null` evaluates to **`false`** — so the ceiling
check silently passed for any value.

**Fix:** derive the ceiling from the source of truth —
`oracle.OracleSubtype.FaceCount()` — at the point of use, in both `OracleConfigurator`
and `EditEntries` POST. The stale `DiceSize` column is now harmless for validation.

Two regression tests (`Reconcile_DiceWithNullDiceSize_StillRejectsCountAboveFaceCount`,
`Post_Dice_StartAboveFaceCount_RejectedEvenWhenDiceSizeNull`) lock the behaviour in.

---

## 10. The process, in general

1. **Model the data before writing code.** We spent the first stretch purely on the
   entity shapes, the enums, and the trade-offs — no code. That decision record
   became a living reference document.
2. **Build in dependency order.** Leaf types first (view model → the action that uses
   it → the view). Never reference a class that doesn't exist yet.
3. **One path end-to-end before the next.** Dice creation flow fully working —
   Configure, EditEntries, ranges persisting — before starting cards. Then playing
   cards fully working before tarot.
4. **Navigation is part of the feature.** A page nothing links to isn't done and
   can't be tested; the `Configure` link on `Index` was a required step, not polish.
5. **Extract a service when logic outgrows the controller.** The reconcile logic was
   real domain work doing add/remove/diff — it belonged in `OracleConfigurator`, which
   also made it directly testable.
6. **Write tests, then let them find the next bug.** The suite passed green, manual
   testing found a hole the tests didn't cover, and that hole became a new regression
   test before the fix went in.

---

## Appendix — gotchas that cost time

| Symptom | Cause | Fix |
|---|---|---|
| List text pushed to centre | `text-align: center` inherits from Bootstrap's `text-center` on a parent | Wrap the list in an element that resets alignment |
| `d@Model.DiceSize` printed literally | Razor reads `d@Model.DiceSize` as an email address | Parenthesise: `d@(Model.DiceSize)` |
| GameSystem group vanished on a form | `<select … />` — `<select>` is **not** self-closing; the browser swallows following markup | Use `<select …></select>` |
| "Could not load assembly 'GM Toolbox'" from EF tools | Space in the assembly name | `<AssemblyName>GM_Toolbox</AssemblyName>` |
| "The Name field is required" with no Name field | Non-nullable `string` on a bound model is **implicitly required** (nullable reference types on) | Make display-only properties `string?` |
| Dropdown showed `OracleType` not a picker | Scaffolder rendered enums as `<input>` | `<select asp-items="Html.GetEnumSelectList<OracleSubtype>()">` |
| "not all code paths return a value" | A required `return View(...)` left as a `// TODO` | It's real code, not a placeholder |
| 500.30 on run, but `dotnet run` works | IIS Express hosting layer, stale worker process | Switch the run profile to `http`/`https` (Kestrel) |
| Test project broke the main build | Web SDK auto-compiles every nested `.cs` | Put the test project in a **sibling** folder |
| `d4` accepted "starts at 9" | `int > null` is `false` in C#; `DiceSize` was null | Compare against `OracleSubtype.FaceCount()` |
