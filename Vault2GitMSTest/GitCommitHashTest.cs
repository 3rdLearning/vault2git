using Microsoft.VisualStudio.TestTools.UnitTesting;
using static Vault2Git.Lib.Vault2GitState;

namespace Vault2GitMSTest
{
    [TestClass]
    public class GitCommitHashTest
    {
        [TestMethod]
        public void NewHashFromStringShouldOuputSameString()
        {
            GitCommitHash hash = new GitCommitHash("1234567890123456789012345678901234567890");
            Assert.AreEqual("1234567890123456789012345678901234567890", hash.ToString());
        }

        [TestMethod]
        public void CompareHashesShouldBeEqual()
        {
            GitCommitHash hash = new GitCommitHash("1234567890123456789012345678901234567890");
            GitCommitHash hash2 = new GitCommitHash("1234567890123456789012345678901234567890");
            Assert.AreEqual(hash, hash2);
            Assert.IsTrue(hash == hash2);
        }

        [TestMethod]
        public void CompareDifferentHashesShouldNotBeEqual()
        {
            GitCommitHash hash = new GitCommitHash("1234567890123456789012345678901234567890");
            GitCommitHash hash2 = new GitCommitHash("0123456789012345678901234567890123456789");
            Assert.AreNotEqual(hash, hash2);
            Assert.IsTrue(hash != hash2);
        }

        [TestMethod]
        public void CompareDifferentHashesWithSameReplacementShouldBeEqual()
        {
            GitCommitHash hash = new GitCommitHash("1234567890123456789012345678901234567890");
            GitCommitHash hash2 = new GitCommitHash("0123456789012345678901234567890123456789");
            hash.Replace(hash2);
            Assert.AreEqual(hash, hash2);
        }

        [TestMethod]
        public void NewHashFromStringShouldNotOuputSameStringWhenNotFollowingReplacments()
        {
            GitCommitHash hash = new GitCommitHash("1234567890123456789012345678901234567890");
            GitCommitHash hash2 = new GitCommitHash("0123456789012345678901234567890123456789");
            hash.Replace(hash2);
            Assert.AreNotEqual(hash.ToString(false), "0123456789012345678901234567890123456789");
        }
    }
}
