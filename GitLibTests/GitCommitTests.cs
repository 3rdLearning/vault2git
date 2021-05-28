using System.Collections.Generic;
using System.Linq;
using GitLib;
using GitLib.Interfaces;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GitLibTests
{
    

    [TestClass()]
    public class GitCommitTests
    {

        private string _testHash1;
        private string _testHash2;
        //private byte[] _testHash1Bytes;

        [TestInitialize]
        public void TestInit()
        {
            _testHash1 = "1234567890123456789012345678901234567890";
            _testHash2 = "0123456789012345678901234567890123456789";
            //_testHash1Bytes = new byte[] { 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x34, 0x56, 0x78, 0x90, 0x12, 0x34, 0x56, 0x78, 0x90 };
        }

        [TestMethod()]
        public void CreateCommitWithoutParentShouldNotContainAParent()
        {
            var hash1 = new GitCommitHash(_testHash1);

            var testCommit1 = new GitCommit(hash1, new List<IGitCommitHash>());

            Assert.IsFalse(testCommit1.GetParentHashes().Any(),"Commit should not have a parent");
        }

        [TestMethod()]
        public void CreateCommitWithParentShouldContainParent()
        {
            var hash1 = new GitCommitHash(_testHash1);
            var parentHash1 = new List<IGitCommitHash>() { new GitCommitHash(_testHash2) };
            
            var testCommit1 = new GitCommit(hash1, parentHash1);
            
            Assert.IsTrue(testCommit1.GetParentHashes().Count == 1, "Commit should have a parent");

            var hash2 = new GitCommitHash(_testHash2);
            testCommit1.AddParent(hash2);

            Assert.IsTrue(testCommit1.GetParentHashes().Count == 1, "Commit should have 2 parents");
        }


        [TestMethod()]
        public void CommitHashShouldMatchHashUsedToCreateIt()
        {
            var hash1 = new GitCommitHash(_testHash1);
            var parentHashes = new List<IGitCommitHash>() { new GitCommitHash(_testHash2) };
            var testCommit1 = new GitCommit(hash1, parentHashes);

            Assert.AreEqual(_testHash1, testCommit1.GetHash().ToString());
            Assert.IsTrue(testCommit1.GetParentHashes().Any(x => x.ToString() == _testHash2));

        }

    }
}