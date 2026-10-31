# GM Toolbox

A web-based toolkit for tabletop gamemasters.

Built as an ASP.NET Core MVC application on .NET 10, with Entity Framework Core and SQLite.
There is no database server to install: everything lives in `App_Data/`, the database
`gmtoolbox.db` plus the uploaded sheet PDFs in `Sheets/`.

## Features

- **Game Systems**
- **Oracle Builder**
- **Rolling and Drawing**
- **Flows** - chain oracles together and run them a step at a time. Steps run in order, each
once; any outcome can carry a *branch* that sends the run to another step or stops it early,
so a flow can fork, skip ahead, loop back, or end. A step can also **ask you something**
rather than roll - a dropdown you author, or a line of text - and a dropdown answer can
decide which table the *next* step rolls on.
- **Sheets** - upload character sheet PDFs, with a version history when you upload a new copy
under the same file name. **Export All** zips your sheets.
- **Export / Import** - download your oracles and flows as a single versioned JSON file to your
computer, and upload one back in. The format carries no database IDs, so a file exported from one
account or instance imports cleanly into another. Nothing is kept on the server: the file is yours.
- **Accounts** - a username and password, plus a security question picked when registering.
"Forgot your password?" resets it with the answer; five wrong answers lock resets for 15
minutes. Logged out, only the About and login pages open; everything else needs an account. An
account made through portfolio login has no password here and no security question.
- **Private by design** - every game system, oracle, flow and sheet belongs to the account that
made it, and nobody else can see or use it. To share, export the file and send it. Deleting your
account (under your account's **Personal data**) deletes everything in it, sheet files included.
- **Log in with a portfolio account** - with `PORTFOLIO_SSO_URL` (the portfolio's address) and
`PORTFOLIO_SSO_PUBLIC_KEY` (from the portfolio's `python sso.py`) set, the login page offers to log in
with a portfolio account. It reads **Log in as** and the portfolio username when this browser is logged in
to the portfolio. The first portfolio login here asks whether to link an account the player already has,
by logging in to it once, or to make a new one named after the portfolio username. Every later portfolio
login opens the linked account. Without them the app runs on its own accounts.



## Getting started

Two ways in. Pick whichever you already have installed - you get the same app on
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

To stop it: press Ctrl+C.

To run the tests: `dotnet test Tests/GM_Toolbox.Tests.csproj`.

The browser scripts in `wwwroot/js` have their own tests in `Tests/js`, run with Vitest on Node.js. From
`Tests/js`, run `npm install` once, then `npm test`.

#### Coverage

```
dotnet test Tests/GM_Toolbox.Tests.csproj --collect:"XPlat Code Coverage" --results-directory TestResults
dotnet tool install dotnet-reportgenerator-globaltool --version 5.5.11 --tool-path .tools
.tools/reportgenerator -reports:"TestResults/**/coverage.cobertura.xml" -targetdir:coverage/dotnet -reporttypes:Html
cd Tests/js && npm run coverage
```

The HTML reports go to `coverage/dotnet` and `coverage/js`; the JavaScript one fails below 100%. The Docker image
builds both reports and serves them at `/coverage/`, open to everyone, under the landing page in
`coverage-index/`, the `ronovos-coverage-index` submodule, with this repo's `coverage-reports.json`. Failing tests
or low coverage don't stop the image build; the reports show what is missing.

### Updates in Docker

On every start the container runs `docker/git-sync.sh` before the app. It fetches the latest
commit on GitHub `main` into `/src`, so a pushed commit goes live on the next restart without
rebuilding the image. When that commit hasn't been built yet it republishes the app first, and a
failed build is tried again on the next start. If GitHub can't be reached the app starts on the
code it already has.

| Variable      | What it does                                                         | Default in the image                        |
| ------------- | -------------------------------------------------------------------- | ------------------------------------------- |
| `REPO_URL`    | The repo to pull. Set it empty to run exactly what is in the image.   | This repo on GitHub                          |
| `REPO_BRANCH` | Branch to pull                                                       | `main`                                       |
| `SYNC_DIR`    | Folder the code is pulled into                                       | `/src`                                       |
| `BUILD_CMD`   | Runs after pulling a commit that hasn't been built yet               | `dotnet publish` into `/app`                 |

### Letting other people in

Use the Docker. It publishes the port on every network interface, so anyone on the same
network reaches it at `http://<your-machine's-ip>:9000` - same port, just your machine's
address instead of `localhost`. Windows will most likely ask you to allow it through the
firewall the first time, and if you dismiss that prompt it keeps working for you and silently
fails for everyone else.

`dotnet run` deliberately binds to `localhost` only, so nobody else can reach it no matter
which port they try.

That is as far as it is meant to go. Every account's items are private to it, but this is still
built for a machine on **your network**, shared with people who are **in the room** with you.

Exposing it to the open internet is not something it is set up
for, and you would be on your own doing it.

## Sharing or moving your toolbox

Currently Supported Import/Export Systems:

- Game Systems
- Oracles
- Flows

**Export** downloads a JSON file to your computer: everything, all oracles, or the oracles or flows
you pick. **Import** uploads one (up to 5 MB) and adds what is in it to your account. To share with
another gamemaster, send them the file. The Import page also has a **Download an example file**
link, which serves `ExportTemplate/CPR - Hustles and Night Markets.json`, so there is always
something to try.

### How an oracle is put together

An `Oracle` is a name, a `GameSystem`, and an `OracleSubtype`. The subtype is just the
kind of oracle - `Dice`, `PlayingCards` or `TarotCards`. `Category()` folds that to dice
or cards, and that in turn decides how its `OracleEntry` rows are shaped:


| Category | Keyed by                  | Rows come from                                                               |
| -------- | ------------------------- | ---------------------------------------------------------------------------- |
| Dice     | `RangeStart` / `RangeEnd` | The outcome count you ask for; you set each band's start, the end is derived |
| Cards    | `CardCode` / `CardLabel`  | `DeckFactory`, which builds the whole deck                                   |


Every dice oracle stores its die in two columns, `DiceCount` and `DiceFaces` - a plain d8
is `1` and `8`, a 2d6 is `2` and `6`. They are the single source of truth for the die
(null only on the two card kinds); the type picker's "d4…d100" presets are just a shortcut
that fills them in. `DiceSpec` is the one place they are read, and it also answers what a
set of dice can roll: a 2d6 has a minimum of 2, a maximum of 12 and eleven distinct
totals, so eleven outcomes fit on it and the first band has to start at 2 rather than 1.
A card draw deals off a shuffled list of that oracle's entries held in your session on the
server, in memory, so a restart reshuffles every deck.
Which cards are left is per-person and per-sitting, and is never exported, imported or saved to the database.

### How a flow is put together

A `Flow` is a name, a `GameSystem`, and an ordered list of `FlowStep` rows. Every step carries a
`Label` - what the flow calls that step, for when "d6 Encounter Table" reads better as
"Encounter Type" in this particular chain - and a `Kind`, which decides what the step actually
does and therefore which of its other fields apply:


| Kind         | What it does                                                 |
| ------------ | ------------------------------------------------------------ |
| `Roll`       | Rolls the oracle it names, once. Each outcome can also **pick a table** for a later `RollChosen` step. |
| `Choice`     | Asks a question and offers a dropdown of options you author. An option can name an oracle. |
| `Text`       | Asks a question and takes a typed answer (including a number). |
| `RollChosen` | Rolls whichever oracle an earlier step picked - a `Choice` option or a `Roll` outcome. |
| `Loop`       | Asks how many times (1-50) to roll the next step, which must be a `Roll` or `RollChosen`. The last roll decides branches and picked tables. |


A flow is a **flat list**. Running it works through one step at a time, in order; when a step
rolls or is answered the run moves to the next one. That fall-through is the whole of the
default behaviour: a flow with three `Roll` steps and nothing else set up is simply those
three oracles, rolled in order.

**A dependent chain** - roll A, then roll whatever A landed on - is two steps. Give the first
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

Every oracle a flow names must be in the same file or already in your toolbox; the two
`Hustle` oracles above stand for ones you already have.

Only version 10 files import. A flow is a flat list of steps, and every step has a per-file
`id` that `pickedAtStep` and `goToStep` point at.



## Configuration

Nothing here needs changing to run the app. It is written down for people who want to move
things around.


| Setting                               | Default                             | What it does                                                                                                                                                                                                                                                                                                                    |
| ------------------------------------- | ----------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ConnectionStrings:DefaultConnection` | `Data Source=App_Data/gmtoolbox.db` | Where the database file lives. A relative path is resolved against the app's content root, so it lands in `App_Data/` no matter which folder you launched from. An absolute path is respected as-is.                                                                                                                            |
| `PORTFOLIO_SSO_URL`                   | unset                               | The portfolio's address. With `PORTFOLIO_SSO_PUBLIC_KEY`, turns on logging in with a portfolio account. |
| `PORTFOLIO_SSO_PUBLIC_KEY`            | unset                               | Checks the portfolio's login tokens. The portfolio's `python sso.py` prints it. |
| `SheetsRoot`                          | `App_Data/Sheets`                   | Where uploaded sheet PDFs are kept. |
| `ForceHttps`                          | `false`                             | Off, because the default setup has no certificate to redirect to - the container serves plain HTTP, and bouncing to a port nothing is listening on would just break it. Turn it on only where the app itself holds a certificate. Leave it off behind a reverse proxy that terminates TLS, which has already done the redirect. |


Change any of them by editing `appsettings.json`, or without touching the file, with an environment
variable - `ForceHttps=true`, or `ConnectionStrings__DefaultConnection` (two underscores, which
is how `:` is written in an environment variable). `dotnet user-secrets` works too, but only
when running with the SDK, not in the container.

## Project layout

```
Controllers/     HomeController, OraclesController (all oracle CRUD, export, import),
                 FlowsController (flow CRUD, the step builder, branches, running a flow),
                 GameSystemsController, SheetsController, PortfolioController (portfolio login)
Models/          GameSystem, Oracle, OracleEntry, Flow, FlowStep, FlowBranch, FlowChoiceOption,
                 Sheet, SheetVersion, IOwned, enums, view models, JSON transfer DTOs
Services/        OracleConfigurator (entry reconciliation), DeckFactory (deck generation),
                 OracleExporter / OracleImporter / FlowExporter / FlowImporter / OracleJson
                 (file round-trip), OracleRoller and FlowRunner (rolling, answering, the walk),
                 FlowConfigurator (the step list: order, options, branches), FlowText,
                 OracleListQuery / FlowListQuery / SheetListQuery / FilterSelectLists (list pages),
                 SheetStorageService / SheetFileValidator (sheet PDFs), SecurityQuestions,
                 PortfolioLogin
Data/            ApplicationDbContext, OwnershipRules and EF Core migrations
Filters/         OwnershipExceptionFilter
Views/           Razor views for Home, Oracles, Flows, GameSystems, Sheets and Portfolio, plus the
                 shared _RollPanel partial
Areas/Identity/  Login, Register, ForgotPassword, SecurityQuestion and DeletePersonalData; the rest
                 of the Identity UI this app doesn't use is closed off
App_Data/        gmtoolbox.db (the database) and Sheets/
ExportTemplate/  The example file the Import page offers to download
Tests/           xUnit tests: privacy rules, the importer, and the pages through a test server; js/ holds
                 the browser scripts' Vitest tests
coverage-index/  The test coverage landing page, the ronovos-coverage-index submodule
coverage-reports.json  This repo's entry for that page: its name and reports
docker/          git-sync.sh, which updates the container from GitHub on start
wwwroot/        Static assets (Bootstrap, jQuery, site CSS/JS, roll-panel.js)
```



## Tech stack

- ASP.NET Core MVC on .NET 10
- Entity Framework Core 10 (SQLite provider)
- Bootstrap 5 + jQuery

## License

BSD 2-Clause. See [LICENSE](LICENSE).
