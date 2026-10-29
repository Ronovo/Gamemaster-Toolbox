using GM_Toolbox.Data;
using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using GM_Toolbox.Models.ViewModels;
using GM_Toolbox.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

// Reading and running are open to everyone; anything that changes data needs an account.
[Authorize]
public class FlowsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly FlowConfigurator _configurator;

    public FlowsController(ApplicationDbContext context, FlowConfigurator configurator)
    {
        _context = context;
        _configurator = configurator;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(string? name, int? gameSystemId, int page = 1)
    {
        var vm = await BuildFlowIndex(name, gameSystemId, page);
        if (string.Equals(Request.Query["partial"], "1", StringComparison.OrdinalIgnoreCase))
        {
            return PartialView("_FlowListResults", vm);
        }

        return View(vm);
    }

    [AllowAnonymous]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        var flow = await LoadFlow(id);
        if (flow == null) return NotFound();

        return View(flow);
    }

    public async Task<IActionResult> Create()
    {
        ViewData["OracleCount"] = await _context.Oracles.CountAsync();
        ViewData["GameSystemID"] = await GameSystemSelectList();
        return View(new Flow());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description,GameSystemID,Visibility")] Flow flow)
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
    public async Task<IActionResult> Edit(int? id, [Bind("ID,Name,Description,GameSystemID,Visibility")] Flow flow)
    {
        if (id != flow.ID) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewData["GameSystemID"] = await GameSystemSelectList(flow.GameSystemID);
            return View(flow);
        }

        var existing = await _context.Flows.FirstOrDefaultAsync(f => f.ID == id);
        if (existing == null) return NotFound();

        existing.Name = flow.Name;
        existing.Description = flow.Description;
        existing.GameSystemID = flow.GameSystemID;
        existing.Visibility = flow.Visibility;

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
            vm.AddQuestion, vm.AddSourceStepID);
        if (error != null)
        {
            return await StepsWithError(flow, error, vm);
        }

        await _context.SaveChangesAsync();

        var added = flow.Steps.OrderByDescending(s => s.ID).First();
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
                $"Step removed. {orphaned} reference(s) to it have been cleared.";
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
        await PopulateStepPickers(flow, step.SourceStepID);
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
            vm.SourceStepID);
        if (error != null)
        {
            ModelState.AddModelError("", error);
            await PopulateStepPickers(flow, vm.SourceStepID);

            var rebuilt = BuildEditStepViewModel(flow, step);
            rebuilt.Label = vm.Label;
            rebuilt.Question = vm.Question;
            rebuilt.OracleID = vm.OracleID;
            rebuilt.SourceStepID = vm.SourceStepID;
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
        return View(await BuildBranchesViewModel(flow, step));
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
            var rebuilt = await BuildBranchesViewModel(flow, step);
            for (int i = 0; i < rebuilt.Rows.Count && i < vm.Rows.Count; i++)
            {
                rebuilt.Rows[i].OracleID = vm.Rows[i].OracleID;
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

        var error = _configurator.SetOptions(flow, step, vm.Rows);
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

    [AllowAnonymous]
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
    [AllowAnonymous]
    public async Task<IActionResult> RollStep(int id, string? answer)
    {
        var flow = await LoadFlow(id);
        if (flow == null) return NotFound();

        var blocked = flow.RunBlockedReason();
        if (blocked != null)
        {
            return RunResponse(flow, null, null, null, blocked);
        }

        var state = FlowRunner.Load(flow, HttpContext.Session);
        var error = FlowRunner.TryAdvance(flow, state, HttpContext.Session, answer, out var rolled);

        if (error != null || rolled == null)
        {
            FlowRunner.Save(state, HttpContext.Session);
            return RunResponse(flow, null, null, null, error ?? "That step produced no outcome.");
        }

        FlowRunner.Save(state, HttpContext.Session);
        return RunResponse(flow, rolled, FlowRunner.CurrentStep(flow, state), state, null);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
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
                next = NextDescriptor(flow, FlowRunner.CurrentStep(flow, fresh)),
                finished = fresh.Finished
            });
        }

        return RedirectToAction(nameof(Run), new { id });
    }

    private bool IsAjaxRequest => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

    private IActionResult RunResponse(Flow flow, FlowRunStep? rolled, FlowStep? next,
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
                outcomes = rolled.Outcomes,
                isQuestion = rolled.IsQuestion,
                next = NextDescriptor(flow, next),
                finished = state!.Finished,
                hitStepLimit = state.HitStepLimit
            });
        }

        if (error != null)
        {
            TempData["FlowRunError"] = error;
        }

        return RedirectToAction(nameof(Run), new { id = flow.ID });
    }

    private static object? NextDescriptor(Flow flow, FlowStep? step)
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
            question = step.Prompt(flow),
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
            vm.AddSourceStepID = posted.AddSourceStepID;
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
                vm.Rows[i].Target = posted.Rows[i].Target;
            }
        }
        return View(nameof(Options), vm);
    }

    private FlowStepsViewModel BuildStepsViewModel(Flow flow)
    {
        var ordered = flow.InRunOrder().ToList();

        var rows = ordered.Select((step, i) => new FlowStepRow
        {
            StepID = step.ID,
            PositionPath = step.PositionPath(),
            Kind = step.Kind,
            Label = step.DisplayLabel(),
            Summary = StepSummary(step, flow),
            OracleTypeLabel = step.Oracle?.TypeLabel() ?? string.Empty,
            BlockedReason = step.BlockedReason(flow),
            Warning = StepWarning(step, flow),
            FallThroughLabel = FallThroughLabel(flow, step),
            BranchSummaries = step.AllowsBranches()
                ? BranchSummaries(flow, step)
                : new List<string>(),
            OptionSummaries = OptionSummaries(flow, step),
            IsFirst = i == 0,
            IsLast = i == ordered.Count - 1
        }).ToList();

        return new FlowStepsViewModel
        {
            FlowID = flow.ID,
            FlowName = flow.Name,
            SourceSteps = SourceTargets(flow),
            Rows = rows
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
                return step.Question ?? "(no question)";

            case FlowStepKind.RollChosen:
                var source = step.SourceStep(flow);
                return source == null
                    ? "No source set"
                    : $"Rolls what {source.PositionPath()} ({source.DisplayLabel()}) picks";

            case FlowStepKind.Loop:
                var next = step.NextInOrder(flow);
                return next == null
                    ? "Nothing to loop"
                    : $"Loops {next.PositionPath()} ({next.DisplayLabel()})";
        }

        return string.Empty;
    }

    private static string? StepWarning(FlowStep step, Flow flow)
    {
        if (step.Kind == FlowStepKind.RollChosen)
        {
            var source = step.SourceStep(flow);
            if (source != null && !StepRunsBefore(flow, source, step))
            {
                return $"{source.PositionPath()} does not run before this step, so nothing will have been picked by the time this runs.";
            }
        }

        if (step.Kind == FlowStepKind.Choice)
        {
            var without = step.Options.Count(o => o.OracleID == null);
            if (without > 0 && without < step.Options.Count)
            {
                return $"{without} option(s) name no oracle, so picking one leaves a later \"roll on another oracle\" step with nothing to roll.";
            }
        }

        return null;
    }

    private static bool StepRunsBefore(Flow flow, FlowStep a, FlowStep b)
    {
        foreach (var step in flow.InRunOrder())
        {
            if (step.ID == a.ID) return true;
            if (step.ID == b.ID) return false;
        }
        return false;
    }

    private static List<string> OptionSummaries(Flow flow, FlowStep step)
    {
        return step.Options
            .OrderBy(o => o.SortOrder)
            .Select(o =>
            {
                var text = o.Oracle == null ? o.Label : $"{o.Label} → {o.Oracle.Name}";
                if (!o.Routes)
                {
                    return text;
                }
                return $"{text} → {(o.EndsFlow ? "ends the flow" : TargetLabel(flow, o.TargetStepID))}";
            })
            .ToList();
    }

    private static List<string> BranchSummaries(Flow flow, FlowStep step)
    {
        var entries = (step.Oracle?.OracleEntries ?? new List<OracleEntry>())
            .ToDictionary(e => e.ID);

        return step.Branches
            .Select(b => new
            {
                Branch = b,
                Entry = entries.TryGetValue(b.OracleEntryID, out var e) ? e : null
            })
            .OrderBy(x => x.Entry?.SortOrder ?? int.MaxValue)
            .Select(x =>
            {
                var key = x.Entry?.KeyLabel() ?? "?";
                var result = x.Entry?.Result;
                var outcome = string.IsNullOrWhiteSpace(result) ? key : $"{key} — {result}";

                var parts = new List<string>();
                if (x.Branch.Oracle != null)
                {
                    parts.Add($"picks {x.Branch.Oracle.Name}");
                }
                if (x.Branch.EndsFlow)
                {
                    parts.Add("ends the flow");
                }
                else if (x.Branch.TargetStepID != null)
                {
                    parts.Add($"goes to {TargetLabel(flow, x.Branch.TargetStepID)}");
                }

                return parts.Count == 0 ? outcome : $"{outcome} → {string.Join(", ", parts)}";
            })
            .ToList();
    }

    private static string TargetLabel(Flow flow, int? stepId)
    {
        if (stepId == null) return "ends the flow";

        var step = flow.Steps.FirstOrDefault(s => s.ID == stepId.Value);
        return step == null ? "ends the flow" : $"{step.PositionPath()}: {step.DisplayLabel()}";
    }

    private static string FallThroughLabel(Flow flow, FlowStep step)
    {
        var next = step.NextInOrder(flow);
        return next != null
            ? $"{next.PositionPath()}: {next.DisplayLabel()}"
            : "ends the flow";
    }

    private static List<BranchTarget> SourceTargets(Flow flow)
    {
        return flow.InRunOrder()
            .Where(s => s.Kind == FlowStepKind.Choice || s.Kind == FlowStepKind.Roll)
            .Select(s => new BranchTarget
            {
                StepID = s.ID,
                Label = $"{s.PositionPath()}: {s.DisplayLabel()}"
            })
            .ToList();
    }

    private static List<BranchTarget> AllTargets(Flow flow)
    {
        return flow.InRunOrder()
            .Select(s => new BranchTarget
            {
                StepID = s.ID,
                Label = $"{s.PositionPath()}: {s.DisplayLabel()}"
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
            PositionPath = step.PositionPath(),
            Kind = step.Kind,
            Label = step.Label,
            Question = step.Question,
            OracleID = step.OracleID,
            SourceStepID = step.SourceStepID
        };
    }

    private async Task<FlowBranchesViewModel> BuildBranchesViewModel(Flow flow, FlowStep step)
    {
        var branches = step.Branches.ToDictionary(b => b.OracleEntryID);

        return new FlowBranchesViewModel
        {
            FlowID = flow.ID,
            StepID = step.ID,
            FlowName = flow.Name,
            StepLabel = step.DisplayLabel(),
            PositionPath = step.PositionPath(),
            CanBranch = step.AllowsBranches(),
            KindName = step.Kind.DisplayName(),
            OracleName = step.Oracle?.Name ?? "(missing oracle)",
            IsDice = step.Oracle?.OracleSubtype.Category() == OracleCategory.Dice,
            FallThroughLabel = FallThroughLabel(flow, step),
            Targets = AllTargets(flow),
            Oracles = await OracleListItems(),
            Rows = !step.Kind.CanBranch()
                ? new List<BranchRow>()
                : (step.Oracle?.OracleEntries ?? new List<OracleEntry>())
                    .OrderBy(e => e.SortOrder)
                    .Select(e => new BranchRow
                    {
                        EntryID = e.ID,
                        OutcomeKey = e.KeyLabel(),
                        Result = e.Result,
                        OracleID = branches.TryGetValue(e.ID, out var b) ? b.OracleID : null,
                        Target = branches.TryGetValue(e.ID, out var wb)
                            ? (wb.EndsFlow ? BranchRow.EndTarget : wb.TargetStepID?.ToString() ?? string.Empty)
                            : string.Empty
                    })
                    .ToList()
        };
    }

    private async Task<FlowOptionsViewModel> BuildOptionsViewModel(Flow flow, FlowStep step)
    {
        ViewData["OracleID"] = await OracleSelectList();

        return new FlowOptionsViewModel
        {
            FlowID = flow.ID,
            StepID = step.ID,
            FlowName = flow.Name,
            StepLabel = step.DisplayLabel(),
            PositionPath = step.PositionPath(),
            Question = step.Question ?? string.Empty,
            HasRollChosenStep = flow.Steps.Any(s =>
                s.Kind == FlowStepKind.RollChosen && s.SourceStepID == step.ID),
            Rows = step.Options
                .OrderBy(o => o.SortOrder)
                .Select(o => new OptionRow
                {
                    OptionID = o.ID,
                    SortOrder = o.SortOrder,
                    Label = o.Label,
                    OracleID = o.OracleID,
                    Target = o.EndsFlow
                        ? BranchRow.EndTarget
                        : o.TargetStepID?.ToString() ?? string.Empty
                })
                .ToList(),
            Available = await OracleListItems(),
            Targets = AllTargets(flow),
            FallThroughLabel = FallThroughLabel(flow, step)
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
            .Include(f => f.Steps).ThenInclude(s => s.Branches).ThenInclude(b => b.Oracle)
                .ThenInclude(o => o!.OracleEntries)
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
        ViewData["SourceStepID"] = SourceStepSelectList(flow, selectedSource);
        ViewData["AddSourceStepID"] = SourceStepSelectList(flow, selectedSource);
    }

    private static SelectList KindSelectList(FlowStepKind? selected = null)
    {
        var items = Enum.GetValues<FlowStepKind>()
            .Select(k => new { Value = k.ToString(), Text = k.DisplayName() })
            .ToList();

        return new SelectList(items, "Value", "Text", (selected ?? FlowStepKind.Roll).ToString());
    }

    private static SelectList SourceStepSelectList(Flow flow, int? selected)
    {
        return new SelectList(SourceTargets(flow), "StepID", "Label", selected);
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
