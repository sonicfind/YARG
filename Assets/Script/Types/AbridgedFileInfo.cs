using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Types
{
    public class AbridgedFileInfo
    {
        public readonly string FullName;
        public readonly DateTime LastWriteTime;

        public AbridgedFileInfo(string file) : this(file, File.GetLastWriteTime(file)) { }

        public AbridgedFileInfo(FileInfo info) : this (info.FullName, info.LastWriteTime) { }

        public AbridgedFileInfo(string fullname, DateTime lastWrite)
        {
            FullName = fullname;
            LastWriteTime = lastWrite;
        }

        public bool IsStillValid()
        {
            FileInfo file = new(FullName);
            return file.Exists && file.LastWriteTime == LastWriteTime;
        }

        public static implicit operator AbridgedFileInfo(FileInfo info) => new(info);
    }
}
