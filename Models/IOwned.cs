namespace GM_Toolbox.Models
{
    /// <summary>
    /// Belongs to the account that made it, and only that account can see it.
    /// </summary>
    public interface IOwned
    {
        string? OwnerId { get; set; }
    }
}
