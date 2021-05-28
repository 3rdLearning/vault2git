using GitLib;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitLibTests
{

    [TestClass()]
    public class GitCommitHashTests
    {
        private string _testHash1;
        private string _testHash2;
        private byte[] _testHash1Bytes;

        [TestInitialize]
        public void TestInit()
        {
            _testHash1 = "1234567890123456789012345678901234567890";
            _testHash2 = "0123456789012345678901234567890123456789";
            _testHash1Bytes = new byte[]{ 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x34, 0x56, 0x78, 0x90 };
        }



        [TestMethod]
        public void NewHashFromStringShouldOuputSameString()
        {
            GitCommitHash hash = new GitCommitHash(_testHash1);
            Assert.AreEqual(_testHash1, hash.ToString());
        }

        [TestMethod]
        public void NewHashFromBytesShouldOuputSameString()
        {
            GitCommitHash hash = new GitCommitHash(_testHash1Bytes);
            Assert.AreEqual(_testHash1, hash.ToString());
        }

        [TestMethod]
        public void CompareHashesShouldBeEqual()
        {
            GitCommitHash hash = new GitCommitHash(_testHash1);
            GitCommitHash hash2 = new GitCommitHash(_testHash1);
            Assert.AreEqual(hash, hash2);
            Assert.IsTrue(hash == hash2);
        }

        [TestMethod]
        public void CompareDifferentHashesShouldNotBeEqual()
        {
            GitCommitHash hash = new GitCommitHash(_testHash1);
            GitCommitHash hash2 = new GitCommitHash(_testHash2);
            Assert.AreNotEqual(hash, hash2);
            Assert.IsTrue(hash != hash2);
        }

        [TestMethod]
        public void CompareDifferentHashesWithSameReplacementShouldBeEqual()
        {
            GitCommitHash hash = new GitCommitHash(_testHash1);
            GitCommitHash hash2 = new GitCommitHash(_testHash2);
            hash.Replace(hash2);
            Assert.AreEqual(hash, hash2);
        }

        [TestMethod]
        public void NewHashFromStringShouldNotOuputSameStringWhenNotFollowingReplacments()
        {
            GitCommitHash hash = new GitCommitHash(_testHash1);
            GitCommitHash hash2 = new GitCommitHash(_testHash2);
            hash.Replace(hash2);
            Assert.AreNotEqual(hash.ToString(false), "0123456789012345678901234567890123456789");
        }

    }
}