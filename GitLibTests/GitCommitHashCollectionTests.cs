using GitLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitLibTests
{
    [TestClass()]
    public class GitCommitHashCollectionTests
    {
        string _hash1;
        string _hash2;

        [TestInitialize]
        public void TestInit()
        {
            _hash1 = "1234567890123456789012345678901234567890";
            _hash2 = "0123456789012345678901234567890123456789";
        }
        
        [TestMethod]
        public void CommitHashCollectionShouldReturnSameHash()
        {
            GitCommitHashCollection commitHashes = new GitCommitHashCollection();
            GitCommitHash commitHash = new GitCommitHash(_hash1);
            commitHashes.AddCommitHash(commitHash);

            Assert.AreEqual(commitHash, commitHashes[_hash1]);
            Assert.AreNotEqual(commitHash, commitHashes[_hash2]);
            Assert.AreEqual(1, commitHashes.Count());
        }

        [TestMethod]
        public void CommitHashCollectionShouldNotAddDuplicateHashes()
        {
            GitCommitHashCollection commitHashes = new GitCommitHashCollection();
            GitCommitHash commitHash = new GitCommitHash(_hash1);
            GitCommitHash commitHash2 = new GitCommitHash(_hash1);

            GitCommitHash commitHash3 = new GitCommitHash(_hash2);

            commitHashes.AddCommitHash(commitHash);
            commitHashes.AddCommitHash(commitHash2);
            commitHashes.AddCommitHash(commitHash3);


            Assert.AreEqual(2, commitHashes.Count());
        }

        [TestMethod]
        public void CommitHashCollectionShouldReplaceHashes()
        {
            GitCommitHashCollection commitHashes = new GitCommitHashCollection();
            GitCommitHash commitHash = new GitCommitHash(_hash1);
            GitCommitHash commitHash2 = new GitCommitHash(_hash1);
            GitCommitHash commitHash3 = new GitCommitHash(_hash2);

            commitHashes.AddCommitHash(commitHash);
            commitHashes.AddCommitHash(commitHash2);
            commitHashes.AddCommitHash(commitHash3);


            Assert.AreEqual(2, commitHashes.Count());
        }
    }
}