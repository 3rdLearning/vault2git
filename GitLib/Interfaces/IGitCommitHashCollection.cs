namespace GitLib.Interfaces
{
    interface IGitCommitHashCollection
    {
        IGitCommitHash this[string commitHash] { get; }

        IGitCommitHash AddCommitHash(byte[] commitHashBytes);
        IGitCommitHash AddCommitHash(IGitCommitHash gitCommitHash);
        IGitCommitHash AddCommitHash(string commitHash);
        
        IGitCommitHash ReplaceCommitHash(IGitCommitHash sourceGitCommitHash, string replacementCommitHash);
        int Count();
    }
}