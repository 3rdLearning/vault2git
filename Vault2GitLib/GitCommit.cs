using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        private class GitCommit
        {
            private GitList<GitCommit> _gitCommitHashes;
        }
    }
}