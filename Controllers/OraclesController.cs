using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.Transfer;
using GM_Toolbox.Models.ViewModels;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Rendering;

public class OraclesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly OracleConfigurator _configurator;
    private readonly IWebHostEnvironment _env;
    public OraclesController(ApplicationDbContext context, OracleConfigurator configurator, IWebHostEnvironment env)
    {
        _context = context;
        _configurator = configurator;
        _env = env;
    }

    public async Task<IActionResult> Index(string? name, int? gameSystemId, string? oracleType, int page = 1)
    {
        var vm = await BuildOracleIndex(name, gameSystemId, oracleType, page);
        if (WantsPartial())
        {
            return PartialView("_OracleListResults", vm);
        }

        return View(vm);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var existing = await LoadOracleWithEntries(id);
        if (existing == null) return NotFound();

        return View(existing);
    }

    public async Task<IActionResult> Roll(int? id)
    {
        if (id == null) return NotFound();

        var oracle = await LoadOracleWithEntries(id);
        if (oracle == null) return NotFound();

        if (oracle.OracleSubtype.Category() == OracleCategory.Cards)
        {
            ViewData["CardsRemaining"] = OracleRoller.CardsRemaining(oracle, HttpContext.Session);
        }

        if (TempData["RollOutcome"] is string rollJson)
        {
            ViewData["RollOutcome"] = JsonSerializer.Deserialize<RollOutcome>(rollJson);
        }

        if (TempData["RollError"] is string rollError)
        {
            ViewData["RollError"] = rollError;
        }

        return View(oracle);
    }

    [HttpPost, ActionName("Roll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RollOnce(int id)
    {
        var oracle = await LoadOracleWithEntries(id);
        if (oracle == null) return NotFound();

        var blocked = oracle.RollBlockedReason();
        if (blocked != null)
        {
            return RollResponse(id, null, blocked);
        }

        var error = OracleRoller.TryRoll(oracle, HttpContext.Session, out var outcome);
        if (error != null || outcome == null)
        {
            return RollResponse(id, null, error ?? "That roll produced no outcome.");
        }

        return RollResponse(id, outcome, null);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reshuffle(int id)
    {
        var oracle = await LoadOracleWithEntries(id);
        if (oracle == null) return NotFound();

        if (oracle.OracleSubtype.Category() != OracleCategory.Cards)
        {
            return BadRequest("Only card oracles have a deck to reshuffle.");
        }

        OracleRoller.Reshuffle(oracle, HttpContext.Session);

        if (IsAjaxRequest)
        {
            return Json(new { cardsRemaining = OracleRoller.CardsRemaining(oracle, HttpContext.Session) });
        }

        return RedirectToAction(nameof(Roll), new { id });
    }

    public async Task<IActionResult> Create()
    {
        ViewData["GameSystemID"] = await GameSystemSelectList();
        ViewData["OracleSubtype"] = SubtypeSelectList();
        // Start on a plain d6 so the die preset and its fields have something to show.
        return View(new Oracle { DiceCount = 1, DiceFaces = 6 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,OracleSubtype,GameSystemID,DiceCount,DiceFaces")] Oracle oracle)
    {
        ClearDiceForCards(oracle);

        if (ModelState.IsValid)
        {
            _context.Add(oracle);
            await _context.SaveChangesAsync();

            if (oracle.OracleSubtype.Category() == OracleCategory.Cards)
            {
                _configurator.Reconcile(oracle, null);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(EditEntries), new { id = oracle.ID });
            }

            return RedirectToAction(nameof(Configure), new { id = oracle.ID });
        }
        ViewData["GameSystemID"] = await GameSystemSelectList(oracle.GameSystemID);
        ViewData["OracleSubtype"] = SubtypeSelectList(oracle.OracleSubtype);
        return View(oracle);
    }

    // Serves the form for editing an oracle's Name, GameSystem and OracleSubtype.
    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var oracle = await _context.Oracles
        .FirstOrDefaultAsync(m => m.ID == id);
        if (oracle == null)
        {
            return NotFound();
        }

        ViewData["GameSystemID"] = await GameSystemSelectList(oracle.GameSystemID);
        ViewData["OracleSubtype"] = SubtypeSelectList(oracle.OracleSubtype);
        return View(oracle);
    }

    // Changing the subtype, or a custom oracle's dice, deletes every existing entry.
    // Card oracles get a full deck with each card's name as the outcome. Dice oracles
    // go to Configure so you can pick how many outcomes to generate.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("ID,Name,OracleSubtype,GameSystemID,DiceCount,DiceFaces")] Oracle oracle)
    {
        if (id != oracle.ID)
        {
            return NotFound();
        }

        ClearDiceForCards(oracle);

		if (!ModelState.IsValid)
		{
            ViewData["GameSystemID"] = await GameSystemSelectList(oracle.GameSystemID);
            ViewData["OracleSubtype"] = SubtypeSelectList(oracle.OracleSubtype);
            return View(oracle);
		}

		var existing = await LoadOracleWithEntries(id);
		if (existing == null)
		{
			return NotFound();
		}

		bool rollChanged = existing.OracleSubtype != oracle.OracleSubtype
			|| existing.DiceCount != oracle.DiceCount
			|| existing.DiceFaces != oracle.DiceFaces;

		existing.Name = oracle.Name;
		existing.GameSystemID = oracle.GameSystemID;
		existing.OracleSubtype = oracle.OracleSubtype;
		existing.DiceCount = oracle.DiceCount;
		existing.DiceFaces = oracle.DiceFaces;

		if (rollChanged)
		{
			_context.OracleEntries.RemoveRange(existing.OracleEntries.ToList());
		}

		await _context.SaveChangesAsync();

		if (rollChanged)
		{
            if (existing.OracleSubtype.Category() == OracleCategory.Cards)
            {
                existing = await LoadOracleWithEntries(existing.ID);
                if (existing == null) return NotFound();
                _configurator.Reconcile(existing, null);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(EditEntries), new { id = existing.ID });
            }

			return RedirectToAction(nameof(Configure), new { id = existing.ID });
		}
		
		return RedirectToAction(nameof(Index));
    }

    // Deleting an oracle takes its entries with it (cascade on the FK).
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null)
        {
            return NotFound();
        }

        var oracle = await _context.Oracles
            .Include(o => o.GameSystem)
            .FirstOrDefaultAsync(m => m.ID == id);
        if (oracle == null)
        {
            return NotFound();
        }

        ViewData["UsedByFlows"] = await FlowsUsing(oracle.ID);
        return View(oracle);
    }

    // Renamed because the POST would otherwise share a signature with the GET;
    // [ActionName] keeps the route as Delete.
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var oracle = await _context.Oracles
            .Include(o => o.GameSystem)
            .FirstOrDefaultAsync(o => o.ID == id);
        if (oracle == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var usedBy = await FlowsUsing(oracle.ID);
        if (usedBy.Count > 0)
        {
            ViewData["UsedByFlows"] = usedBy;
            ModelState.AddModelError("",
                "This oracle is used by one or more flows and cannot be deleted until they let go of it.");
            return View(oracle);
        }

        _context.Oracles.Remove(oracle);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    // Step 1 of the two-step builder: how many outcomes (dice), or just build the deck (cards).
    public async Task<IActionResult> Configure(int? id, string? returnTo = null)
    {
        if (id == null) return NotFound();

        var oracle = await LoadOracleWithEntries(id);
        if (oracle == null) return NotFound();

        ViewData["ReturnTo"] = returnTo;
        return View(oracle);
    }

    // Reconcile adds, trims or rebuilds entry rows, keeping the result text already typed.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configure(int id, int? outcomeCount, string? returnTo = null)
    {
        var oracle = await LoadOracleWithEntries(id);
        if (oracle == null) return NotFound();
        var error = _configurator.Reconcile(oracle, outcomeCount);
        if (error != null)
        {
            ModelState.AddModelError(nameof(outcomeCount), error);
            ViewData["ReturnTo"] = returnTo;
            return View(oracle);
        }
        
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(EditEntries), new { id });
    }

    // Step 2 of the two-step builder: type the result text, and for dice the band starts.
    public async Task<IActionResult> EditEntries(int? id)
    {
        if (id == null) return NotFound();
        var oracle = await LoadOracleWithEntries(id);
        if (oracle == null) return NotFound();

        var isDice = oracle.OracleSubtype.Category() == OracleCategory.Dice;
        var spec = oracle.Dice();
        var vm = new EditEntriesViewModel
        {
            OracleID = oracle.ID,
            Name = oracle.Name,
            IsDice = isDice,
            OracleSubtype = oracle.OracleSubtype,
            MinRoll = spec?.Min ?? 0,
            MaxRoll = spec?.Max ?? 0,
            DiceCount = spec?.Count ?? 0,
            DiceFaces = spec?.Faces ?? 0,
            Rows = oracle.OracleEntries
           .OrderBy(e => e.SortOrder)
           .Select(e => ToEntryRow(e, spec))
           .ToList()
        };

        return View(vm);
    }

    // Validates the band starts, then derives each RangeEnd from them.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEntries(EditEntriesViewModel vm)
    {
        var oracle = await LoadOracleWithEntries(vm.OracleID);
        if (oracle == null) return NotFound();

        var entries = oracle.OracleEntries.OrderBy(e => e.SortOrder).ToList();
        var isDice = oracle.OracleSubtype.Category() == OracleCategory.Dice;
        var spec = oracle.Dice();

        int minRoll = spec?.Min ?? 0;
        int maxRoll = spec?.Max ?? 0;

        if (isDice)
        {
            for (int i = 0; i < vm.Rows.Count; i++)
            {
                int startsAt = vm.Rows[i].StartsAt;

                if (i == 0 && startsAt != minRoll)
                    ModelState.AddModelError("", $"The first outcome must start at {minRoll}.");

                if (startsAt < minRoll || startsAt > maxRoll)
                    ModelState.AddModelError("", $"Row {i + 1}: start must be between {minRoll} and {maxRoll}.");

                if (i > 0 && startsAt <= vm.Rows[i - 1].StartsAt)
                    ModelState.AddModelError("", $"Row {i + 1}: start must be greater than the row above.");
            }
        }

        if (!ModelState.IsValid)
        {
            // Rebuild the fields the form didn't post back, then re-show
            vm.Name = oracle.Name;
            vm.IsDice = isDice;
            vm.OracleSubtype = oracle.OracleSubtype;
            vm.MinRoll = minRoll;
            vm.MaxRoll = maxRoll;
            vm.DiceCount = spec?.Count ?? 0;
            vm.DiceFaces = spec?.Faces ?? 0;
            for (int i = 0; i < vm.Rows.Count && i < entries.Count; i++)
                ApplyEntryDisplay(vm.Rows[i], entries[i]);
            return View(vm);
        } 

        for (int i = 0; i < vm.Rows.Count; i++)
        {
            var row = vm.Rows[i];
            var entry = entries.FirstOrDefault(e => e.ID == row.EntryId);
            if (entry == null) continue;

            entry.Result = row.Result ?? string.Empty;

            if (isDice)
            {
                // Bands are derived, not entered. The user gives only each band's start;
                // a band ends one before the next one begins, and the last runs to the
                // highest total the dice can roll. Starts 1/3/5 on a d6 give 1-2, 3-4, 5-6.
                entry.RangeStart = row.StartsAt;
                entry.RangeEnd = (i < vm.Rows.Count - 1)
                    ? vm.Rows[i + 1].StartsAt - 1
                    : maxRoll;
            }
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = vm.OracleID });
    }

    // Asks what to export before exporting it. The download itself is the POST below.
    [HttpGet]
    public async Task<IActionResult> Export(ExportViewModel vm)
    {
        vm.ReturnTo = NormalizeReturnTo(vm.ReturnTo);
        await BuildExportViewModel(vm);

        var partial = Request.Query["partial"].ToString();
        if (string.Equals(partial, "oracles", StringComparison.OrdinalIgnoreCase))
        {
            return PartialView("_ExportOraclePicker", vm);
        }

        if (string.Equals(partial, "flows", StringComparison.OrdinalIgnoreCase))
        {
            return PartialView("_ExportFlowPicker", vm);
        }

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> ExportOracleIds(string? oracleName, int? oracleGameSystemId, string? oracleType)
    {
        var query = OracleListQuery.ApplyFilters(_context.Oracles, oracleName, oracleGameSystemId, oracleType);
        return Json(await OracleListQuery.MatchingIdsAsync(query));
    }

    [HttpGet]
    public async Task<IActionResult> ExportFlowIds(string? flowName, int? flowGameSystemId)
    {
        var query = FlowListQuery.ApplyFilters(_context.Flows, flowName, flowGameSystemId);
        return Json(await FlowListQuery.MatchingIdsAsync(query));
    }

    [HttpPost, ActionName("Export")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportConfirmed(ExportViewModel vm)
    {
        vm.ReturnTo = NormalizeReturnTo(vm.ReturnTo);

        var oracleQuery = _context.Oracles
            .Include(o => o.OracleEntries)
            .Include(o => o.GameSystem)
            .AsQueryable();

        var flowQuery = _context.Flows
            .Include(f => f.GameSystem)
            .Include(f => f.Steps).ThenInclude(s => s.Branches).ThenInclude(b => b.Oracle)
                .ThenInclude(o => o!.GameSystem)
            .Include(f => f.Steps).ThenInclude(s => s.Branches).ThenInclude(b => b.Oracle)
                .ThenInclude(o => o!.OracleEntries)
            .Include(f => f.Steps).ThenInclude(s => s.Oracle).ThenInclude(o => o!.GameSystem)
            .Include(f => f.Steps).ThenInclude(s => s.Oracle).ThenInclude(o => o!.OracleEntries)
            // The options and their oracles too: a dropdown question is nothing without them,
            // and FlowExporter bundles those oracles into the file.
            .Include(f => f.Steps).ThenInclude(s => s.Options).ThenInclude(o => o.Oracle)
                .ThenInclude(o => o!.GameSystem)
            .Include(f => f.Steps).ThenInclude(s => s.Options).ThenInclude(o => o.Oracle)
                .ThenInclude(o => o!.OracleEntries)
            // Split, for the same reason FlowsController.LoadFlow is: several collections down
            // different branches multiply out into one very wide result set.
            .AsSplitQuery()
            .AsQueryable();

        List<Oracle> oracles;
        List<Flow> flows;

        switch (vm.Scope)
        {
            case ExportScope.AllOracles:
                oracles = await oracleQuery.OrderBy(o => o.Name).ToListAsync();
                flows = new List<Flow>();
                break;

            case ExportScope.SelectedOracles:
                oracles = await oracleQuery
                    .Where(o => vm.SelectedIds.Contains(o.ID))
                    .OrderBy(o => o.Name)
                    .ToListAsync();
                flows = new List<Flow>();
                break;

            case ExportScope.SelectedFlows:
                oracles = new List<Oracle>();
                flows = await flowQuery
                    .Where(f => vm.SelectedFlowIds.Contains(f.ID))
                    .OrderBy(f => f.Name)
                    .ToListAsync();
                break;

            default:
                oracles = await oracleQuery.OrderBy(o => o.Name).ToListAsync();
                flows = await flowQuery.OrderBy(f => f.Name).ToListAsync();
                break;
        }

        if (vm.Scope == ExportScope.SelectedOracles && oracles.Count == 0)
        {
            ModelState.AddModelError("", "Pick at least one oracle to export.");
            return View(await BuildExportViewModel(vm));
        }

        if (vm.Scope == ExportScope.SelectedFlows && flows.Count == 0)
        {
            ModelState.AddModelError("", "Pick at least one flow to export.");
            return View(await BuildExportViewModel(vm));
        }

        // A flow that arrives without the oracles it rolls is a list of names pointing at
        // nothing, so they come along whether or not they were ticked. 
        var referencedIds = FlowExporter.ReferencedOracles(flows).Select(o => o.ID).ToList();
        if (referencedIds.Count > 0)
        {
            var bundled = await oracleQuery
                .Where(o => referencedIds.Contains(o.ID))
                .ToListAsync();
            oracles = oracles.Concat(bundled).ToList();
        }

        oracles = oracles
            .DistinctBy(o => o.ID)
            .OrderBy(o => o.Name)
            .ToList();

        var systems = vm.Scope == ExportScope.Everything
            ? await _context.GameSystems.OrderBy(g => g.Name).ToListAsync()
            : oracles.Select(o => o.GameSystem)
                .Concat(flows.Select(f => f.GameSystem))
                .Where(s => s != null)
                .Select(s => s!)
                .DistinctBy(s => s.ID)
                .OrderBy(s => s.Name)
                .ToList();

        if (oracles.Count == 0 && flows.Count == 0 && systems.Count == 0)
        {
            ModelState.AddModelError("", "Nothing to export.");
            return View(await BuildExportViewModel(vm));
        }

        var file = OracleExporter.Build(oracles);
        file.Flows = FlowExporter.Build(flows);
        file.GameSystems = OracleExporter.BuildSystems(systems);

        var json = JsonSerializer.Serialize(file, OracleJson.Options);
        var bytes = Encoding.UTF8.GetBytes(json);
        // fff = milliseconds, so two exports in the same second do not collide
        var filename = $"gm-toolbox-{DateTime.Now:yyyy-MM-dd-HHmmssfff}.json";

        await WriteExportArchive(bytes, filename, vm.Scope != ExportScope.Everything);

        return File(bytes, "application/json", filename);
    }

    // Shows what Clear All is about to destroy.
    public async Task<IActionResult> ClearAll()
    {
        var vm = new ClearAllViewModel
        {
            OracleCount = await _context.Oracles.CountAsync(),
            EntryCount = await _context.OracleEntries.CountAsync(),
            FlowCount = await _context.Flows.CountAsync(),
            FlowStepCount = await _context.FlowSteps.CountAsync()
        };

        return View(vm);
    }

    // Destroys everything confirmed in Oracles/ClearAll
    [HttpPost, ActionName("ClearAll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAllConfirmed()
    {
        // TODO FOR MULTI-USER: filter both deletes to the current user, and drop the reseed
        // entirely — you cannot reseed a table that still holds other people's rows.
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            await _context.FlowBranches.ExecuteDeleteAsync();
            await _context.FlowChoiceOptions.ExecuteDeleteAsync();
            await _context.FlowSteps.ExecuteDeleteAsync();
            await _context.Flows.ExecuteDeleteAsync();

            // Children first: OracleEntries has a required FK to Oracles.
            await _context.OracleEntries.ExecuteDeleteAsync();
            await _context.Oracles.ExecuteDeleteAsync();

            await transaction.CommitAsync();
        }

        await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM sqlite_sequence WHERE name IN " +
            "('Oracles', 'OracleEntries', 'Flows', 'FlowSteps', 'FlowBranches', 'FlowChoiceOptions');");

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Import(string? returnTo)
    {
        ViewData["ReturnTo"] = NormalizeReturnTo(returnTo);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(IFormFile? file, string? returnTo)
    {
        returnTo = NormalizeReturnTo(returnTo);
        ViewData["ReturnTo"] = returnTo;

        if (file == null || file.Length == 0)
        {
            ModelState.AddModelError("", "Choose a file to import.");
            return View();
        }

        OracleExportFile? parsed;

        try
        {
            using var stream = file.OpenReadStream();
            parsed = await JsonSerializer.DeserializeAsync<OracleExportFile>(stream, OracleJson.Options);
        }
        catch (JsonException ex)
        {
            ModelState.AddModelError("", $"That file is not valid JSON: {ex.Message}");
            return View();
        }

        var systemsByName = OracleImporter.BuildSystemLookup(await _context.GameSystems.ToListAsync());

        var error = OracleImporter.TryBuild(parsed, systemsByName, out var oracles);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            return View();
        }

        var existingOracles = await _context.Oracles.Include(o => o.GameSystem).ToListAsync();

        error = FlowImporter.TryBuild(parsed!.Flows, parsed.FormatVersion, systemsByName, oracles,
            existingOracles, out var flows);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            return View();
        }

        var newSystems = systemsByName.Values.Where(s => s.ID == 0).ToList();

        if (oracles.Count == 0 && flows.Count == 0 && newSystems.Count == 0)
        {
            ModelState.AddModelError("", "The file contains no game systems, oracles or flows to add.");
            return View();
        }

        // MULTI-USER: stamp each oracle with the current user's id before adding.
        //
        // AddRange takes the whole graph — EF inserts each Oracle, reads back its
        // generated ID, and fills in OracleID on its entries automatically. 
        foreach (var system in newSystems)
        {
            if (_context.Entry(system).State == EntityState.Detached)
                _context.GameSystems.Add(system);
        }

        _context.Oracles.AddRange(oracles);
        _context.Flows.AddRange(flows);
        await _context.SaveChangesAsync();

        TempData["ImportMessage"] = ImportMessage(oracles.Count, flows.Count, newSystems.Count);

        if (returnTo == "Flows")
            return RedirectToAction(nameof(FlowsController.Index), "Flows");
        if (returnTo == "GameSystems")
            return RedirectToAction(nameof(GameSystemsController.Index), "GameSystems");

        return RedirectToAction(nameof(Index));
    }

    private bool IsAjaxRequest => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

    private IActionResult RollResponse(int id, RollOutcome? outcome, string? error)
    {
        if (IsAjaxRequest)
        {
            return error != null ? Json(new { error }) : Json(outcome);
        }

        if (error != null)
        {
            TempData["RollError"] = error;
        }
        else
        {
            TempData["RollOutcome"] = JsonSerializer.Serialize(outcome);
        }

        return RedirectToAction(nameof(Roll), new { id });
    }

    private async Task<List<string>> FlowsUsing(int oracleId)
    {
        var fromSteps = await _context.FlowSteps
            .Where(s => s.OracleID == oracleId)
            .Select(s => s.Flow!.Name)
            .ToListAsync();

        var fromOptions = await _context.FlowChoiceOptions
            .Where(o => o.OracleID == oracleId)
            .Select(o => o.FlowStep!.Flow!.Name)
            .ToListAsync();

        return fromSteps.Concat(fromOptions).Distinct().OrderBy(name => name).ToList();
    }

    private static string? NormalizeReturnTo(string? returnTo)
    {
        if (string.Equals(returnTo, "Flows", StringComparison.OrdinalIgnoreCase))
            return "Flows";
        if (string.Equals(returnTo, "GameSystems", StringComparison.OrdinalIgnoreCase))
            return "GameSystems";
        return null;
    }

    private static string ImportMessage(int oracleCount, int flowCount, int newSystemCount)
    {
        var parts = new List<string>();
        if (oracleCount > 0) parts.Add($"{oracleCount} oracle(s)");
        if (flowCount > 0) parts.Add($"{flowCount} flow(s)");
        if (newSystemCount > 0) parts.Add($"{newSystemCount} game system(s)");
        return parts.Count == 0 ? "Imported nothing." : "Imported " + string.Join(" and ", parts) + ".";
    }

    private async Task<ExportViewModel> BuildExportViewModel(ExportViewModel vm)
    {
        if (vm.OracleGameSystemID is <= 0) vm.OracleGameSystemID = null;
        if (vm.FlowGameSystemID is <= 0) vm.FlowGameSystemID = null;

        var systems = await _context.GameSystems.OrderBy(g => g.Name).ToListAsync();
        vm.GameSystems = FilterSelectLists.GameSystems(systems);
        vm.Subtypes = FilterSelectLists.OracleTypes(await OracleListQuery.DiceGeometriesAsync(_context.Oracles));

        var oracles = OracleListQuery.ApplyFilters(
            _context.Oracles, vm.OracleName, vm.OracleGameSystemID, vm.OracleType);
        vm.Available = await OracleListQuery.ToPagedListAsync(oracles, vm.OraclePage);
        vm.OraclePage = vm.Available.Page;

        var flows = FlowListQuery.ApplyFilters(_context.Flows, vm.FlowName, vm.FlowGameSystemID);
        vm.AvailableFlows = await FlowListQuery.ToPagedListAsync(flows, vm.FlowPage);
        vm.FlowPage = vm.AvailableFlows.Page;

        vm.GameSystemCount = await _context.GameSystems.CountAsync();
        vm.OracleCount = await _context.Oracles.CountAsync();
        vm.FlowCount = await _context.Flows.CountAsync();
        return vm;
    }

    private async Task<OracleIndexViewModel> BuildOracleIndex(
        string? name, int? gameSystemId, string? oracleType, int page)
    {
        if (gameSystemId is <= 0) gameSystemId = null;
        var term = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var type = string.IsNullOrWhiteSpace(oracleType) ? null : oracleType.Trim();

        var systems = await _context.GameSystems.OrderBy(g => g.Name).ToListAsync();
        var filtered = OracleListQuery.ApplyFilters(_context.Oracles, term, gameSystemId, type);

        return new OracleIndexViewModel
        {
            Name = term,
            GameSystemID = gameSystemId,
            OracleType = type,
            CatalogEmpty = !await _context.Oracles.AnyAsync(),
            Results = await OracleListQuery.ToPagedListAsync(filtered, page),
            GameSystems = FilterSelectLists.GameSystems(systems),
            Subtypes = FilterSelectLists.OracleTypes(await OracleListQuery.DiceGeometriesAsync(_context.Oracles))
        };
    }

    private Task<List<OracleListItem>> OracleListItems()
    {
        return OracleListQuery.ToListItemsAsync(_context.Oracles);
    }

    private bool WantsPartial()
    {
        return string.Equals(Request.Query["partial"], "1", StringComparison.OrdinalIgnoreCase);
    }

    // Keeps a server-side copy alongside the download. Full and partial exports go to
    // separate folders so a subset is never mistaken for a backup of everything.
    private async Task WriteExportArchive(byte[] bytes, string filename, bool isPartial)
    {
        var folder = Path.Combine(
            _env.ContentRootPath, "App_Data", "Exports", isPartial ? "Partial" : "Full");

        Directory.CreateDirectory(folder);
        // Explicitly reference System.IO.File to avoid ambiguity with the File() method of ControllerBase
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, filename), bytes);
    }

    private Task<Oracle?> LoadOracleWithEntries(int? id)
    {
        return _context.Oracles
            .Include(o => o.OracleEntries)
            .Include(o => o.GameSystem)
            .FirstOrDefaultAsync(o => o.ID == id);
    }

    private static EntryRow ToEntryRow(OracleEntry entry, DiceSpec? spec)
    {
        var row = new EntryRow
        {
            EntryId = entry.ID,
            Result = entry.Result,
            StartsAt = entry.RangeStart ?? (spec?.Min ?? 1) + entry.SortOrder - 1
        };
        ApplyEntryDisplay(row, entry);
        return row;
    }

    private static void ApplyEntryDisplay(EntryRow row, OracleEntry entry)
    {
        row.Label = entry.CardLabel ?? $"Outcome {entry.SortOrder}";
        row.CardCode = entry.CardCode;
        var classified = DeckFactory.Classify(entry.CardCode);
        row.Suit = classified?.Suit;
        row.RankKind = classified?.RankKind;
    }

    // A card oracle has no dice geometry: null the columns whatever the form posted. A dice
    // oracle keeps whatever came in (the die preset on the form fills them; Validate() then
    // guarantees both are set).
    private static void ClearDiceForCards(Oracle oracle)
    {
        if (oracle.OracleSubtype.Category() != OracleCategory.Cards)
        {
            return;
        }

        oracle.DiceCount = null;
        oracle.DiceFaces = null;
    }

    private static SelectList SubtypeSelectList(OracleSubtype? selected = null)
    {
        var ordered = new[]
        {
            OracleSubtype.Dice,
            OracleSubtype.PlayingCards,
            OracleSubtype.TarotCards,
        };

        var items = ordered
            .Select(s => new { Value = s.ToString(), Text = s.DisplayName() })
            .ToList();

        return new SelectList(items, "Value", "Text", (selected ?? OracleSubtype.Dice).ToString());
    }

    private async Task<SelectList> GameSystemSelectList(int? selected = null)
    {
        var systems = await _context.GameSystems
            .OrderBy(g => g.Name)
            .ToListAsync();
        return new SelectList(systems, "ID", "Name", selected);
    }
}