using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;

namespace GM_Toolbox.Services;

// Security questions for password resets. The same 30 questions are in every portfolio app.
// They are kept as Identity user tokens rather than claims, because claims are copied into the
// login cookie and the answer hash has no business there.
public class SecurityQuestions(UserManager<IdentityUser> users, IPasswordHasher<IdentityUser> hasher)
{
    public static readonly IReadOnlyList<string> All =
    [
        "What is your mother's maiden name?",
        "What was the name of your first pet?",
        "In what city were you born?",
        "What was the make and model of your first car?",
        "What was the name of your elementary school?",
        "What was the name of your childhood best friend?",
        "Who was your favorite teacher?",
        "What street did you grow up on?",
        "What is your father's middle name?",
        "What was your childhood nickname?",
        "In what city did your parents meet?",
        "What was the first concert you went to?",
        "What was the name of your first employer?",
        "In what hospital were you born?",
        "What was your favorite food as a child?",
        "What was the first video game you played?",
        "What is your oldest sibling's middle name?",
        "What was the name of your first stuffed animal?",
        "What was your high school mascot?",
        "In what city or town was your first job?",
        "What was the last name of your third grade teacher?",
        "What is your maternal grandmother's first name?",
        "What was your dream job as a child?",
        "What was your favorite book as a child?",
        "What is your favorite movie?",
        "Where did you go on your first vacation?",
        "What was the name of your first roommate?",
        "What was the first album you bought?",
        "What is your paternal grandfather's first name?",
        "What sport did you play most as a child?",
    ];

    public const int MaxAnswerLength = 100;
    private const string Store = "SecurityQuestion";
    private const int MaxFailures = 5;
    private static readonly TimeSpan LockTime = TimeSpan.FromMinutes(15);

    // A username with no account shows the same stand-in question until the app restarts.
    private static readonly byte[] StandInKey = RandomNumberGenerator.GetBytes(32);

    /// <summary>Answers match ignoring capitals and extra spaces.</summary>
    public static string Normalize(string? answer) =>
        string.Join(' ', (answer ?? "").ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    public static string? Problem(string? question, string? answer)
    {
        if (question == null || !All.Contains(question)) return "Pick a security question.";
        var normalized = Normalize(answer);
        if (normalized.Length == 0) return "Answer your security question.";
        if (normalized.Length > MaxAnswerLength) return $"Keep the security answer to {MaxAnswerLength} characters or fewer.";
        return null;
    }

    public async Task<bool> HasAsync(IdentityUser user) =>
        await users.GetAuthenticationTokenAsync(user, Store, "answer") != null;

    public async Task SetAsync(IdentityUser user, string question, string answer)
    {
        await users.SetAuthenticationTokenAsync(user, Store, "question", question);
        await users.SetAuthenticationTokenAsync(user, Store, "answer", hasher.HashPassword(user, Normalize(answer)));
    }

    public async Task<string> QuestionForAsync(string userName)
    {
        var user = await users.FindByNameAsync(userName);
        var question = user == null || !await HasAsync(user) ? null : await users.GetAuthenticationTokenAsync(user, Store, "question");
        if (question != null) return question;
        var digest = HMACSHA256.HashData(StandInKey, Encoding.UTF8.GetBytes(userName.Trim().ToLowerInvariant()));
        return All[(int)(BitConverter.ToUInt32(digest, 0) % (uint)All.Count)];
    }

    /// <summary>
    /// Sets a new password when the security answer is right, which also logs the account out
    /// everywhere. Too many wrong answers lock resets for a while. Returns what went wrong, or null.
    /// </summary>
    public async Task<string?> ResetAsync(string userName, string? answer, string newPassword)
    {
        var user = await users.FindByNameAsync(userName);
        var answerHash = user == null ? null : await users.GetAuthenticationTokenAsync(user, Store, "answer");
        if (user == null || answerHash == null)
        {
            hasher.HashPassword(new IdentityUser(), Normalize(answer));
            return "That answer is wrong.";
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (long.TryParse(await users.GetAuthenticationTokenAsync(user, Store, "locked_until"), out var lockedUntil) && now < lockedUntil)
        {
            return "Too many wrong answers. Try again in 15 minutes.";
        }
        if (hasher.VerifyHashedPassword(user, answerHash, Normalize(answer)) == PasswordVerificationResult.Failed)
        {
            var failures = (int.TryParse(await users.GetAuthenticationTokenAsync(user, Store, "failures"), out var n) ? n : 0) + 1;
            var locked = failures >= MaxFailures;
            await users.SetAuthenticationTokenAsync(user, Store, "failures", (locked ? 0 : failures).ToString());
            await users.SetAuthenticationTokenAsync(user, Store, "locked_until", locked ? (now + (long)LockTime.TotalSeconds).ToString() : "0");
            return "That answer is wrong.";
        }

        var reset = await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), newPassword);
        if (!reset.Succeeded)
        {
            return string.Join(" ", reset.Errors.Select(e => e.Description));
        }
        await users.SetAuthenticationTokenAsync(user, Store, "failures", "0");
        return null;
    }
}
