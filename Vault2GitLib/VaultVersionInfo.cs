using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VaultLib;

namespace Vault2Git.Lib
{
    public class VaultVersionInfo
    {
		private string _branch;
		private string _path;
		private long _version;
		private long _txId;
		private string _comment;
		private string _login;
        private string _mergedFrom;
		private VaultLib.VaultDateTime _timeStamp;

        public VaultVersionInfo()
        {
        }

        public string Branch { get => _branch; set => _branch = value; }
        public string Path { get => _path; set => _path = value; }
        public long Version { get => _version; set => _version = value; }
        public long TxId { get => _txId; set => _txId = value; }
        public string Comment { get => _comment; set => _comment = value; }
        public string Login { get => _login; set => _login = value; }
        public VaultDateTime TimeStamp { get => _timeStamp; set => _timeStamp = value; }
        public string MergedFrom { get => _mergedFrom; set => _mergedFrom = value; }

        public override bool Equals(object obj)
        {
            return obj is VaultVersionInfo info &&
                   _branch == info._branch &&
                   _path == info._path &&
                   _version == info._version &&
                   _txId == info._txId &&
                   _comment == info._comment &&
                   _login == info._login &&
                   _mergedFrom == info._mergedFrom &&
                   EqualityComparer<VaultDateTime>.Default.Equals(_timeStamp, info._timeStamp);
        }

        public override int GetHashCode()
        {
            int hashCode = -1303771472;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_branch);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_path);
            hashCode = hashCode * -1521134295 + _version.GetHashCode();
            hashCode = hashCode * -1521134295 + _txId.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_comment);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_login);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(_mergedFrom);
            hashCode = hashCode * -1521134295 + EqualityComparer<VaultDateTime>.Default.GetHashCode(_timeStamp);
            return hashCode;
        }

        public static bool operator ==(VaultVersionInfo left, VaultVersionInfo right)
        {
            return EqualityComparer<VaultVersionInfo>.Default.Equals(left, right);
        }

        public static bool operator !=(VaultVersionInfo left, VaultVersionInfo right)
        {
            return !(left == right);
        }
    }
}
