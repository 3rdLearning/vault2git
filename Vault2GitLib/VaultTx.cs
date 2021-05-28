using System.Collections.Generic;
using VaultLib;

namespace Vault2Git.Lib
{
    public partial class Vault2GitState
    {
        public class VaultTx
        {
            private readonly long _txId;
            private readonly string _branch;
            private string _path;
            private long _version;
            private string _comment;
            private string _login;
            private string _mergedFrom;
            private VaultDateTime _timeStamp;

            public long TxId => _txId;
            public string Branch => _branch;
            public string Path { get => _path; set => _path = value; }
            public long Version { get => _version; set => _version = value; }
            public string Comment { get => _comment; set => _comment = value; }
            public string Login { get => _login; set => _login = value; }
            public string MergedFrom { get => _mergedFrom; set => _mergedFrom = value; }
            public VaultDateTime TimeStamp { get => _timeStamp; set => _timeStamp = value; }

            public VaultTx(long txId, string branch)
            {
                _branch = branch;
                _txId = txId;
            }

            public VaultTx()
            {
            }

            public VaultTx(long txId, string branch, string path, long version, string comment, string login, string mergedFrom, VaultDateTime timeStamp)
            {
                _txId = txId;
                _branch = branch;
                _path = path;
                _version = version;
                _comment = comment;
                _login = login;
                _mergedFrom = mergedFrom;
                _timeStamp = timeStamp;
            }

            public VaultTx(long txId)
            {
                _txId = txId;
            }

            public static VaultTx Create(long txId, string branch = DEFAULT_BRANCH)
            {
                return new VaultTx(txId, branch);
            }

            public override bool Equals(object obj)
            {
                return obj is VaultTx tx &&
                       _branch == tx._branch &&
                       _txId == tx._txId;
            }

            public override int GetHashCode()
            {
                int hashCode = -1854605187;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_branch);
                hashCode = hashCode * -1521134295 + _txId.GetHashCode();
                return hashCode;
            }
        }
    }
}