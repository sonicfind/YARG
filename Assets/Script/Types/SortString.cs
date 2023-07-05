using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace YARG.Types
{
    public struct SortString : IComparable<SortString>, IEquatable<SortString>
    {
        private string _str;
        private string _sortStr;
        private int _hashCode;

        public string Str
        {
            get { return _str; }
            set {
                _str = value;
                _sortStr = value.ToLower();
                _hashCode = _sortStr.GetHashCode();
            }
        }

        public int Length { get { return _str.Length; } }
        
        public readonly string SortStr { get { return _sortStr; } }

        public SortString(string str)
        {
            _sortStr = _str = string.Empty;
            _hashCode = 0;
            Str = str;
        }

        public int CompareTo(SortString other)
        {
            return _sortStr.CompareTo(other._sortStr);
        }

        public override int GetHashCode()
        {
            return _hashCode;  
        }

        public bool Equals(SortString other)
        {
            return _hashCode == other._hashCode;
        }

        public static implicit operator SortString(string str) => new(str);
    }
}
