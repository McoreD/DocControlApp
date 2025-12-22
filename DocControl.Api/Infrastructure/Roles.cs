namespace DocControl.Api.Infrastructure;

public static class Roles
{
    public const string Owner = "Owner";
    public const string Contributor = "Contributor";
    public const string Viewer = "Viewer";

    public static int Compare(string current, string required)
    {
        return Rank(current).CompareTo(Rank(required));
    }

    private static int Rank(string role) => role switch
    {
        Owner => 3,
        Contributor => 2,
        Viewer => 1,
        _ => 0
    };
}
