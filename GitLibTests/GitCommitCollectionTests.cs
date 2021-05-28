using GitLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitLibTests
{
    [TestClass()]
    public class GitCommitCollectionTests
    {
        string _hash1;
        string _hash2;
        private byte[] _testHash1Bytes;

        [TestInitialize]
        public void TestInit()
        {
            _hash1 = "1234567890123456789012345678901234567890";
            _hash2 = "0123456789012345678901234567890123456789";
            _testHash1Bytes = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x34, 0x56, 0x78, 0x90 };
        }

        [TestMethod()]
        public void GitCommitCollectionShouldNotBeNull()
        {
            var gitCommitCollection = new GitCommitCollection();
            Assert.IsNotNull(gitCommitCollection);
        }

        [TestMethod()]
        public void GitCommitAddedToCollectionShouldMatchSourceCommit()
        {
            var gitCommitCollection = new GitCommitCollection();

            var commit1 = gitCommitCollection.AddCommit(_hash1);
            var commit2 = gitCommitCollection.AddCommit(new GitCommitHash(_hash2));
            
            Assert.IsTrue(gitCommitCollection[_hash1] == commit1);
            Assert.IsTrue(gitCommitCollection[_hash2] == commit2);
        }

        [TestMethod()]
        public void GitCommitHashReplacedInGitCommitCollectionShouldMatchReplacementCommitHash()
        {
            var gitCommitCollection = new GitCommitCollection();
            var commitHash11 = new GitCommitHash(_hash1);
            gitCommitCollection.AddCommit(_hash1);
            gitCommitCollection.AddCommit(_testHash1Bytes);

            
            gitCommitCollection.ReplaceCommitHash(commitHash11, new GitCommitHash(_hash2));

            Assert.IsTrue(gitCommitCollection[_hash1].GetHash().ToString(false) == _hash1);
            Assert.IsTrue(gitCommitCollection[_hash1].GetHash().ToString(true) == _hash2);
            Assert.IsTrue(gitCommitCollection[_hash1].GetHash().ToString() == _hash2); 
        }
    }
}