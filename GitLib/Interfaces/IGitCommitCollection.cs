namespace GitLib.Interfaces
{
    public interface IGitCommitCollection
    {
        IGitCommit this[string commitHash] { get; }

        IGitCommit AddCommit(byte[] commitHashBytes);
        IGitCommit AddCommit(IGitCommitHash gitCommitHash);
        IGitCommit AddCommit(string commitHash);
        IGitCommit ReplaceCommitHash(IGitCommitHash sourceGitCommitHash, IGitCommitHash replacementGitCommitHash);
    }

}

