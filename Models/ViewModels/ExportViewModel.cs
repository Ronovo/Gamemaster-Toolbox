namespace GM_Toolbox.Models.ViewModels
{
    public enum ExportScope
    {
        All = 0,
        Selected = 1
    }

    /// <summary>
    /// The Export page: what to export, and the list to pick from.
    ///
    /// Only Scope and SelectedIds come back from the form. Available is display data and is
    /// rebuilt from the database on every render — including the validation-failure redisplay,
    /// which is the path that is easy to forget and shows up as an empty list on the error page.
    /// </summary>
    public class ExportViewModel
    {
        public ExportScope Scope { get; set; } = ExportScope.All;

        /// <summary>
        /// The ticked oracles. Each checkbox is named SelectedIds and carries an oracle's ID,
        /// so an unticked box posts nothing and no hidden fields are needed — unlike the
        /// indexed Rows[n] binding EditEntriesViewModel uses.
        ///
        /// Ignored entirely when Scope is All, so a stale tick cannot narrow a full export.
        /// </summary>
        public List<int> SelectedIds { get; set; } = new List<int>();

        /// <summary>Every oracle that could be exported. Never trusted from the form.</summary>
        public List<OracleListItem> Available { get; set; } = new List<OracleListItem>();
    }
}
