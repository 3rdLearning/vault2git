using System.Globalization;

namespace GitLib.Interfaces
{
    public interface IGitCommitHash
    {
        string ToString();
        string ToString(bool followReplacement);
        string ToString(CultureInfo cultureInfo);
        string ToString(bool followReplacement, CultureInfo cultureInfo);

        IGitCommitHash Replace(IGitCommitHash replacementGitCommitHash);

    }
}
