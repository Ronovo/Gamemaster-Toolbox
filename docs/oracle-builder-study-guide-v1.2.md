# Oracle Builder — Study Guide v1.2 ("Polish")

**A follow-up to [`oracle-builder-study-guide.md`](./oracle-builder-study-guide.md) (v1.0) and
[`oracle-builder-study-guide-v1.1.md`](./oracle-builder-study-guide-v1.1.md).**
Both are left unchanged as the record of what they described. This document is the polish
pass: the loose ends v1.1 left dangling, the things that were quietly broken, and the
cosmetic debt the scaffolder left behind.

Scope was set deliberately narrow. **Nothing here adds a route, a column, or a migration.**
The roll/draw play page and multi-user both stay in "Still open" where v1.1 put them — this
pass is about finishing what already exists rather than starting what doesn't.

Read v1.0 and v1.1 first. Everything here assumes them.

---

## 0. Catch-up — the parts of v1.0 and v1.1 that matter here

**An oracle is a header plus one row per outcome.** `Oracle` holds the name, game system and
`OracleSubtype`; `OracleEntry` holds one row per possible result. Dice entries are keyed by
`RangeStart`/`RangeEnd`; card entries by `CardCode`/`CardLabel`. Each entry carries exactly
one of those pairs and nulls in the other. (v1.0 §2, §3)

**Store the specific thing, derive the general thing.** `OracleSubtype` is stored;
`OracleCategory` is computed from it by an extension method. v1.1 §1 made this the whole
theme of that pass, and deleted the `DiceSize` column for breaking it. (v1.0 §2, v1.1 §1)

**Derived logic lives in a static extension class.** C# enums cannot hold methods, so
`Category()` and `FaceCount()` sit in `OracleSubtypeExtension`. This pass adds a third method
in that shape. (v1.0 §4)

**`.Include` or the collection comes back empty.** EF loads only what you ask for, and a
missing `.Include` produces no error — just an empty navigation collection. v1.0 §6 recorded
it; v1.1 §4 caught it in `Export`, where the fix was one line and the symptom was a
valid-looking export file containing none of the data. It bites a third time below. (v1.0 §6)

**Creation spans three committing pages.** `Create` saves, `Configure` saves, `EditEntries`
saves. Walk away partway and you have a real, half-finished oracle in the database. v1.1 §5
decided not to prevent that state but to *show* it, via the "Needs setup" badge on the list.
(v1.1 §5)

That last decision is where this pass picks up.

---

## 1. The theme of this pass: the screen has to say it

v1.1's theme was one source of truth **in the data** — if a fact is stored twice, the copies
will eventually disagree. v1.2's theme is the same honesty one layer out, **in the
interface**: a fact the app knows and never displays might as well not exist.

v1.1 §5 already got this right once. An oracle with no entries was a state the database could
hold and the list page could not show, so the badge was added rather than the state
prevented. The conclusion there was framed as a cost-benefit — showing the state was cheaper
than preventing it — but it generalises further than that:

**Work the user cannot see is not finished.** Not "not polished". Not finished.

Three things in this pass are the same defect wearing different clothes:

- `Details` is the page every flow redirects to when it is done, and it displayed the
  oracle's name, type and system and **not one of its outcomes**. The data was there. The
  page never asked for it.
- A successful import set a confirmation message and no view ever rendered it, so importing a
  file redirected to a list that looked exactly as it had before.
- Two exports in the same second collided on a filename and threw, because the only thing
  distinguishing one export from another was a timestamp too coarse to distinguish them.

None of these are bugs in the sense v1.1 §2 meant — nothing writes a wrong value. They are
bugs in the sense that the app knows something and does not say it.

---

## 2. Change 1 — Details shows what you made

**Status: done.**

### The markup, first

`Details.cshtml` opened one `<dl class="row">` and closed it three times. Stripped of Razor:

```html
<dl class="row">
    <dt>Name</dt>          <dd>Weather</dd>
</dl>
    <dt>Oracle Type</dt>   <dd>D6</dd>
</dl>
    <dt>Game System</dt>   <dd>Dungeons and Dragons</dd>
</dl>
```

This is not a cosmetic complaint. It rendered visibly wrong and nobody noticed, which is
worth understanding.

A `<dl>` is a **description list**, exactly as `<ul>` is an unordered list. Three
`<dt>`/`<dd>` pairs are three *items in one list*, not three lists — the same way three
bullets are three `<li>` inside one `<ul>`. The instinct to count the pairs and reach for
three `<dl>` elements is the mistake; a list element wraps all of its items.

The visible consequence comes from Bootstrap. `col-sm-2` and `col-sm-10` are column widths,
and columns only sit side by side when their parent is a flex container — which is what
`class="row"` provides. Only the **Name** pair was inside the `<dl class="row">`. After that
first stray `</dl>`, the Oracle Type and Game System pairs had no row parent at all, so each
`<dt>` and `<dd>` fell back to being a plain block and stacked vertically. One label lined up
in a column and two did not, on a page in daily use.

The fix is one `<dl class="row">` wrapping all six tags. **Browsers silently recover from
unbalanced tags, so bad structure shows up as bad layout rather than as an error.** If a
Bootstrap grid class is not doing anything, check that its parent is actually the element it
needs.

### The real miss: the page showed nothing you made

`EditEntries` POST redirects to `Details`. It is the last page of the build flow — the one
that says "you are done" — and it listed the oracle's name, type and system and none of its
outcomes. You could build a 78-row tarot oracle and have no screen anywhere that showed you
the 78 rows, short of going back into the edit form.

Two changes, in dependency order.

**The controller had to load the entries.** `Details` was the only action in the file that
loaded an oracle by hand:

```csharp
var oracle = await _context.Oracles
    .FirstOrDefaultAsync(m => m.ID == id);
```

Adding a `@foreach` over `Model.OracleEntries` against that would have rendered **zero rows
and no error** — the third appearance of the `.Include` trap. But the fix was not to add
`.Include`; it was to stop hand-rolling the query. `LoadOracleWithEntries(id)` already existed
at the bottom of the controller and `Configure`, `EditEntries` and `Edit` all used it.
`Details` was the odd one out.

**When you are about to add a line to fix something, check whether the fix is already a helper
three other callers use.** The `.Include` trap is easy to fall into twice; it is much harder
to fall into when every caller goes through one loader.

**The view got an Outcomes section** with three states, mirroring the states `NeedsSetup`
already recognises on the list page (v1.1 §5):

| State | Rendered |
|---|---|
| No entries at all | "This oracle has no outcomes yet." No table. |
| Dice entries, no bands set | Amber warning, plus the table. |
| Set up | The table. |

The table's second column is always `Result`. Its first column is the entry's key, and the
header switches between "Roll" and "Card" — the dice/cards split that runs through the whole
app, surfacing one more time.

### `KeyLabel()` — why the branching left the view

That first column has four cases: a card shows its label; a dice band shows `1-3`; a
single-number band shows `6`, not `6-6`; and a band that was never set has to say so rather
than render an empty cell. Written in Razor it was four `@if`/`else if` blocks with markup
inside each, including a `<text>` tag to stop Razor misreading a bare hyphen after a C#
expression.

It moved into `Models/OracleEntryExtension.cs`:

```csharp
public static string KeyLabel(this OracleEntry entry)
{
    if (entry.CardLabel != null)
    {
        return entry.CardLabel;
    }

    if (entry.RangeStart == null)
    {
        return "not set";
    }

    if (entry.RangeStart == entry.RangeEnd)
    {
        return entry.RangeStart.Value.ToString();
    }

    return $"{entry.RangeStart}-{entry.RangeEnd}";
}
```

and the cell became `<td>@entry.KeyLabel()</td>`.

**The branching did not go away.** Four cases went in and four cases came out. That is worth
saying plainly, because "simplify this" often means "make the branches disappear", and here
they cannot — the four cases are real distinctions in the data. What changed is where they
live, and that buys three things:

1. **It is testable.** `GM Toolbox.Tests` can call methods and controller actions. It cannot
   reach Razor markup at all. Logic in a view is logic no test will ever cover. This is the
   only piece of the entire v1.2 pass the suite can reach — everything else is markup.
2. **It is reusable.** "How do I name this outcome" is a question the list page, a delete
   confirmation, and the eventual roll/draw page all need answered the same way.
3. **It matches the shape the app already uses.** `Category()` and `FaceCount()` are the same
   thing: derive a fact from stored data, in a static class, outside both the model and the
   view. `KeyLabel()` is the third. The next person looking for derived logic knows where to
   look.

**Why an extension method and not a property on `OracleEntry`.** `OracleEntry` is an EF entity
mapped to a table, so anything that looks like a property on it is a candidate for a column,
and keeping it out of the mapping means thinking about `[NotMapped]`. An extension method is
not a member of the class at all, so EF never sees it and the question never comes up. It is
the same instinct as v1.1 §1 — do not store what you can derive, and do not even *look* like
you store it.

**The `@using` that is not in `_ViewImports`.** `Category()` lives in
`GM_Toolbox.Models.Enums`, and `Views/_ViewImports.cshtml` imports `GM_Toolbox` and
`GM_Toolbox.Models` but not `.Enums`. Without `@using GM_Toolbox.Models.Enums` at the top of
the view the build fails saying `OracleSubtype` has no definition for `Category` — an error
that is lying to you. The method exists; the namespace is not imported. Extension methods are
only visible when their namespace is in scope, so "no definition" on an extension method
almost always means a missing `using`, not a missing method. `Configure.cshtml` already
carried that line; `Details.cshtml` did not. (`KeyLabel()` needs no import — it was
deliberately put in `GM_Toolbox.Models`, which `_ViewImports` already has.)

### `CS8603` on a value that cannot be null

`return entry.RangeStart.ToString();` produced **CS8603: Possible null reference return**, two
lines below a check that returns early when `RangeStart` is null.

The warning is not about the value. `RangeStart` is `int?` — `Nullable<int>` — and calling
`.ToString()` on it resolves to the inherited `object.ToString()`, which .NET annotates as
returning `string?` on the grounds that any override could return null. The method promises
`string`. That mismatch is the whole warning, and no amount of null-checking the *value*
addresses it, because the value was never the problem.

`.Value` unwraps `int?` into a plain `int`, and `int.ToString()` **is** annotated non-null:

```csharp
return entry.RangeStart.Value.ToString();
```

`.Value` throws at runtime on an empty nullable, so it is only safe once you have proven the
nullable is populated — which the `return "not set"` two lines above does.

This is the mirror image of the `DiceSize` bug in v1.0 §9. There, `int > null` silently
evaluated to `false` and the compiler said nothing while a `d4` accepted a roll band starting
at 9. Here the compiler is loud about a null that cannot occur. **Nullable value types are
quiet where you want noise and noisy where you do not.** The defence in both directions is the
same: unwrap explicitly, at the point where you have already established which case you are in.

---

## 3. Change 2 — the import message nobody sees

**Status: done.**

The end of a successful import, in `OraclesController.Import`:

```csharp
TempData["ImportMessage"] = $"Imported {oracles.Count} oracle(s).";
return RedirectToAction(nameof(Index));
```

Nothing read it. A grep of the whole repository found that line to be the only `TempData`
reference outside the test support code. So importing a file redirected to a list that
looked exactly as it had a moment earlier, and the only way to confirm the import had worked
was to count rows.

What makes this one worth recording is the second half. v1.1 §6 documented that the test
suite had to grow a `TempDataDictionary` over a no-op provider, because `TempData` throws
outside the MVC pipeline and `TestControllers` builds its controller by hand. That
infrastructure was written, debugged and committed **to support a message no user could
ever see.** The test suite was more complete than the feature. Support code has a way of
implying the feature it supports is finished.

### Why `TempData` and not `ViewData`

`Import` POST ends in `RedirectToAction` — the Post/Redirect/Get pattern from v1.0 §1. That
means two separate HTTP requests: the POST that does the work, and the GET that renders the
list. `ViewData` and `ViewBag` live and die inside one request, so anything put there by the
POST is gone before the GET starts.

`TempData` exists for exactly this gap. It is backed by a cookie, so it survives the
redirect, and it is **read-once**: reading a key marks it so it is not carried into the
*next* request, which is what stops the banner reappearing on every subsequent refresh.

The render, added to `Index.cshtml` under the `<h1>`:

```cshtml
@if (TempData["ImportMessage"] != null)
{
    <div class="alert alert-success">@TempData["ImportMessage"]</div>
}
```

Reading the key twice here — once in the condition, once in the body — is safe, and the
distinction is worth understanding. Reading removes the key from the set that gets saved for
the next request; it does not remove the value from the dictionary you are currently holding.
Within one request you can read it as often as you like. Across requests you get it once.

`TempData["..."]` returns `object?`, not `string`. Razor calls `ToString()` on it when
rendering, so no cast is needed — and that is also why the check is `!= null` rather than
`string.IsNullOrEmpty(...)`, which would need a cast first.

### The reason it looked broken

The `@if` block was correct the first time and the banner still did not appear. **Runtime
compilation of `.cshtml` is off by default in .NET 6 and later** — views are compiled into
the assembly at build time. Editing a view and refreshing the browser serves the previously
compiled view; the new markup is genuinely not in the page.

This cost time twice in one session. It is a bad failure mode because it looks exactly like a
logic bug: no error, no warning, the page simply behaves as though your edit does not exist.
**When a view change appears to have no effect, rebuild before you debug it.**

---

## 4. Change 3 — export filenames

**Status: done.** Closes v1.1 §9.

```csharp
var filename = $"gm-toolbox-oracles-{DateTime.Now:yyyy-MM-dd-HHmmssfff}.json";
```

`HHmmss` was hours, minutes and seconds, so every export inside the same second computed the
same string. That one variable is used twice — as the download name handed to the browser,
and as a real path under `App_Data/Exports`. The download name does not care about
collisions; the path does, and a double-click on "Export All" threw `IOException`.

`fff` adds three-digit milliseconds. In a custom date format string these letters are
placeholders rather than literal text, which is why `yyyy` produces `2026` and not four y's.

**What this fixes, stated honestly:** it narrows the collision window from one second to one
millisecond. It does not make collisions impossible. That is the right trade here — two
clicks from a human are never a millisecond apart, and v1.1 §6 already isolated the test
suite by giving each `TestEnvironment` its own directory, so the remaining window is
theoretical rather than reachable. A guaranteed-unique name means putting a GUID in it, which
costs real readability for a file people are meant to find on disk later. **"Impossible" is
not always worth more than "unreachable and legible".**

One deliberate inconsistency left in place: `DateTime.Now` is local time in the filename,
while the JSON body's `exportedUtc` field is UTC. The filename is for a human scanning a
folder, where local time is what they expect; the field inside the file is data, where UTC is
correct. Different audiences, different right answers.

---

## 5. Change 4 — the cosmetic pass

**Status: done.**

### The scaffolder line, drawn in the UI

Sorting the pages by whose heading was wrong produced a perfectly clean split:

| Page | Heading before | Written by |
|---|---|---|
| Configure | `Configure: @Model.Name` | by hand |
| EditEntries | `Outcomes: @Model.Name` | by hand |
| Import | `Import Oracles` | by hand |
| ClearAll | `Clear All Oracles` | by hand |
| Index | `Index` | scaffolder |
| Create | `Create` | scaffolder |
| Edit | `Edit` | scaffolder |
| Details | `Details` | scaffolder |
| Delete | `Delete` + `GM_Toolbox.Models.Oracle` | scaffolder |

Every page written by hand names **what the user is looking at**. Every scaffolded page names
**the action method that produced it**. Nobody decided that; it fell out of the scaffolder
templating `<h1>@ViewData["Title"]`-shaped text from the action name, and of nobody reading it
afterwards.

`GM_Toolbox.Models.Oracle` is the extreme case — a fully-qualified .NET class name printed as
a heading on a destructive confirmation page, in an app in daily use.

This is v1.1's principle 10 ("check what the scaffolder actually wrote") again, and the second
time it has produced a real defect. v1.1 found it in `Edit`'s `_context.Update()`, which was a
data-loss bug. Here it is cosmetic — but the pattern is identical: **generated code is a first
draft that nobody has read.** It compiles, it runs, it demos fine, and it is the last thing
anyone opens.

Headings are now the oracle's name on `Details`, and singular action phrases elsewhere —
"Create Oracle", not "Create Oracles". Each of those pages acts on exactly one oracle, and the
plural reads like a bulk operation the app does not have.

### The empty state

`Index` rendered a table head with no rows for a brand-new install: three column headers and
nothing under them. It now says "No oracles yet. Use **Create New** to build your first one."

The same theme as §1 — the app knew the list was empty and rendered a shape that made it look
like something had failed to load.

### Loading what you do not display

`Delete`'s confirmation page was going to report how many outcomes it was about to destroy, so
`Delete` GET was switched to `LoadOracleWithEntries` to have the count available. Then the
count itself was cut as noise on a page that already names the oracle and asks "are you sure".

That left `.Include(o => o.OracleEntries)` pulling up to 78 rows out of the database for a page
that displayed none of them, so the loader change was reverted with it.

**A query exists to serve a rendering.** When the rendering goes, the query goes with it — and
it will not announce itself, because loading extra rows produces no error, no warning, and no
visible symptom. This is the exact inverse of §2's bug: that page knew something and refused to
say it; this one would have fetched something it never used. Both are the data layer and the
view layer disagreeing about what is needed.

### Typos and stale text

Six typos in comments and user-facing copy: "permently delets" on the Clear All confirmation,
and "signiture", "EditEntires", "destory", "destoryed", "reseededa" in controller comments.

More interesting than the typos: `Views/Home/Index.cshtml` still listed "Current Features:
Bootable Home Page" and "In progress: Oracle Builder — create a new oracle table". Everything
in v1.0 and v1.1 — the whole builder, dice and cards, export, import, Clear All — had shipped
without the home page noticing. **Documentation drifts silently because nothing tests it.** The
compiler cannot check a feature list, and neither can 119 unit tests. The only defence is to
treat the page as part of the feature the way v1.0 §10 rule 4 treats navigation.

---

## 6. Change 5 — consistency

**Status: done.**

### Consistency is not uniformity

The observation that started this step: only `Edit.cshtml` loaded `_ValidationScriptsPartial`,
so four other forms had `asp-validation-summary` markup with no client-side validation behind
it. The obvious move is to paste the `@section Scripts` block into all four.

That would have been wrong for three of them. What a form can validate on the client depends
entirely on what it binds:

| Form | Binds | Client-side validation |
|---|---|---|
| `Create` | `Oracle` | **Yes** — `Name` is `[Required, MaxLength(100)]` |
| `Edit` | `Oracle` | Yes — already had it |
| `Configure` | a loose `int? outcomeCount` parameter | **No** — no model, so no metadata to generate rules from |
| `Import` | `IFormFile` | **No** — same |
| `EditEntries` | `EditEntriesViewModel` | Yes, and that turned out to matter |

jQuery unobtrusive validation works from `data-val-*` attributes that the tag helpers emit
from the model's data annotations. A parameter that is not part of a bound model has no
annotations, so the scripts load and generate nothing. `Configure`'s number input is
constrained by `min`/`max`, but that is HTML5 form validation — a completely separate
mechanism that works with no scripts at all.

So `Create` got the scripts, and `Configure` and `Import` deliberately did not. **Uniformity
that adds a dead dependency to three pages is not consistency; it is cargo cult.** The
consistent rule is "every form gets what it needs", and it is only satisfied by looking at
each one.

### The bug that fell out of asking

`EditEntries` was the interesting case, because adding the scripts there would have surfaced a
rule that should not have existed.

`EntryRow.Result` was a **non-nullable `string`**. Under nullable reference types, a
non-nullable string on a bound model is implicitly `[Required]` — the exact gotcha v1.0's
appendix records as "The Name field is required" appearing on a form with no Name field. v1.0
framed the fix as "make display-only properties `string?`", and that framing is too narrow:
`Result` is not display-only. It is the field the whole page exists to collect.

Confirmed by hand: clearing one outcome on a d6 and saving returned "The Result field is
required."

Which makes a state the app itself produces unsaveable. `Configure` deliberately creates blank
rows — that is its entire job. A fresh tarot oracle therefore lands on `EditEntries` with 78
empty boxes and **cannot be saved until every one of them is filled**. Leave one blank and the
other 77 are lost.

It is also inconsistent with how the rest of the app treats the same data. v1.1 §4 established
that import must accept everything export can write, and export writes blank `Result` strings
for a configured-but-never-edited oracle. So the app can create this state, export it, and
import it, and only `EditEntries` refused to save it.

The fix, in two halves:

```csharp
// EntryRow — was: public string Result { get; set; } = string.Empty;
public string? Result { get; set; }
```

```csharp
// EditEntries POST — was: entry.Result = row.Result;
entry.Result = row.Result ?? string.Empty;
```

Nullable on the way in, because a blank box is legitimate input. Non-null on the way to the
entity, because `OracleEntry.Result` is a real column and "no text" is `""`, not `NULL`.
**The view model and the entity are allowed to disagree about nullability — that boundary is
what the view model is for.**

### Why 114 passing tests never saw it

This is the part worth carrying forward.

`EditEntriesActionTests` builds an `EditEntriesViewModel` in C# and calls the action directly.
That is the approach v1.0 §9 chose deliberately and it has caught real bugs. But **validation
attributes are applied during model binding**, and calling an action directly skips binding
entirely. `ModelState` starts empty and stays empty unless the action adds to it.

So the suite can never see an implicit-required rule, a `[MaxLength]` rejection, a failed type
conversion, or anything else that lives in the binder. Every one of the 12 `EditEntries` tests
passes on a code path the browser never takes.

That is not an argument against controller tests — it is an argument for knowing precisely
where their edge is. v1.0 §9 listed "model binding from form fields" under *not covered*. This
pass is what that line costs when it goes unread: a bug sitting on the happy path of the app's
most-used page, invisible to a green suite, found only by clearing a text box by hand.

### The way out of a page

`EditEntries` had a Save button and nothing else — no Cancel, no back link. Starting to edit a
78-row deck and changing your mind meant the browser's Back button or the nav bar.

`Details` had Configure, Edit and Back to List, but no Delete, making it the one page listing
an oracle from which you could not delete it.

Both fixed. v1.0 §10 rule 4 says navigation is part of the feature; that applies to the way
**out** of a page as much as the way in.

---

## 7. What changed in the test suite

**Status: done.**

v1.1 ended at 114 tests. This pass ends at **119, all green** — one new file, nothing deleted,
nothing changed in an existing test.

### `OracleEntryExtensionTests` — five tests

| Test | Locks in |
|---|---|
| `KeyLabel_CardEntry_IsTheCardLabel` | Cards are identified by label. |
| `KeyLabel_DiceEntryWithNoBand_SaysNotSet` | The configured-but-unbanded state renders as text, not an empty cell. |
| `KeyLabel_SingleNumberBand_IsJustTheNumber` | `6`, not `6-6`. |
| `KeyLabel_MultiNumberBand_IsStartToEnd` | `1-3`. |
| `KeyLabel_CardLabelWinsOverRanges` | Precedence: an entry carrying both is a card. |

No database, no context, no seeding — the same shape as `OracleSubtypeExtensionTests`, because
`KeyLabel()` is a pure function of one entry.

That last test is the one that would not have existed if the logic had stayed in the view.
Reading four `@if` branches in Razor, the precedence between "has a card label" and "has a
range" is an accident of ordering. Written as a method with a test named after it, the
precedence becomes a decision on the record.

### The honest coverage statement

**Five tests is the entire testable surface of this pass.** Everything else — the repaired
`<dl>`, the outcomes table, the empty state, the import banner, the headings, the Cancel
links, the validation scripts — is Razor markup, and this suite cannot reach Razor markup at
all.

That is not a gap to apologise for so much as a boundary to state plainly, because the
alternative is worse: a green suite that implies more than it checks. Every claim in §2, §3,
§5 and §6 about how a page renders was verified by loading it in a browser, and by nothing
else.

### The blind spot this pass found

§6 covers it in detail, and it belongs here too because it is a property of the *suite*, not of
any one test: **controller tests that construct a view model in C# and call the action
directly never exercise model binding, so they can never see binding-level validation.**

v1.0 §9 already listed "model binding from form fields" as not covered. The `EditEntries`
blank-outcome bug is what that line costs when nobody re-reads it — twelve passing tests on a
page whose most common real-world action was rejected by the browser.

Worth knowing the shape of the fix, even though it was not taken here: an integration test
using `WebApplicationFactory` drives a real HTTP request through the real pipeline, binding
included. That is a different and heavier kind of test than anything in this suite, and adding
one for a bug already fixed is not obviously the right trade. **Recording the limit is the
cheap half; the expensive half can wait until it is earning its cost.**

---

## 8. Corrections to v1.0 and v1.1

Apply these on top of the reference sections in the earlier documents.

**v1.0 §6, `EditEntriesViewModel`** — `EntryRow.Result` is now `public string? Result { get; set; }`,
with no initialiser. The controller writes `entry.Result = row.Result ?? string.Empty;`. See
§6 above for why.

**v1.0 appendix, "The Name field is required with no Name field"** — the stated fix, "make
display-only properties `string?`", is too narrow and reads as though the rule only bites on
properties the form does not post. It bites on **any** non-nullable reference-typed property on
a bound model, including ones the form very much does post. The question to ask is not "is this
display-only?" but "is an empty value legitimate here?" — and if it is, the property must be
nullable regardless of whether it is displayed, posted, or both.

**v1.1 appendix, `MSB3027: could not copy … locked by "IIS Express Worker Process"`** — the
fix column claims "Kestrel profiles (`http`/`https`) do not leave one behind the way IIS
Express does". That is wrong. A Kestrel `dotnet run` holds exactly the same lock on
`bin\Debug\net10.0\GM_Toolbox.dll`, and it blocked `dotnet test` twice during this pass. The
correct rule is simpler and has no exceptions: **any running instance of the app locks the
build output.** Stop the app before building or testing, whichever profile launched it. The
error message names the locking process and its PID, which is the fastest way to confirm what
is holding it.

**v1.1 §6, on the `TempData` test stub** — the note that `TestControllers` needed a
`TempDataDictionary` for Import's success message now has a rendered message behind it. Until
this pass it did not; see §3.

**v1.0 §5 and v1.1 §7, routes table** — one changed row:

| Verb | Route | What it does now |
|---|---|---|
| GET | `/Oracles/Details/5` | Loads via `LoadOracleWithEntries` and renders the oracle's outcomes — dice bands or card names, plus an empty state and an unbanded warning. |

`Delete` GET is unchanged and still loads without entries; it was briefly switched to the
loader and reverted (§5).

**v1.0 §9, "Coverage"** — add `OracleEntryExtension` to the covered list. The *not covered*
list is unchanged and is more load-bearing than it looks; see §7.

---

## 9. Principles this pass added

Continuing v1.1 §8, which ended at 14.

15. **Work the user cannot see is not finished.** Not unpolished — unfinished. `Details` was
    redirected to, loaded, and rendered without ever showing the outcomes it existed to
    confirm.
16. **Logic in a view is logic no test can reach.** The suite can call a method or an action;
    it cannot reach Razor markup. Anything with branches wants to live somewhere a test can
    call it, and moving it there is worth doing even when the branch count is unchanged.
17. **Before adding a line to fix something, check whether the fix is already a helper.**
    `Details` needed `.Include`; what it actually needed was the loader its three sibling
    actions were already using.
18. **Consistency is not uniformity.** Four forms lacked the validation scripts and only one
    of them benefited from having them. The consistent rule is "every form gets what it
    needs", which can only be satisfied by looking at each one. Pasting the same block
    everywhere adds a dead dependency and calls it a standard.
19. **A test that skips model binding cannot see binding-level validation.** Constructing a
    view model in C# and calling the action leaves `ModelState` empty by construction, so
    implicit-required rules, `[MaxLength]`, and failed conversions are all invisible. Twelve
    green `EditEntries` tests sat above a bug on that page's most common action.
20. **A query exists to serve a rendering; when the rendering goes, the query goes.** Loading
    rows nobody displays produces no error and no warning. It is the same disagreement between
    the data layer and the view layer as failing to load rows you do display — just pointing
    the other way.
21. **Generated code is a first draft nobody has read.** v1.1 found this in `Edit`'s
    `_context.Update()` and lost data to it. This pass found it in nine page headings, one of
    which printed a .NET class name at a user about to delete something. Same root cause, two
    very different severities — which is exactly why it is worth checking before it is the
    severe one.

---

## 10. Still open

Carried forward from v1.1 §9, unchanged:

- **The roll/draw play page.** The whole point of an oracle, still unbuilt. Explicitly out of
  scope for v1.2 — see **§11** for what the next session should read first.
- **Multi-user.** v1.1 §10 maps the seven places that change. Out of scope for v1.2.
- **Parking lot: importing from the vault** — listing `App_Data/Exports` server-side rather
  than uploading. Needs a path-traversal guard.
- **Deferred from v1.0, unchanged:** tarot reversed, partial decks, weighted outcomes,
  free-form ranges, sub-tables, sharing, entries-form pagination.

Noted during this pass, deliberately not acted on:

- `OracleListItem.NeedsSetup` treats a card oracle with 52 blank `Result` strings as finished.
  That is arguably correct — the deck exists, and blank text is a draft rather than a broken
  state — and changing it would mean projecting a third count out of the `Index` query.
  Recorded rather than changed.

---

## 11. Looking ahead — v1.3, rolling and drawing

**Status: not started.** This section is a starting point for the next session, not a plan
that has been agreed. Nothing below is decided.

Three versions in, an oracle can be built, edited, exported, imported and now *read* — and it
still cannot be **used**. v1.3 is the payoff: roll the die or draw the card and get an outcome
back.

### What already exists, and what it means

The good news is that this needs **no schema change and no migration**. Everything a roll
requires is already stored.

**Dice bands are guaranteed contiguous.** This is the most useful thing to know going in, and
it is worth trusting rather than re-deriving. `EditEntries` POST enforces four rules together:
the first row must start at 1, every start must be between 1 and `FaceCount()`, each start
must be strictly greater than the one above it, and `RangeEnd` is computed as
`next.StartsAt - 1` with the last band ending at `FaceCount()`. Taken together those mean the
bands of a saved dice oracle **exactly tile `1..FaceCount()` with no gaps and no overlaps.**

So the lookup is a single unambiguous match, not a search that might fail:

```
roll a number 1..FaceCount(), find the one entry where RangeStart <= roll <= RangeEnd
```

There is exactly one. That invariant is worth a comment wherever the roller relies on it,
because it comes from validation in a completely different file.

**Cards need no bands at all.** A draw picks one entry from the set; `CardCode` identifies it
and `CardLabel` names it.

**`KeyLabel()` (§2) already renders the result.** Whatever gets rolled or drawn, the thing to
show next to the outcome text is `entry.KeyLabel()` — `1-3` or `The Fool`. That method was
built for the Details table and this is the reuse it was predicted to have.

### The decision to get right first: where the randomness lives

This is the one that is expensive to change later, so make it deliberately.

`new Random()` inside a controller action or a service method makes the whole feature
**untestable** — every test would be asserting on a number nobody chose. A test suite that
cannot check "a roll of 3 returns the second outcome" is not covering the feature at all, it
is only covering that a page renders.

The shape that stays testable is to separate **choosing a number** from **finding the entry**.
Finding the entry given a number is a pure function and should be trivially testable with no
randomness anywhere near it. Choosing the number is the part that needs to be substitutable in
a test — by seeding, by injection, or by passing the number in.

Getting this wrong is the difference between v1.3 being covered like `OracleConfigurator` and
covered like the Razor markup in §7.

### The other decisions, unresolved

**Does a roll happen on GET or POST?** A roll changes nothing in the database, which argues for
GET. But Post/Redirect/Get (v1.0 §1) exists so that a refresh does not repeat an action — and
here, repeating the action on refresh is arguably the *desired* behaviour for a die. If the
answer is POST, note that redirecting afterwards means carrying the result through `TempData`,
exactly as §3 does for the import banner.

**Does a draw put the card back?** "Roll a die" is obviously with replacement. "Draw a card"
usually is not — and drawing without replacement means a deck that remembers what has already
gone, which is per-user state that survives requests. That is a materially bigger feature than
rolling, and it is worth deciding explicitly rather than discovering halfway in. With
replacement is the small version; a persistent shuffled deck is the large one.

**What happens to an oracle that is not ready?** A dice oracle with unbanded entries has no
valid roll, and an oracle with no entries has nothing to return. The list page already answers
this question — see below.

### The thing to fix on the way in

`NeedsSetup` currently lives on `OracleListItem`, a view model built for the `Index`
projection. The roll page needs the identical question answered about a loaded `Oracle`, and a
"Roll" link on the list page needs it too.

Copying that logic into a second place is precisely the mistake v1.1 §1 was written about — two
copies of one rule, which will eventually disagree. The move is to promote it to an extension
method alongside `Category()`, `FaceCount()` and `KeyLabel()`, and have the view model call it
rather than reimplement it.

Do that **before** building the roll page, not after. It is a five-minute change while there is
one caller and an awkward one once there are three.

### Scope guardrails

Things that will feel like part of this feature and are not, unless deliberately chosen:
reversed tarot, weighted outcomes, drawing several cards at once, and a history of past rolls.
Each is a separate decision with its own storage implications. v1.0 §10 rule 3 — one path
end-to-end before the next — applies: get a d6 rolling and displaying before touching cards,
and cards working before anything else.

---

## Appendix — new gotchas

Append to v1.0's and v1.1's tables.

| Symptom | Cause | Fix |
|---|---|---|
| Some rows of a Bootstrap grid line up and others do not | `col-*` classes only form columns inside a flex container; an unbalanced `</dl>` had dropped later pairs outside the `row` | Balance the tags. If a grid class does nothing, check its parent is the element it needs |
| Three `<dl>` elements where one was meant | Counting the *pairs* instead of the *list* — one `<dl>` holds many `<dt>`/`<dd>` items, exactly as one `<ul>` holds many `<li>` | One list element wrapping all items |
| `'OracleSubtype' has no definition for 'Category'` in a view | Extension methods are only visible when their namespace is in scope; `_ViewImports.cshtml` imports `GM_Toolbox.Models` but not `.Enums` | `@using GM_Toolbox.Models.Enums` at the top of the view. "No definition" on an extension method usually means a missing `using` |
| A `.cshtml` edit appears to have no effect — no error, page behaves as though the change is not there | Runtime compilation of Razor views is off by default in .NET 6+; views compile into the assembly at build time, so a browser refresh serves the previously compiled view | Rebuild and restart before debugging any view change. This looks identical to a logic bug |
| A confirmation message set before a redirect never appears | `ViewData`/`ViewBag` die with the request, and a POST that redirects is two requests | `TempData` — cookie-backed, survives the redirect, read-once so it does not reappear on refresh |
| `CS8603: Possible null reference return` on `someNullable.ToString()`, right after a null check | `.ToString()` on `int?` binds to `object.ToString()`, annotated `string?`. The warning is about the called method's declared return type, not about the value | Unwrap first: `.Value.ToString()` — `int.ToString()` is annotated non-null. Safe only where the null case has already returned |
| "The X field is required" on a field the user deliberately left blank | Any non-nullable reference-typed property on a bound model is implicitly required — not just display-only ones. Sharpens the v1.0 appendix row | Ask "is an empty value legitimate here?", not "is this displayed?". If empty is legitimate, make it `string?` and coalesce when writing to the entity |
| Client-side validation scripts added to a form change nothing | jQuery unobtrusive validation is driven by `data-val-*` attributes generated from a bound model's data annotations. A loose action parameter or an `IFormFile` has none | Only add `_ValidationScriptsPartial` to forms that bind an annotated model. `min`/`max` on a number input is HTML5 validation, a separate mechanism needing no scripts |
| `dotnet test` fails with `MSB3027 … file is locked` | **Any** running instance of the app locks `bin\Debug\...\GM_Toolbox.dll` — Kestrel `dotnet run` included, not just IIS Express as v1.1's appendix claims | Stop the app before building or testing. The error names the locking process and PID |
| `ERR_CONNECTION_REFUSED` on a URL that worked a minute ago | The app was stopped — often to release the build lock above — and never restarted. Looks like a broken route | Check the app is running before debugging the page |
