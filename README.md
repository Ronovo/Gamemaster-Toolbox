# GM Toolbox

A web-based toolkit for tabletop gamemasters.

Built as an ASP.NET Core MVC application on .NET 10, with Entity Framework Core and SQLite.
There is no database server to install: your whole toolbox is one file at
`App_Data/gmtoolbox.db`.

## Features
- **Game Systems** - Add the name of any game and set a custom genre.
- **Oracle Builder** — create an oracle, pick a game system and a type, and the app
  generates the outcome rows for you.
  - *Dice Oracles* — any die from a d2 to a d100, one or several at once (2d6, 3d10, …).
    The type picker offers the common dice as one-click presets; "Custom" is there for a
    d3, a d7, or a pool of several.
- **Rolling and Drawing**
- **Export / Import** — download every oracle as a single versioned JSON file, and read one
  back in. The format carries no database IDs, so a file exported from one instance imports
  cleanly into another. Exports are also archived server-side under `App_Data/Exports/`.
- **Accounts** — ASP.NET Core Identity is wired up for register and login. Nothing is
  restricted to a signed-in user yet: oracles are global and an account is currently just a
  name, so this is groundwork for per-owner oracles rather than a working permission system.
  Email confirmation is off, because there is no mail sender configured to confirm with.

## Getting started

Two ways in. Pick whichever you already have installed — you get the same app on
**http://localhost:9000** either way.

### With Docker (To Host Locally)

```bash
docker compose up -d
```

Open **http://localhost:9000**.

To stop it: `docker compose down`

### With the .NET SDK (To Develop)

Needs the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run
```

Open **http://localhost:9000**.

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
- Oracles 

### How an oracle is put together

An `Oracle` is a name, a `GameSystem`, and an `OracleSubtype`. The subtype is just the
kind of oracle — `Dice`, `PlayingCards` or `TarotCards`. `Category()` folds that to dice
or cards, and that in turn decides how its `OracleEntry` rows are shaped:

| Category | Keyed by | Rows come from |
| --- | --- | --- |
| Dice | `RangeStart` / `RangeEnd` | The outcome count you ask for; you set each band's start, the end is derived |
| Cards | `CardCode` / `CardLabel` | `DeckFactory`, which builds the whole deck |

Every dice oracle stores its die in two columns, `DiceCount` and `DiceFaces` — a plain d8
is `1` and `8`, a 2d6 is `2` and `6`. They are the single source of truth for the die
(null only on the two card kinds); the type picker's "d4…d100" presets are just a shortcut
that fills them in. `DiceSpec` is the one place they are read, and it also answers what a
set of dice can roll: a 2d6 has a minimum of 2, a maximum of 12 and eleven distinct
totals, so eleven outcomes fit on it and the first band has to start at 2 rather than 1.
A card draw deals off a shuffled list of that oracle's entries held in your browser session.
Which cards are left is per-person and per-sitting, and is never exported, imported or saved to the database.

#### JSON export format

```json
{
  "formatVersion": 2,
  "exportedUtc": "2026-09-03T11:08:28Z",
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
  ]
}
```

Both `gameSystem` and `subtype` are written as strings rather than numbers, so a file stays
readable and stays valid across versions — but they are not the same kind of string.

`gameSystem` is the free-text name of a game system as you typed it into the **Game Systems**
tab. On import it is matched against your existing systems by name, case-insensitively, and if
nothing matches, that system is created for you with its genre set to "Unknown". So importing
someone else's file never fails because you don't happen to run their game — you just get their
system added, ready to rename or fill in.

`subtype` is a fixed set — `Dice`, `PlayingCards`, `TarotCards`. An unrecognised value
there is a real error, and import stops with a message naming the oracle rather than
crashing on deserialisation. Allows for addition of oracles in the future.

A `Dice` oracle also carries `diceCount` and `diceFaces` (both required, held to the same
1–10 / 2–100 rules the form uses); the two card kinds write neither. Collapsing the old
per-size subtypes (`D4`…`D100`, `Custom`) into `Dice` plus those two fields is the change
that took the format to version 2; version 1 files no longer import.

Entries carry either the dice fields or the card fields, never both.

## Configuration

Nothing here needs changing to run the app. It is written down for people who want to move
things around.

| Setting | Default | What it does |
| --- | --- | --- |
| `ConnectionStrings:DefaultConnection` | `Data Source=App_Data/gmtoolbox.db` | Where the database file lives. A relative path is resolved against the app's content root, so it lands in `App_Data/` no matter which folder you launched from. An absolute path is respected as-is. |
| `ForceHttps` | `false` | Off, because the default setup has no certificate to redirect to — the container serves plain HTTP, and bouncing to a port nothing is listening on would just break it. Turn it on only where the app itself holds a certificate. Leave it off behind a reverse proxy that terminates TLS, which has already done the redirect. |

Change either by editing `appsettings.json`, or without touching the file, with an environment
variable — `ForceHttps=true`, or `ConnectionStrings__DefaultConnection` (two underscores, which
is how `:` is written in an environment variable). `dotnet user-secrets` works too, but only
when running with the SDK, not in the container.

## Project layout

```
Controllers/     HomeController, OraclesController (all oracle CRUD, export, import)
Models/          Oracle, OracleEntry, enums, view models, JSON transfer DTOs
Services/        OracleConfigurator (entry reconciliation), DeckFactory (deck generation),
                 OracleExporter / OracleImporter / OracleJson (file round-trip)
Data/            ApplicationDbContext and EF Core migrations
Views/           Razor views for Home and Oracles
Areas/Identity/  Scaffolded ASP.NET Core Identity UI
App_Data/        gmtoolbox.db (the database) and Exports/ (server-side archive of JSON exports)
wwwroot/         Static assets (Bootstrap, jQuery, site CSS/JS)
```


## Tech stack

- ASP.NET Core MVC on .NET 10
- Entity Framework Core 10 (SQLite provider)
- Bootstrap 5 + jQuery
