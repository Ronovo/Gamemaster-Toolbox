# GM Toolbox

A web-based toolkit for tabletop gamemasters.

Built as an ASP.NET Core MVC application on .NET 10, with Entity Framework Core and SQLite.
There is no database server to install: your whole toolbox is one file at
`App_Data/gmtoolbox.db`.

## Features
- **Game Systems** - Add the name of any game and set a custom genre.
- **Oracle Builder** — create an oracle, pick a game system and a subtype, and the app
  generates the outcome rows for you.
  - *Dice oracles* (D4, D6, D8, D10, D12, D20, D100)
  - *Card oracles*
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

An `Oracle` is a name, a `GameSystem`, and an `OracleSubtype`. The subtype decides its
category — dice or cards — and that in turn decides how its `OracleEntry` rows are shaped:

| Category | Keyed by | Rows come from |
| --- | --- | --- |
| Dice | `RangeStart` / `RangeEnd` | The outcome count you ask for; you set each band's start, the end is derived |
| Cards | `CardCode` / `CardLabel` | `DeckFactory`, which builds the whole deck |

#### JSON export format

```json
{
  "formatVersion": 1,
  "exportedUtc": "2026-09-03T11:08:28Z",
  "oracles": [
    {
      "name": "Wilderness Encounter",
      "gameSystem": "Dungeons & Dragons",
      "subtype": "D20",
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

`subtype` is a fixed set (`D4`…`D100`, `PlayingCards`, `Tarot`). An unrecognised value there is
a real error, and import stops with a message naming the oracle rather than crashing on
deserialisation.

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
