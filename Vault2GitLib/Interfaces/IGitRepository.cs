using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace Vault2Git.Lib.Interfaces
{

    interface IGitRepository
    {
        // adds new commit and returns it
        IGitCommit AddCommit(IGitCommit gitCommit);
        
        //replaces any matching existing commit with a new commit
        IGitCommit ReplaceCommit(IGitCommit originalGitCommit, IGitCommit newGitCommit);
    }

}
