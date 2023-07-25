﻿using YARG.Modifiers;
using YARG.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace YARG.Serialization
{
    public unsafe class IniFileReader : IDisposable
    {
        private readonly TxtFileReader reader;
        private bool disposeReader = false;
        private string sectionName = string.Empty;
        public string Section { get { return sectionName; } }

        public IniFileReader(TxtFileReader reader) { this.reader = reader; }

        public IniFileReader(FrameworkFile file) : this(new TxtFileReader(file)) { disposeReader = true; }

        public IniFileReader(byte[] data) : this(new TxtFileReader(data)) { disposeReader = true; }

        public IniFileReader(string path) : this(new TxtFileReader(path)) { disposeReader = true; }

        public IniFileReader(PointerHandler handler, bool dispose = false) : this(new TxtFileReader(handler, dispose)) { disposeReader = true; }

        public void Dispose()
        {
            if (disposeReader)
            {
                reader.Dispose();
                disposeReader = false;
            }
            GC.SuppressFinalize(this);
        }

        public bool IsStartOfSection()
        { 
            if (reader.IsEndOfFile())
                return false;

            if (reader.PeekByte() != '[')
            {
                SkipSection();
                if (reader.IsEndOfFile())
                    return false;
            }

            byte* curr = reader.CurrentPtr;
            long length = 0;

            do
                ++length;
            while (curr[length] > 32);

            byte[] bytes = new byte[length];
            for (int i = 0; i < length; ++i)
            {
                byte character = bytes[i] = curr[i];
                if (65 <= character && character <= 90)
                    bytes[i] += 32;
            }

            sectionName = Encoding.UTF8.GetString(bytes);
            return true;
        }

        public void SkipSection()
        {
            reader.GotoNextLine();
            byte* ptr = reader.file.ptr;
            int position = reader.Position;
            while (GetDistanceToTrackCharacter(position, out int next))
            {
                int point = position + next - 1;
                while (point > position && ptr[point] <= 32 && ptr[point] != '\n')
                    --point;

                if (ptr[point] == '\n')
                {
                    reader.Position = position + next;
                    reader.SetNextPointer();
                    return;
                }

                position += next + 1;
            }

            reader.Position = reader.file.Length;
            reader.SetNextPointer();
        }

        private bool GetDistanceToTrackCharacter(int position, out int i)
        {
            int distanceToEnd = reader.file.Length - position;
            byte* curr = reader.file.ptr + position;
            i = 0;
            while (i < distanceToEnd)
            {
                if (curr[i] == '[')
                    return true;
                ++i;
            }
            return false;
        }

        public bool IsStillCurrentSection()
        {
            return !reader.IsEndOfFile() && reader.PeekByte() != '[';
        }

        public Dictionary<string, List<Modifier>> ExtractModifiers(ref Dictionary<string, ModifierNode> validNodes)
        {
            Dictionary<string, List<Modifier>> modifiers = new();
            reader.GotoNextLine();
            while (IsStillCurrentSection())
            {
                var name = reader.ExtractModifierName().ToLower();
                if (validNodes.TryGetValue(name, out var node))
                {
                    var mod = node.CreateModifier(reader);
                    if (modifiers.TryGetValue(node.outputName, out var list))
                        list.Add(mod);
                    else
                        modifiers.Add(node.outputName, new() { mod });
                }
                reader.GotoNextLine();
            }
            return modifiers;
        }
    }
}
