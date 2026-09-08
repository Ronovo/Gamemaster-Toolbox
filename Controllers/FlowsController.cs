using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.ViewModels;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

public class FlowsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly FlowConfigurator _configurator;

    public FlowsController(ApplicationDbContext context, FlowConfigurator configurator)
    {
        _context = context;
        _configurator = configurator;
    }

    public async Task<IActionResult> Index(string? name, int? gameSystemId, int page = 1)
    {
        var vm = await BuildFlowIndex(name, gameSystemId, page);
        if (string.Equals(Request.Query["partial"], "1", StringComparison.OrdinalIgnoreCase))
        {
            return PartialView("_FlowListResults", vm);
        }

        return View(vm);
    }

    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var flow = await LoadFlow(id);
        if (flow == null) return NotFound();

        return View(flow);
    }

    public async Task<IActionResult> Create()
    {
        // A flow is made of oracles, so there is nothing to build until at least one exists.
        // The view says so rather than offering an empty picker.
        ViewData["OracleCount"] = await _context.Oracles.CountAsync();
        ViewData["GameSystemID"] = await GameSystemSelectList();
        return View(new Flow());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description,GameSystemID")] Flow flow)
    {
        if (ModelState.IsValid)
        {
            _context.Add(flow);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Steps), new { id = flow.ID });
        }

        ViewData["OracleCount"] = await _context.Oracles.CountAsync();
        ViewData["GameSystemID"] = await GameSystemSelectList(flow.GameSystemID);
        return View(flow);
    }

    public async Task<IActionResult> Edit(int? id)
    {
        if (id == null) return NotFound();

        var flow = await _context.Flows.FirstOrDefaultAsync(f => f.ID == id);
        if (flow == null) return NotFound();

        ViewData["GameSystemID"] = await GameSystemSelectList(flow.GameSystemID);
        return View(flow);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int? id, [Bind("ID,Name,Description,GameSystemID")] Flow flow)
    {
        if (id != flow.ID) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewData["GameSystemID"] = await GameSystemSelectList(flow.GameSystemID);
            return View(flow);
        }

        // Load-then-assign, never _context.Update() on the bound object. See the comment on
        // OraclesController.Edit: Update() marks every scalar modified, so a property missing
        // from the [Bind] list above would be written back as its default.
        var existing = await _context.Flows.FirstOrDefaultAsync(f => f.ID == id);
        if (existing == null) return NotFound();

        existing.Name = flow.Name;
        existing.Description = flow.Description;
        existing.GameSystemID = flow.GameSystemID;

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();

        var flow = await _context.Flows
            .Include(f => f.GameSystem)
            .Include(f => f.Steps)
            .FirstOrDefaultAsync(f => f.ID == id);
        if (flow == null) return NotFound();

        return View(flow);
    }

    // Renamed because the POST would otherwise share a signature with the GET;
    // [ActionName] keeps the route as Delete.
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int? id)
    {
        var flow = await _context.Flows.FindAsync(id);
        if (flow != null)
        {
            _context.Flows.Remove(flow);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Steps(int? id)
    {
        if (id == null) return NotFound();

        var flow = await LoadFlow(id);
        if (flow == null) return NotFound();

        await PopulateStepPickers(flow);
        return View(BuildStepsViewModel(flow));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddStep(int id, FlowStepsViewModel vm)
    {
        var flow = await LoadFlow(id);
        if (flow == null) return NotFound();

        var error = _configurator.AddStep(flow, vm.AddKind, vm.AddOracleID, vm.AddLabel,
            vm.AddQuestion, vm.AddSourceStepSortOrder);
        if (error != null)
        {
            return await StepsWithError(flow, error, vm);
        }

        await _context.SaveChangesAsync();

        var added = flow.Steps.OrderBy(s => s.SortOrder).Last();
        if (added.Kind == FlowStepKind.Choice)
        {
            return RedirectToAction(nameof(Options), new { id = added.ID });
        }

        return RedirectToAction(nameof(Steps), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveStep(int id, int stepId)
    {
        var flow = await LoadFlow(id);
        if (flow == null) return NotFound();

        var error = _configurator.RemoveStep(flow, stepId, out var orphaned);
        if (error != null)
        {
            return await StepsWithError(flow, error, null);
        }

        await _context.SaveChangesAsync();

        if (orphaned > 0)
        {
            TempData["FlowMessage"] =
                $"Step removed. {orphaned} branch(es) or later step(s) pointed at it and have been cleared.";
        }

        return RedirectToAction(nameof(Steps), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveStep(int id, int stepId, int delta)
    {
        var flow = await LoadFlow(id);
        if (flow == null) return NotFound();

        var error = _configurator.MoveStep(flow, stepId, delta);
        if (error != null)
        {
            return await StepsWithError(flow, error, null);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Steps), new { id });
    }

    public async Task<IActionResult> EditStep(int? id)
    {
        if (id == null) return NotFound();

        var located = await LocateStep(id);
        if (located == null) return NotFound();

        var (flow, step) = located.Value;
        await PopulateStepPickers(flow, step.SourceStepSortOrder);
        return View(BuildEditStepViewModel(flow, step));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditStep(EditStepViewModel vm)
    {
        var located = await LocateStep(vm.StepID);
        if (located == null) return NotFound();

        var (flow, step) = located.Value;

        var error = _configurator.EditStep(flow, step, vm.Label, vm.Question, vm.OracleID,
            vm.SourceStepSortOrder);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            await PopulateStepPickers(flow, vm.SourceStepSortOrder);

            var rebuilt = BuildEditStepViewModel(flow, step);
            rebuilt.Label = vm.Label;
            rebuilt.Question = vm.Question;
            rebuilt.OracleID = vm.OracleID;
            rebuilt.SourceStepSortOrder = vm.SourceStepSortOrder;
            return View(rebuilt);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Steps), new { id = flow.ID });
    }

    public async Task<IActionResult> Branches(int? id)
    {
        if (id == null) return NotFound();

        var located = await LocateStep(id);
        if (located == null) return NotFound();

        var (flow, step) = located.Value;
        return View(BuildBranchesViewModel(flow, step));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Branches(FlowBranchesViewModel vm)
    {
        var located = await LocateStep(vm.StepID);
        if (located == null) return NotFound();

        var (flow, step) = located.Value;

        var error = _configurator.SetBranches(flow, step, vm.Rows);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            // Rebuild everything the form did not post back, keeping the choices just made.
            var rebuilt = BuildBranchesViewModel(flow, step);
            for (int i = 0; i < rebuilt.Rows.Count && i < vm.Rows.Count; i++)
            {
                rebuilt.Rows[i].Target = vm.Rows[i].Target;
            }
            return View(rebuilt);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Steps), new { id = flow.ID });
    }

    public async Task<IActionResult> Options(int? id)
    {
        if (id == null) return NotFound();

        var located = await LocateStep(id);
        if (located == null) return NotFound();

        var (flow, step) = located.Value;
        if (step.Kind != FlowStepKind.Choice) return NotFound();

        return View(await BuildOptionsViewModel(flow, step));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Options(FlowOptionsViewModel vm)
    {
        var located = await LocateStep(vm.StepID);
        if (located == null) return NotFound();

        var (flow, step) = located.Value;

        var error = _configurator.SetOptions(step, vm.Rows);
        if (error != null)
        {
            return await OptionsWithError(flow, step, error, vm);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Options), new { id = step.ID });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOption(int id, FlowOptionsViewModel vm)
    {
        var located = await LocateStep(id);
        if (located == null) return NotFound();

        var (flow, step) = located.Value;

        var error = _configurator.AddOption(step, vm.AddLabel, vm.AddOracleID);
        if (error != null)
        {
            return await OptionsWithError(flow, step, error, vm);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Options), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOptionsFromOracles(int id, FlowOptionsViewModel vm)
    {
        var located = await LocateStep(id);
        if (located == null) return NotFound();

        var (flow, step) = located.Value;

        var error = _configurator.AddOptionsFromOracles(step, vm.AddOracleIds, out var added);
        if (error != null)
        {
            return await OptionsWithError(flow, step, error, vm);
        }

        await _context.SaveChangesAsync();
        TempData["FlowMessage"] = $"Added {added} option(s).";
        return RedirectToAction(nameof(Options), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveOption(int id, int optionId)
    {
        var located = await LocateStep(id);
        if (located == null) return NotFound();

        var (flow, step) = located.Value;

        var error = _configurator.RemoveOption(step, optionId);
        if (error != null)
        {
            return await OptionsWithError(flow, step, error, null);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Options), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MoveOption(int id, int optionId, int delta)
    {
        var located = await LocateStep(id);
        if (located == null) return NotFound();

        var (flow, step) = located.Value;

        var error = _configurator.MoveOption(step, optionId, delta);
        if (error != null)
        {
            return await OptionsWithError(flow, step, error, null);
        }

        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Options), new { id });
    }

    public async Task<IActionResult> Run(int? id)
    {
        if (id == null) return NotFound();

        var flow = await LoadFlow(id);
        if (flow == null) return NotFound();

        var state = FlowRunner.Load(flow, HttpContext.Session);
        FlowRunner.Save(state, HttpContext.Session);

        if (TempData["FlowRunError"] is string runError)
        {
            ViewData["RunError"] = runError;
        }

        ViewData["RunState"] = state;
        return View(flow);
    }

    [HttpPost, ActionName("Run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RollStep(int id, string? answer)
    {
        var flow = await LoadFlow(id);
        if (flow == null) return NotFound();

        var blocked = flow.RunBlockedReason();
        if (blocked != null)
        {
            return RunResponse(id, null, null, null, blocked);
        }

        var state = FlowRunner.Load(flow, HttpContext.Session);
        var error = FlowRunner.TryAdvance(flow, state, HttpContext.Session, answer, out var rolled);

        if (error != null || rolled == null)
        {
            // TryAdvance may have reset a run that no longer matches the flow, so save either way.
            FlowRunner.Save(state, HttpContext.Session);
            return RunResponse(id, null, null, null, error ?? "That step produced no outcome.");
        }

        FlowRunner.Save(state, HttpContext.Session);
        return RunResponse(id, rolled, FlowRunner.CurrentStep(flow, state), state, null);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restart(int id)
    {
        var flow = await LoadFlow(id);
        if (flow == null) return NotFound();

        FlowRunner.Restart(flow.ID, HttpContext.Session);

        if (IsAjaxRequest)
        {
            var fresh = FlowRunner.Load(flow, HttpContext.Session);
            return Json(new
            {
                next = NextDescriptor(FlowRunner.CurrentStep(flow, fresh)),
                finished = fresh.Finished
            });
        }

        return RedirectToAction(nameof(Run), new { id });
    }

    private bool IsAjaxRequest => Request.Headers["X-Requested-With"] == "XMLHttpRequest";


    private IActionResult RunResponse(int id, FlowRunStep? rolled, FlowStep? next,
        FlowRunState? state, string? error)
    {
        if (IsAjaxRequest)
        {
            if (error != null)
            {
                return Json(new { error });
            }

            return Json(new
            {
                label = rolled!.Label,
                oracleName = rolled.OracleName,
                outcome = rolled.Outcome,
                isQuestion = rolled.IsQuestion,
                next = NextDescriptor(next),
                finished = state!.Finished,
                hitStepLimit = state.HitStepLimit
            });
        }

        if (error != null)
        {
            TempData["FlowRunError"] = error;
        }

        return RedirectToAction(nameof(Run), new { id });
    }

    private static object? NextDescriptor(FlowStep? step)
    {
        if (step == null)
        {
            return null;
        }

        return new
        {
            stepId = step.ID,
            kind = step.Kind.ToString(),
            isQuestion = step.Kind.IsQuestion(),
            label = step.DisplayLabel(),
            question = step.Question,
            buttonLabel = step.ControlLabel(),
            options = step.Options
                .OrderBy(o => o.SortOrder)
                .Select(o => new { id = o.ID, label = o.Label })
                .ToList()
        };
    }

    private async Task<IActionResult> StepsWithError(Flow flow, string error, FlowStepsViewModel? posted)
    {
        ModelState.AddModelError("", error);
        await PopulateStepPickers(flow);

        var vm = BuildStepsViewModel(flow);
        if (posted != null)
        {
            vm.AddKind = posted.AddKind;
            vm.AddOracleID = posted.AddOracleID;
            vm.AddLabel = posted.AddLabel;
            vm.AddQuestion = posted.AddQuestion;
            vm.AddSourceStepSortOrder = posted.AddSourceStepSortOrder;
        }
        return View(nameof(Steps), vm);
    }

    private async Task<IActionResult> OptionsWithError(Flow flow, FlowStep step, string error,
        FlowOptionsViewModel? posted)
    {
        ModelState.AddModelError("", error);

        var vm = await BuildOptionsViewModel(flow, step);
        if (posted != null)
        {
            vm.AddLabel = posted.AddLabel;
            vm.AddOracleID = posted.AddOracleID;
            for (int i = 0; i < vm.Rows.Count && i < posted.Rows.Count; i++)
            {
                vm.Rows[i].Label = posted.Rows[i].Label;
                vm.Rows[i].OracleID = posted.Rows[i].OracleID;
            }
        }
        return View(nameof(Options), vm);
    }

    private FlowStepsViewModel BuildStepsViewModel(Flow flow)
    {
        var ordered = flow.Steps.OrderBy(s => s.SortOrder).ToList();

        return new FlowStepsViewModel
        {
            FlowID = flow.ID,
            FlowName = flow.Name,
            ChoiceSteps = ChoiceTargets(flow),
            Rows = ordered.Select(step => new FlowStepRow
            {
                StepID = step.ID,
                SortOrder = step.SortOrder,
                Kind = step.Kind,
                Label = step.DisplayLabel(),
                Summary = StepSummary(step, flow),
                OracleTypeLabel = step.Oracle?.TypeLabel() ?? string.Empty,
                BlockedReason = step.BlockedReason(flow),
                Warning = StepWarning(step, flow),
                FallThroughLabel = TargetLabel(ordered, step.SortOrder + 1),
                BranchSummaries = step.Kind.CanBranch()
                    ? BranchSummaries(ordered, step)
                    : new List<string>(),
                OptionSummaries = OptionSummaries(step)
            }).ToList()
        };
    }

    private static string StepSummary(FlowStep step, Flow flow)
    {
        switch (step.Kind)
        {
            case FlowStepKind.Roll:
                return step.Oracle?.Name ?? "(missing oracle)";

            case FlowStepKind.Choice:
            case FlowStepKind.Text:
            case FlowStepKind.Number:
                return step.Question ?? "(no question)";

            case FlowStepKind.RollChosen:
                var source = step.SourceStep(flow);
                return source == null
                    ? "No question set"
                    : $"Rolls what step {source.SortOrder} ({source.DisplayLabel()}) picks";
        }

        return string.Empty;
    }

    private static string? StepWarning(FlowStep step, Flow flow)
    {
        if (step.Kind == FlowStepKind.RollChosen)
        {
            var source = step.SourceStep(flow);
            if (source != null && source.SortOrder > step.SortOrder)
            {
                return $"Step {source.SortOrder} comes after this one, so nothing will have been picked by the time this runs.";
            }
        }

        if (step.Kind == FlowStepKind.Choice)
        {
            var without = step.Options.Count(o => o.OracleID == null);
            if (without > 0 && without < step.Options.Count)
            {
                return $"{without} option(s) name no oracle, so picking one leaves a later \"roll what was picked\" step with nothing to roll.";
            }
        }

        return null;
    }

    private static List<string> OptionSummaries(FlowStep step)
    {
        return step.Options
            .OrderBy(o => o.SortOrder)
            .Select(o => o.Oracle == null ? o.Label : $"{o.Label} → {o.Oracle.Name}")
            .ToList();
    }

    private static List<string> BranchSummaries(List<FlowStep> ordered, FlowStep step)
    {
        var entries = (step.Oracle?.OracleEntries ?? new List<OracleEntry>())
            .ToDictionary(e => e.ID);

        return step.Branches
            .Select(b => new
            {
                Branch = b,
                Entry = entries.TryGetValue(b.OracleEntryID, out var e) ? e : null
            })
            // Ordered by the outcome they fire on, so the list reads down the oracle.
            .OrderBy(x => x.Entry?.SortOrder ?? int.MaxValue)
            .Select(x =>
            {
                var key = x.Entry?.KeyLabel() ?? "?";
                var result = x.Entry?.Result;
                var outcome = string.IsNullOrWhiteSpace(result) ? key : $"{key} — {result}";
                return $"{outcome} → {TargetLabel(ordered, x.Branch.TargetStepSortOrder)}";
            })
            .ToList();
    }

    private static string TargetLabel(List<FlowStep> ordered, int? sortOrder)
    {
        if (sortOrder == null) return "ends the flow";

        var step = ordered.FirstOrDefault(s => s.SortOrder == sortOrder.Value);
        return step == null ? "ends the flow" : $"step {step.SortOrder}: {step.DisplayLabel()}";
    }

    private static List<BranchTarget> ChoiceTargets(Flow flow)
    {
        return flow.Steps
            .Where(s => s.Kind == FlowStepKind.Choice)
            .OrderBy(s => s.SortOrder)
            .Select(s => new BranchTarget
            {
                SortOrder = s.SortOrder,
                Label = $"Step {s.SortOrder}: {s.DisplayLabel()}"
            })
            .ToList();
    }

    private static EditStepViewModel BuildEditStepViewModel(Flow flow, FlowStep step)
    {
        return new EditStepViewModel
        {
            FlowID = flow.ID,
            StepID = step.ID,
            FlowName = flow.Name,
            SortOrder = step.SortOrder,
            Kind = step.Kind,
            Label = step.Label,
            Question = step.Question,
            OracleID = step.OracleID,
            SourceStepSortOrder = step.SourceStepSortOrder
        };
    }

    private static FlowBranchesViewModel BuildBranchesViewModel(Flow flow, FlowStep step)
    {
        var ordered = flow.Steps.OrderBy(s => s.SortOrder).ToList();
        var branches = step.Branches.ToDictionary(b => b.OracleEntryID);

        return new FlowBranchesViewModel
        {
            FlowID = flow.ID,
            StepID = step.ID,
            FlowName = flow.Name,
            StepLabel = step.DisplayLabel(),
            StepSortOrder = step.SortOrder,
            CanBranch = step.Kind.CanBranch(),
            KindName = step.Kind.DisplayName(),
            OracleName = step.Oracle?.Name ?? "(missing oracle)",
            IsDice = step.Oracle?.OracleSubtype.Category() == OracleCategory.Dice,
            FallThroughLabel = TargetLabel(ordered, step.SortOrder + 1),
            Targets = ordered
                .Select(s => new BranchTarget
                {
                    SortOrder = s.SortOrder,
                    Label = $"Step {s.SortOrder}: {s.DisplayLabel()}"
                })
                .ToList(),
            Rows = !step.Kind.CanBranch()
                ? new List<BranchRow>()
                : (step.Oracle?.OracleEntries ?? new List<OracleEntry>())
                    .OrderBy(e => e.SortOrder)
                    .Select(e => new BranchRow
                    {
                        EntryID = e.ID,
                        OutcomeKey = e.KeyLabel(),
                        Result = e.Result,
                        Target = branches.TryGetValue(e.ID, out var b)
                            ? (b.TargetStepSortOrder?.ToString() ?? BranchRow.EndTarget)
                            : string.Empty
                    })
                    .ToList()
        };
    }

    private async Task<FlowOptionsViewModel> BuildOptionsViewModel(Flow flow, FlowStep step)
    {
        // Both the per-row picker and the add-one-by-hand form read this.
        ViewData["OracleID"] = await OracleSelectList();

        return new FlowOptionsViewModel
        {
            FlowID = flow.ID,
            StepID = step.ID,
            FlowName = flow.Name,
            StepLabel = step.DisplayLabel(),
            StepSortOrder = step.SortOrder,
            Question = step.Question ?? string.Empty,
            HasRollChosenStep = flow.Steps.Any(s =>
                s.Kind == FlowStepKind.RollChosen && s.SourceStepSortOrder == step.SortOrder),
            Rows = step.Options
                .OrderBy(o => o.SortOrder)
                .Select(o => new OptionRow
                {
                    OptionID = o.ID,
                    SortOrder = o.SortOrder,
                    Label = o.Label,
                    OracleID = o.OracleID
                })
                .ToList(),
            Available = await OracleListItems()
        };
    }

    private async Task<FlowIndexViewModel> BuildFlowIndex(string? name, int? gameSystemId, int page)
    {
        if (gameSystemId is <= 0) gameSystemId = null;
        var term = string.IsNullOrWhiteSpace(name) ? null : name.Trim();

        var systems = await _context.GameSystems.OrderBy(g => g.Name).ToListAsync();
        var filtered = FlowListQuery.ApplyFilters(_context.Flows, term, gameSystemId);

        return new FlowIndexViewModel
        {
            Name = term,
            GameSystemID = gameSystemId,
            CatalogEmpty = !await _context.Flows.AnyAsync(),
            Results = await FlowListQuery.ToPagedListAsync(filtered, page),
            GameSystems = FilterSelectLists.GameSystems(systems)
        };
    }

    private Task<Flow?> LoadFlow(int? id)
    {
        return _context.Flows
            .Include(f => f.GameSystem)
            .Include(f => f.Steps).ThenInclude(s => s.Branches)
            .Include(f => f.Steps).ThenInclude(s => s.Oracle).ThenInclude(o => o!.OracleEntries)
            .Include(f => f.Steps).ThenInclude(s => s.Options).ThenInclude(o => o.Oracle)
                .ThenInclude(o => o!.OracleEntries)
            .AsSplitQuery()
            .FirstOrDefaultAsync(f => f.ID == id);
    }

    private async Task<(Flow flow, FlowStep step)?> LocateStep(int? stepId)
    {
        var stub = await _context.FlowSteps.AsNoTracking().FirstOrDefaultAsync(s => s.ID == stepId);
        if (stub == null) return null;

        var flow = await LoadFlow(stub.FlowID);
        var step = flow?.Steps.FirstOrDefault(s => s.ID == stub.ID);

        return flow == null || step == null ? null : (flow, step);
    }

    private async Task PopulateStepPickers(Flow flow, int? selectedSource = null)
    {
        ViewData["OracleID"] = await OracleSelectList();
        ViewData["AddOracleID"] = await OracleSelectList();
        ViewData["AddKind"] = KindSelectList();
        ViewData["SourceStepSortOrder"] = ChoiceStepSelectList(flow, selectedSource);
        ViewData["AddSourceStepSortOrder"] = ChoiceStepSelectList(flow, selectedSource);
    }

    private static SelectList KindSelectList(FlowStepKind? selected = null)
    {
        var items = Enum.GetValues<FlowStepKind>()
            .Select(k => new { Value = k.ToString(), Text = k.DisplayName() })
            .ToList();

        return new SelectList(items, "Value", "Text", (selected ?? FlowStepKind.Roll).ToString());
    }

    private static SelectList ChoiceStepSelectList(Flow flow, int? selected)
    {
        return new SelectList(ChoiceTargets(flow), "SortOrder", "Label", selected);
    }

    private async Task<SelectList> OracleSelectList()
    {
        var items = (await OracleListItems())
            .Select(o => new
            {
                o.ID,
                Text = o.NeedsSetup
                    ? $"{o.Name} — {o.GameSystemName} (needs setup)"
                    : $"{o.Name} — {o.GameSystemName}"
            })
            .ToList();

        return new SelectList(items, "ID", "Text");
    }

    private Task<List<OracleListItem>> OracleListItems()
    {
        return OracleListQuery.ToListItemsAsync(_context.Oracles);
    }

    private async Task<SelectList> GameSystemSelectList(int? selected = null)
    {
        var systems = await _context.GameSystems
            .OrderBy(g => g.Name)
            .ToListAsync();
        return new SelectList(systems, "ID", "Name", selected);
    }
}
