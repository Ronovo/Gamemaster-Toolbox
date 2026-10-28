using GM_Toolbox.Models;
using GM_Toolbox.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GM_Toolbox.Data
{
    /// <summary>
    /// A save that breaks a rule in <see cref="OwnershipRules"/>. The message is shown to the user.
    /// </summary>
    public class OwnershipException(string message) : Exception(message);

    /// <summary>
    /// Who may change oracles, flows and game systems, checked on every save so no action can skip it.
    /// New ones belong to the account that made them. Only that account may change or delete one, and
    /// any signed-in account may change one from before accounts, which has no owner. Something everyone
    /// can see may only lean on things everyone can see: its game system, and for a flow every oracle it
    /// rolls or picks. Otherwise someone could open a public flow that rolls on an oracle hidden from them.
    /// </summary>
    internal class OwnershipRules(ApplicationDbContext context)
    {
        private const string NotYours = "Only the account that made this can change it.";
        private const string HiddenSystem = "A game system only you can see can't hold an oracle or flow everyone can see.";
        private const string HiddenOracle = "A flow everyone can see can only use oracles everyone can see.";
        private const string UsedPublicly = "This oracle is used by a flow everyone can see, so everyone has to be able to see it.";

        private static readonly HashSet<Type> Guarded =
        [
            typeof(Oracle), typeof(OracleEntry), typeof(GameSystem),
            typeof(Flow), typeof(FlowStep), typeof(FlowChoiceOption), typeof(FlowBranch)
        ];

        public async Task CheckAsync(CancellationToken ct)
        {
            var changed = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                            && Guarded.Contains(e.Entity.GetType()))
                .ToList();
            if (changed.Count == 0)
            {
                return;
            }

            var userId = context.CurrentUserId ?? throw new OwnershipException("Log in to make changes.");

            foreach (var entry in changed.Where(e => e.Entity is IOwned))
            {
                var owned = (IOwned)entry.Entity;
                if (entry.State == EntityState.Added)
                {
                    owned.OwnerId ??= userId;
                }
                else
                {
                    CheckOwner(entry.OriginalValues.GetValue<string?>(nameof(IOwned.OwnerId)), userId);
                    owned.OwnerId = entry.OriginalValues.GetValue<string?>(nameof(IOwned.OwnerId));
                }
            }

            foreach (var entry in changed)
            {
                switch (entry.Entity)
                {
                    case OracleEntry e:
                        CheckOwner((await OracleAsync(e.OracleID, ct))?.OwnerId, userId);
                        break;
                    case FlowStep s:
                        CheckOwner((s.Flow ?? await FlowAsync(s.FlowID, ct))?.OwnerId, userId);
                        break;
                    case FlowChoiceOption o:
                        CheckOwner((await FlowOfStepAsync(o.FlowStep, o.FlowStepID, ct))?.OwnerId, userId);
                        break;
                    case FlowBranch b:
                        CheckOwner((await FlowOfStepAsync(b.FlowStep, b.FlowStepID, ct))?.OwnerId, userId);
                        break;
                }
            }

            foreach (var entry in changed.Where(e => e.State != EntityState.Deleted))
            {
                await CheckVisibilityAsync(entry, ct);
            }
        }

        private static void CheckOwner(string? ownerId, string userId)
        {
            if (ownerId != null && ownerId != userId)
            {
                throw new OwnershipException(NotYours);
            }
        }

        private async Task CheckVisibilityAsync(EntityEntry entry, CancellationToken ct)
        {
            switch (entry.Entity)
            {
                case Oracle oracle:
                    if (oracle.Visibility == Visibility.Everyone)
                    {
                        await RequirePublicSystemAsync(oracle.GameSystemID, ct);
                    }
                    else if (entry.State == EntityState.Modified && await UsedByPublicFlowAsync(oracle.ID, ct))
                    {
                        throw new OwnershipException(UsedPublicly);
                    }
                    break;
                case Flow flow when flow.Visibility == Visibility.Everyone:
                    await RequirePublicSystemAsync(flow.GameSystemID, ct);
                    if (entry.State == EntityState.Modified && await UsesHiddenOracleAsync(flow.ID, ct))
                    {
                        throw new OwnershipException(HiddenOracle);
                    }
                    break;
                case GameSystem system when system.Visibility == Visibility.OnlyMe && entry.State == EntityState.Modified:
                    if (await context.Oracles.IgnoreQueryFilters().AnyAsync(o => o.GameSystemID == system.ID && o.Visibility == Visibility.Everyone, ct)
                        || await context.Flows.IgnoreQueryFilters().AnyAsync(f => f.GameSystemID == system.ID && f.Visibility == Visibility.Everyone, ct))
                    {
                        throw new OwnershipException(HiddenSystem);
                    }
                    break;
                case FlowStep s:
                    await RequirePublicOracleInAsync((s.Flow ?? await FlowAsync(s.FlowID, ct))?.Visibility, s.Oracle?.ID ?? s.OracleID, ct);
                    break;
                case FlowChoiceOption o:
                    await RequirePublicOracleInAsync((await FlowOfStepAsync(o.FlowStep, o.FlowStepID, ct))?.Visibility, o.Oracle?.ID ?? o.OracleID, ct);
                    break;
                case FlowBranch b:
                    await RequirePublicOracleInAsync((await FlowOfStepAsync(b.FlowStep, b.FlowStepID, ct))?.Visibility, b.Oracle?.ID ?? b.OracleID, ct);
                    break;
            }
        }

        private async Task RequirePublicSystemAsync(int gameSystemId, CancellationToken ct)
        {
            var system = Tracked<GameSystem>(s => s.ID == gameSystemId)
                ?? await context.GameSystems.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.ID == gameSystemId, ct);
            if (system != null && system.Visibility != Visibility.Everyone)
            {
                throw new OwnershipException(HiddenSystem);
            }
        }

        private async Task RequirePublicOracleInAsync(Visibility? flowVisibility, int? oracleId, CancellationToken ct)
        {
            if (flowVisibility == Visibility.Everyone && oracleId != null
                && (await OracleAsync(oracleId.Value, ct))?.Visibility != Visibility.Everyone)
            {
                throw new OwnershipException(HiddenOracle);
            }
        }

        private Task<bool> UsedByPublicFlowAsync(int oracleId, CancellationToken ct) =>
            context.Flows.IgnoreQueryFilters().AnyAsync(f => f.Visibility == Visibility.Everyone && f.Steps.Any(s =>
                s.OracleID == oracleId
                || s.Options.Any(o => o.OracleID == oracleId)
                || s.Branches.Any(b => b.OracleID == oracleId)), ct);

        private Task<bool> UsesHiddenOracleAsync(int flowId, CancellationToken ct) =>
            context.FlowSteps.IgnoreQueryFilters().Where(s => s.FlowID == flowId).AnyAsync(s =>
                (s.Oracle != null && s.Oracle.Visibility != Visibility.Everyone)
                || s.Options.Any(o => o.Oracle != null && o.Oracle.Visibility != Visibility.Everyone)
                || s.Branches.Any(b => b.Oracle != null && b.Oracle.Visibility != Visibility.Everyone), ct);

        private async Task<Oracle?> OracleAsync(int id, CancellationToken ct) =>
            Tracked<Oracle>(o => o.ID == id)
            ?? await context.Oracles.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(o => o.ID == id, ct);

        private async Task<Flow?> FlowAsync(int id, CancellationToken ct) =>
            Tracked<Flow>(f => f.ID == id)
            ?? await context.Flows.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(f => f.ID == id, ct);

        private async Task<Flow?> FlowOfStepAsync(FlowStep? loaded, int stepId, CancellationToken ct)
        {
            var step = loaded ?? Tracked<FlowStep>(s => s.ID == stepId)
                ?? await context.FlowSteps.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(s => s.ID == stepId, ct);
            return step == null ? null : step.Flow ?? await FlowAsync(step.FlowID, ct);
        }

        // Something added in this same save (an import) has no row to look up yet, so the tracked copy is used.
        private T? Tracked<T>(Func<T, bool> match) where T : class =>
            context.ChangeTracker.Entries<T>().Select(e => e.Entity).FirstOrDefault(match);
    }
}
