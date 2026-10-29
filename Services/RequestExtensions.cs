namespace GM_Toolbox.Services
{
    public static class RequestExtensions
    {
        /// <summary>The list pages' filters ask for just the results table with ?partial=1.</summary>
        public static bool WantsPartial(this HttpRequest request) =>
            string.Equals(request.Query["partial"], "1", StringComparison.OrdinalIgnoreCase);

        public static bool IsAjax(this HttpRequest request) =>
            request.Headers["X-Requested-With"] == "XMLHttpRequest";
    }
}
