using GM_Toolbox.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace GM_Toolbox.Data
{
    /// <summary>
    /// A save that breaks a rule in <see cref="OwnershipRules"/>. The message is shown to the user.
    /// </summary>
    public class OwnershipException(string message) : Exception(message);

    /// <summary>
    /// Checked on every save so no action can skip it: anything added, changed or deleted must belong to the
    /// signed-in account, and so must anything it points at (a game system, or the oracles a flow uses).
    /// New items belong to the account that made them.
    /// </summary>
    internal class OwnershipRules(ApplicationDbContext context)
    {
        private const string NotYours = "Only the account that made this can change it.";
        private const string NotYourReference = "You can only use your own game systems and oracles.";

        private static readonly HashSet<Type> Guarded =
        [
            typeof(GameSystem), typeof(Oracle), typeof(OracleEntry), typeof(Sheet), typeof(SheetVersion),
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
                    CheckOwner(owned.OwnerId, userId, NotYours);
                }
                else
                {
                    var original = entry.OriginalValues.GetValue<string?>(nameof(IOwned.OwnerId));
                    CheckOwner(original, userId, NotYours);
                    owned.OwnerId = original;
                }
            }

            foreach (var entry in changed)
            {
                switch (entry.Entity)
                {
                    case OracleEntry:
                        await RequireMineAsync<Oracle>(Key(entry, nameof(OracleEntry.OracleID)), userId, NotYours, ct);
                        break;
                    case SheetVersion:
                        await RequireMineAsync<Sheet>(Key(entry, nameof(SheetVersion.SheetID)), userId, NotYours, ct);
                        break;
                    case FlowStep:
                        await RequireMineAsync<Flow>(Key(entry, nameof(FlowStep.FlowID)), userId, NotYours, ct);
                        break;
                    case FlowChoiceOption or FlowBranch:
                        await RequireMineAsync<Flow>(await FlowOfStepAsync(Key(entry, nameof(FlowBranch.FlowStepID)), ct), userId, NotYours, ct);
                        break;
                }

                if (entry.State == EntityState.Deleted)
                {
                    continue;
                }
                switch (entry.Entity)
                {
                    case Oracle or Flow or Sheet:
                        await RequireMineAsync<GameSystem>(Key(entry, nameof(Oracle.GameSystemID)), userId, NotYourReference, ct);
                        break;
                    case FlowStep or FlowChoiceOption or FlowBranch:
                        await RequireMineAsync<Oracle>(Key(entry, nameof(FlowStep.OracleID)), userId, NotYourReference, ct);
                        break;
                }
            }
        }

        private static void CheckOwner(string? ownerId, string userId, string message)
        {
            if (ownerId != userId)
            {
                throw new OwnershipException(message);
            }
        }

        // Keys are read through the change tracker: something added in this same save (an import) only has a
        // temporary key, which EF keeps there rather than on the object.
        private static object? Key(EntityEntry entry, string property) => entry.Property(property).CurrentValue;

        private EntityEntry<T>? TrackedById<T>(object id) where T : class =>
            context.ChangeTracker.Entries<T>().FirstOrDefault(e => Equals(e.Property("ID").CurrentValue, id));

        private async Task RequireMineAsync<T>(object? id, string userId, string message, CancellationToken ct)
            where T : class, IOwned
        {
            if (id == null)
            {
                return;
            }
            var owner = TrackedById<T>(id)?.Entity.OwnerId
                ?? await context.Set<T>().IgnoreQueryFilters()
                    .Where(e => EF.Property<int>(e, "ID") == (int)id)
                    .Select(e => e.OwnerId)
                    .FirstOrDefaultAsync(ct);
            CheckOwner(owner, userId, message);
        }

        private async Task<object?> FlowOfStepAsync(object? stepId, CancellationToken ct)
        {
            if (stepId == null)
            {
                return null;
            }
            var tracked = TrackedById<FlowStep>(stepId);
            return tracked != null
                ? Key(tracked, nameof(FlowStep.FlowID))
                : await context.FlowSteps.IgnoreQueryFilters()
                    .Where(s => s.ID == (int)stepId)
                    .Select(s => s.FlowID)
                    .FirstOrDefaultAsync(ct);
        }
    }
}
