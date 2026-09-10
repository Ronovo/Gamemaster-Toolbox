# GM Toolbox

A web-based toolkit for tabletop gamemasters.

Built as an ASP.NET Core MVC application on .NET 10, with Entity Framework Core and SQLite.
There is no database server to install: your whole toolbox is one file at
`App_Data/gmtoolbox.db`.

## Features

- **Game Systems**
- **Oracle Builder**
- **Rolling and Drawing**
- **Flows** — chain oracles together and run them a step at a time. Steps run in order, each
once; any outcome can carry a *branch* that sends the run to another step or stops it early,
so a flow can fork, skip ahead, loop back, or end. A step can also **ask you something**
rather than roll — a dropdown you author, or a line of text — and a dropdown answer can
decide which table the *next* step rolls on.
- **Export / Import** — download every oracle and flow as a single versioned JSON file, and
read one back in. The format carries no database IDs, so a file exported from one instance
imports cleanly into another. Exports are also archived server-side under `App_Data/Exports/`.
- **Accounts** — ASP.NET Core Identity is wired up for register and login. Nothing is
restricted to a signed-in user yet: oracles are global and an account is currently just a
name, so this is groundwork for per-owner oracles rather than a working permission system.
Email confirmation is off, because there is no mail sender configured to confirm with.



## Getting started

Two ways in. Pick whichever you already have installed — you get the same app on
**[http://localhost:9000](http://localhost:9000)** either way.

### With Docker (To Host Locally)

```bash
docker compose up -d
```

Open **[http://localhost:9000](http://localhost:9000)**.

To stop it: `docker compose down`

### With the .NET SDK (To Develop)

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run
```

Open **[http://localhost:9000](http://localhost:9000)**.

To stop it: `dotnet run`

### Letting other people in

Use the Docker. It publishes the port on every network interface, so anyone on the same
network reaches it at `http://<your-machine's-ip>:9000` — same port, just your machine's
address instead of `localhost`. Windows will most likely ask you to allow it through the
firewall the first time, and if you dismiss that prompt it keeps working for you and silently
fails for everyone else.

`dotnet run` deliberately binds to `localhost` only, so nobody else can reach it no matter
which port they try.

That is as far as it is meant to go. There are no permissions yet — everyone who can reach it
can edit everything — so this is built for a machine on **your network**, shared with people
who are **in the room** with you. 

Exposing it to the open internet is not something it is set up
for, and you would be on your own doing it.

## Sharing or moving your toolbox

Currently Supported Import/Export Systems:

- Game Systems
- Oracles
- Flows



### How an oracle is put together

An `Oracle` is a name, a `GameSystem`, and an `OracleSubtype`. The subtype is just the
kind of oracle — `Dice`, `PlayingCards` or `TarotCards`. `Category()` folds that to dice
or cards, and that in turn decides how its `OracleEntry` rows are shaped:


| Category | Keyed by                  | Rows come from                                                               |
| -------- | ------------------------- | ---------------------------------------------------------------------------- |
| Dice     | `RangeStart` / `RangeEnd` | The outcome count you ask for; you set each band's start, the end is derived |
| Cards    | `CardCode` / `CardLabel`  | `DeckFactory`, which builds the whole deck                                   |


Every dice oracle stores its die in two columns, `DiceCount` and `DiceFaces` — a plain d8
is `1` and `8`, a 2d6 is `2` and `6`. They are the single source of truth for the die
(null only on the two card kinds); the type picker's "d4…d100" presets are just a shortcut
that fills them in. `DiceSpec` is the one place they are read, and it also answers what a
set of dice can roll: a 2d6 has a minimum of 2, a maximum of 12 and eleven distinct
totals, so eleven outcomes fit on it and the first band has to start at 2 rather than 1.
A card draw deals off a shuffled list of that oracle's entries held in your browser session.
Which cards are left is per-person and per-sitting, and is never exported, imported or saved to the database.

### How a flow is put together

A `Flow` is a name, a `GameSystem`, and an ordered list of `FlowStep` rows. Every step carries a
`Label` — what the flow calls that step, for when "d6 Encounter Table" reads better as
"Encounter Type" in this particular chain — and a `Kind`, which decides what the step actually
does and therefore which of its other fields apply:


| Kind         | What it does                                                 |
| ------------ | ------------------------------------------------------------ |
| `Roll`       | Rolls the oracle it names, once. Each outcome can also **pick a table** for a later `RollChosen` step. |
| `Choice`     | Asks a question and offers a dropdown of options you author. An option can name an oracle. |
| `Text`       | Asks a question and takes a typed answer (including a number). |
| `RollChosen` | Rolls whichever oracle an earlier step picked — a `Choice` option or a `Roll` outcome. |


A flow is a **flat list**. Running it works through one step at a time, in order; when a step
rolls or is answered the run moves to the next one. That fall-through is the whole of the
default behaviour: a flow with three `Roll` steps and nothing else set up is simply those
three oracles, rolled in order.

**A dependent chain** — roll A, then roll whatever A landed on — is two steps. Give the first
`Roll` a `FlowBranch` on each outcome that names the table that outcome should lead to, then a
`RollChosen` pointed back at that step.

A `Roll` step (or a `Choice` option) can also route the run instead of just carrying on (this
is advanced; picking a table is the ordinary use):

| The branch says     | What the run does                                            |
| ------------------- | ----------------------------------------------------------- |
| Go to step *n*      | Jumps there, forwards or backwards; the run then continues from wherever it lands |
| End the flow        | Stops after this step                                        |
| *nothing*           | Falls through to the next step                               |

A run still stops after 200 steps whatever it is doing, so a branch that points backwards
cannot loop forever.

An older file still imports. `"kind": "Number"` and `"kind": "Fields"` both become `Text` (a
Fields step's labels become the question text); a `"kind": "Loop"` is dropped and its body
steps kept in sequence; `times`, `countMode` and `countDice` are ignored. A version 6 or 7
file (flat `steps`, `blockEnd` spans, `goToStep` by position) imports the same way.

### JSON export format

```json
{
  "formatVersion": 10,
  "exportedUtc": "2026-09-08T11:08:28Z",
  "gameSystems": [
    { "name": "Dungeons & Dragons", "genre": "Fantasy" }
  ],
  "oracles": [
    {
      "name": "Wilderness Encounter",
      "gameSystem": "Dungeons & Dragons",
      "subtype": "Dice",
      "diceCount": 1,
      "diceFaces": 20,
      "entries": [
        { "sortOrder": 1, "rangeStart": 1, "rangeEnd": 10, "result": "Nothing happens." },
        { "sortOrder": 2, "rangeStart": 11, "rangeEnd": 20, "result": "A patrol spots you." }
      ]
    }
  ],
  "flows": [
    {
      "name": "Night market",
      "gameSystem": "Cyberpunk Red",
      "description": "Which role, then that role's stalls.",
      "steps": [
        {
          "id": 1,
          "kind": "Roll",
          "label": "Encounter Type",
          "oracle": { "name": "Wilderness Encounter", "gameSystem": "Dungeons & Dragons" },
          "branches": [
            { "whenOutcome": 1, "picksOracle": { "name": "Hustle - Nomad", "gameSystem": "Cyberpunk Red" } },
            { "whenOutcome": 2, "picksOracle": { "name": "Hustle - Solo",  "gameSystem": "Cyberpunk Red" } }
          ]
        },
        {
          "id": 2,
          "kind": "RollChosen",
          "label": "Stall",
          "pickedAtStep": 1
        },
        {
          "id": 3,
          "kind": "Choice",
          "label": "Haggle?",
          "question": "Do you haggle?",
          "options": [
            { "sortOrder": 1, "label": "Yes" },
            { "sortOrder": 2, "label": "No", "goToStep": 4 }
          ]
        },
        {
          "id": 4,
          "kind": "Text",
          "label": "The trader",
          "question": "Name, look, what they want"
        }
      ]
    }
  ]
}
```

Version 10 makes a flow a flat list: `steps` no longer nest, every step has a per-file `id`
(its position) that `pickedAtStep` and `goToStep` point at, and the `Loop` / `Fields` kinds
and `times` / `countMode` / `countDice` fields are gone. Older files still import — a `Loop`
is unwrapped, a `Fields` step becomes a `Text` question, and version 6/7 `blockEnd` spans are
discarded.



## Configuration

Nothing here needs changing to run the app. It is written down for people who want to move
things around.


| Setting                               | Default                             | What it does                                                                                                                                                                                                                                                                                                                    |
| ------------------------------------- | ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:DefaultConnection` | `Data Source=App_Data/gmtoolbox.db` | Where the database file lives. A relative path is resolved against the app's content root, so it lands in `App_Data/` no matter which folder you launched from. An absolute path is respected as-is.                                                                                                                            |
| `ForceHttps`                          | `false`                             | Off, because the default setup has no certificate to redirect to — the container serves plain HTTP, and bouncing to a port nothing is listening on would just break it. Turn it on only where the app itself holds a certificate. Leave it off behind a reverse proxy that terminates TLS, which has already done the redirect. |


Change either by editing `appsettings.json`, or without touching the file, with an environment
variable — `ForceHttps=true`, or `ConnectionStrings__DefaultConnection` (two underscores, which
is how `:` is written in an environment variable). `dotnet user-secrets` works too, but only
when running with the SDK, not in the container.

## Project layout

```
Controllers/     HomeController, OraclesController (all oracle CRUD, export, import),
                 FlowsController (flow CRUD, the step builder, branches, running a flow)
Models/          Oracle, OracleEntry, Flow, FlowStep, FlowBranch, FlowChoiceOption,
                 enums, view models, JSON transfer DTOs
Services/        OracleConfigurator (entry reconciliation), DeckFactory (deck generation),
                 OracleExporter / OracleImporter / FlowExporter / FlowImporter / OracleJson
                 (file round-trip), OracleRoller and FlowRunner (rolling, answering, the walk),
                 FlowConfigurator (the step list: order, options, branches)
Data/            ApplicationDbContext and EF Core migrations
Views/           Razor views for Home, Oracles and Flows, plus the shared _RollPanel partial
Areas/Identity/  Scaffolded ASP.NET Core Identity UI
App_Data/        gmtoolbox.db (the database) and Exports/ (server-side archive of JSON exports)
wwwroot/        Static assets (Bootstrap, jQuery, site CSS/JS, roll-panel.js)
```



## Tech stack

- ASP.NET Core MVC on .NET 10
- Entity Framework Core 10 (SQLite provider)
- Bootstrap 5 + jQuery

