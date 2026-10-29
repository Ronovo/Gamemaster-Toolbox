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
    private const string ExampleFileName = "CPR - Hustles and Night Markets.json";
    private const long MaxImportBytes = 5 * 1024 * 1024;

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
        if (Request.WantsPartial())
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

        if (Request.IsAjax())
        {
            return Json(new { cardsRemaining = OracleRoller.CardsRemaining(oracle, HttpContext.Session) });
        }

        return RedirectToAction(nameof(Roll), new { id });
    }

    public async Task<IActionResult> Create()
    {
        ViewData["GameSystemID"] = await FilterSelectLists.GameSystemPickerAsync(_context.GameSystems);
        ViewData["OracleSubtype"] = SubtypeSelectList();
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
        ViewData["GameSystemID"] = await FilterSelectLists.GameSystemPickerAsync(_context.GameSystems, oracle.GameSystemID);
        ViewData["OracleSubtype"] = SubtypeSelectList(oracle.OracleSubtype);
        return View(oracle);
    }

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

        ViewData["GameSystemID"] = await FilterSelectLists.GameSystemPickerAsync(_context.GameSystems, oracle.GameSystemID);
        ViewData["OracleSubtype"] = SubtypeSelectList(oracle.OracleSubtype);
        ViewData["BranchCount"] = await _context.FlowBranches.CountAsync(b => b.OracleEntry!.OracleID == oracle.ID);
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
            ViewData["GameSystemID"] = await FilterSelectLists.GameSystemPickerAsync(_context.GameSystems, oracle.GameSystemID);
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

        var flowQuery = _context.Flows.WithStepsAndOracles();

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
        return File(bytes, "application/json", $"gm-toolbox-{DateTime.Now:yyyy-MM-dd-HHmmssfff}.json");
    }

    // Shows what Clear All is about to destroy: the signed-in account's own oracles and flows.
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

    // Destroys everything confirmed in Oracles/ClearAll. The query filters keep each delete to the account's own
    // rows; steps, options and branches go with their flows, and entries with their oracles.
    [HttpPost, ActionName("ClearAll")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearAllConfirmed()
    {
        using (var transaction = await _context.Database.BeginTransactionAsync())
        {
            await _context.Flows.ExecuteDeleteAsync();
            await _context.Oracles.ExecuteDeleteAsync();
            await transaction.CommitAsync();
        }

        return RedirectToAction(nameof(Index));
    }

    public IActionResult Import(string? returnTo)
    {
        return View(new ImportViewModel { ReturnTo = NormalizeReturnTo(returnTo) });
    }

    public IActionResult ExampleFile()
    {
        return PhysicalFile(Path.Combine(_env.ContentRootPath, "ExportTemplate", ExampleFileName),
            "application/json", ExampleFileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxImportBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxImportBytes)]
    public async Task<IActionResult> Import(IFormFile? file, string? returnTo)
    {
        returnTo = NormalizeReturnTo(returnTo);

        if (file == null || file.Length == 0)
        {
            return ImportFailed("Choose an export file to import.", returnTo);
        }

        OracleExportFile? parsed;

        try
        {
            await using var stream = file.OpenReadStream();
            parsed = await JsonSerializer.DeserializeAsync<OracleExportFile>(stream, OracleJson.Options);
        }
        catch (JsonException ex)
        {
            return ImportFailed($"That file is not valid JSON: {ex.Message}", returnTo);
        }

        var systemsByName = OracleImporter.BuildSystemLookup(await _context.GameSystems.ToListAsync());

        var error = OracleImporter.TryBuild(parsed, systemsByName, out var oracles);
        if (error != null)
        {
            return ImportFailed(error, returnTo);
        }

        var existingOracles = await _context.Oracles.Include(o => o.GameSystem).Include(o => o.OracleEntries).ToListAsync();

        error = FlowImporter.TryBuild(parsed!.Flows, systemsByName, oracles, existingOracles, out var flows);
        if (error != null)
        {
            return ImportFailed(error, returnTo);
        }

        var newSystems = systemsByName.Values.Where(s => s.ID == 0).ToList();

        if (oracles.Count == 0 && flows.Count == 0 && newSystems.Count == 0)
        {
            return ImportFailed("The file contains no game systems, oracles or flows to add.", returnTo);
        }

        _context.GameSystems.AddRange(newSystems);
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

    private IActionResult RollResponse(int id, RollOutcome? outcome, string? error)
    {
        if (Request.IsAjax())
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

    private Task<List<string>> FlowsUsing(int oracleId)
    {
        return _context.Flows
            .Where(f => f.Steps.Any(s => s.OracleID == oracleId
                || s.Options.Any(o => o.OracleID == oracleId)
                || s.Branches.Any(b => b.OracleID == oracleId)))
            .OrderBy(f => f.Name)
            .Select(f => f.Name)
            .ToListAsync();
    }

    private static string? NormalizeReturnTo(string? returnTo)
    {
        if (string.Equals(returnTo, "Flows", StringComparison.OrdinalIgnoreCase))
            return "Flows";
        if (string.Equals(returnTo, "GameSystems", StringComparison.OrdinalIgnoreCase))
            return "GameSystems";
        return null;
    }

    private IActionResult ImportFailed(string error, string? returnTo)
    {
        ModelState.AddModelError("", error);
        return View(nameof(Import), new ImportViewModel { ReturnTo = returnTo });
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
        var vm = new OracleIndexViewModel
        {
            OracleType = string.IsNullOrWhiteSpace(oracleType) ? null : oracleType.Trim(),
            Subtypes = FilterSelectLists.OracleTypes(await OracleListQuery.DiceGeometriesAsync(_context.Oracles))
        };
        await vm.FillAsync(name, gameSystemId, _context.GameSystems, _context.Oracles);
        vm.Results = await OracleListQuery.ToPagedListAsync(
            OracleListQuery.ApplyFilters(_context.Oracles, vm.Name, vm.GameSystemID, vm.OracleType), page);
        return vm;
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

}