namespace GM_Toolbox.Services
{
    /// <summary>The length limits on a flow's words, shared by the flow editor and the importer.</summary>
    public static class FlowText
    {
        public const int MaxLabel = 100;
        public const int MaxQuestion = 200;

        public static string? LabelProblem(string? label, string what) =>
            label != null && label.Length > MaxLabel ? $"{what} cannot be longer than {MaxLabel} characters." : null;

        public static string? QuestionProblem(string? question) =>
            string.IsNullOrWhiteSpace(question) ? "Type the question this step asks."
            : question.Length > MaxQuestion ? $"A question cannot be longer than {MaxQuestion} characters."
            : null;
    }
}
