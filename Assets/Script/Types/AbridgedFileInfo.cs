using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Assets.Script.Types
{
    public class AbridgedFileInfo
    {
        public readonly string FullName;
        public readonly DateTime LastWriteTime;

        public AbridgedFileInfo(string file) : this(new FileInfo(file)) { }

        public AbridgedFileInfo(FileInfo info) : this (info.FullName, info.LastWriteTime) { }

        public AbridgedFileInfo(string fullname, DateTime lastWrite)
        {
            FullName = fullname;
            LastWriteTime = lastWrite;
        }

        public static implicit operator AbridgedFileInfo(FileInfo info) => new(info);
    }
}
