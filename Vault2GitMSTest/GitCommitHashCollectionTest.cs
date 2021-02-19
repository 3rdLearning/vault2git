using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Vault2Git.Lib.Vault2GitState;

namespace Vault2GitMSTest
{
    [TestClass]
    public class GitCommitHashCollectionTest
    {
        string hash1 = "1234567890123456789012345678901234567890";
        string hash2 = "0123456789012345678901234567890123456789";
        //string hash3 = "9012345678901234567890123456789012345678";

        [TestMethod]
        public void CommitHashCollectionShouldReturnSameHash()
        {
            GitCommitHashCollection commitHashes = new GitCommitHashCollection();
            GitCommitHash CommitHash = new GitCommitHash(hash1);
            commitHashes.AddCommitHash(CommitHash);

            Assert.AreEqual(CommitHash, commitHashes[hash1]);
            Assert.AreNotEqual(CommitHash, commitHashes[hash2]);
            Assert.AreEqual(1, commitHashes.Count());
        }

        [TestMethod]
        public void CommitHashCollectionShouldNotAddDuplicateHashes()
        {
            GitCommitHashCollection commitHashes = new GitCommitHashCollection();
            GitCommitHash CommitHash = new GitCommitHash(hash1);
            GitCommitHash CommitHash2 = new GitCommitHash(hash1);
            GitCommitHash CommitHash3 = new GitCommitHash(hash2);

            commitHashes.AddCommitHash(CommitHash);
            commitHashes.AddCommitHash(CommitHash2);
            commitHashes.AddCommitHash(CommitHash3);


            Assert.AreEqual(2, commitHashes.Count());
        }

        [TestMethod]
        public void CommitHashCollectionShouldReplaceHashes()
        {
            GitCommitHashCollection commitHashes = new GitCommitHashCollection();
            GitCommitHash CommitHash = new GitCommitHash(hash1);
            GitCommitHash CommitHash2 = new GitCommitHash(hash1);
            GitCommitHash CommitHash3 = new GitCommitHash(hash2);

            commitHashes.AddCommitHash(CommitHash);
            commitHashes.AddCommitHash(CommitHash2);
            commitHashes.AddCommitHash(CommitHash3);


            Assert.AreEqual(2, commitHashes.Count());
        }
    }
}
