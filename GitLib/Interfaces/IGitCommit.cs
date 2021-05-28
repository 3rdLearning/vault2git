using System.Collections.Generic;

namespace GitLib.Interfaces
{
    public interface IGitCommit
    {
        string Comment { get; set; }

        void AddParent(IGitCommitHash gitCommitHash);
        IGitCommitHash GetHash();
        List<IGitCommitHash> GetParentHashes();

    }
}
