using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.InputSystem;

namespace YARG.Types
{
    public struct FlatMapNode<Key, T>
        where T : new()
       where Key : IComparable<Key>, IEquatable<Key>
    {
        public Key key;
        public T obj;

        public static bool operator <(FlatMapNode<Key, T> node, Key key) { return node.key.CompareTo(key) < 0; }

        public static bool operator >(FlatMapNode<Key, T> node, Key key) { return node.key.CompareTo(key) < 0; }
    }

#nullable enable
    public abstract unsafe class FlatMap_Base<Key, T> : IDisposable, IEnumerable
       where T : new()
       where Key : IComparable<Key>, IEquatable<Key>
    {
        internal static readonly int DEFAULTCAPACITY = 16;

        protected int _count;
        protected int _capacity;
        protected int _version;
        protected bool _disposed;

        public int Count { get { return _count; } }

        public abstract int Capacity { get; set; }

        public FlatMap_Base() { }
        ~FlatMap_Base()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        protected abstract void Dispose(bool disposing);

        public void Clear()
        {
            for (int i = 0; i < _count; ++i)
                At_index(i) = default;

            if (_count > 0)
                _version++;
            _count = 0;
        }

        public bool IsEmpty() { return _count == 0; }

        protected void CheckAndGrow()
        {
            if (_count == int.MaxValue)
                throw new OverflowException("Element limit reached");

            if (_count == _capacity)
                Grow();
            ++_version;
        }

        protected void Grow()
        {
            int newcapacity = _capacity == 0 ? DEFAULTCAPACITY : 2 * _capacity;
            if ((uint) newcapacity > int.MaxValue) newcapacity = int.MaxValue;
            Capacity = newcapacity;
        }

        public void TrimExcess()
        {
            if (_count < _capacity)
                Capacity = _count;
        }

        public ref T Add(Key key, ref T obj)
        {
            CheckAndGrow();

            int index = _count++;
            ref var node = ref At_index(index);
            node.key = key;
            node.obj = obj;
            return ref node.obj;
        }

        public ref T Add(Key key, T obj)
        {
            CheckAndGrow();

            int index = _count++;
            ref var node = ref At_index(index);
            node.key = key;
            node.obj = obj;
            return ref node.obj;
        }

        public abstract ref T Add(Key key);

        public void Add_NoReturn(Key key, T obj)
        {
            CheckAndGrow();

            int index = _count++;
            ref var node = ref At_index(index);
            node.key = key;
            node.obj = obj;
        }

        public abstract void Add_NoReturn(Key key);

        public void Pop()
        {
            if (_count == 0)
                throw new Exception("Pop on emtpy map");

            At_index(_count - 1) = default;
            --_count;
            ++_version;
        }

        public ref T this[Key key] { get { return ref Find_Or_Add(0, key); } }

        public int Find_Or_Add_index(int searchIndex, Key key) { return Find_or_emplace_index(searchIndex, key); }

        public ref T Find_Or_Add(int searchIndex, Key key)
        {
            int index = Find_or_emplace_index(searchIndex, key);
            return ref At_index(index).obj;
        }

        protected abstract int Find_or_emplace_index(int searchIndex, Key key);

        public ref T At(Key key)
        {
            int index = Find(0, key);
            if (index < 0)
                throw new KeyNotFoundException();
            return ref At_index(index).obj;
        }

        public ref T Get_Or_Add_Back(Key key)
        {
            if (_count == 0)
                return ref Add(key);

            ref var node = ref At_index(_count - 1);
            if (node < key)
                return ref Add(key);
            return ref node.obj;
        }

        public ref T Traverse_Backwards_Until(Key key)
        {
            int index = _count;
            while (index > 0)
                if (At_index(--index).key.CompareTo(key) <= 0)
                    break;
            return ref At_index(index).obj;
        }

        public abstract ref FlatMapNode<Key, T> At_index(int index);

        public ref T Last() { return ref At_index(_count - 1).obj; }

        public bool ValidateLastKey(Key key)
        {
            return _count > 0 && At_index(_count - 1).key.Equals(key);
        }

        public bool Contains(Key key) { return Find(0, key) >= 0; }

        public int Find(int searchIndex, Key key)
        {
            int lo = searchIndex;
            int hi = Count - (searchIndex + 1);
            while (lo <= hi)
            {
                int curr = lo + ((hi - lo) >> 1);
                int order = At_index(curr).key.CompareTo(key);

                if (order == 0) return curr;
                if (order < 0)
                    lo = curr + 1;
                else
                    hi = curr - 1;
            }

            return ~lo;
        }

        public abstract FlatMapNode<Key, T>[] ToArray();

        public IEnumerator GetEnumerator() { return new Enumerator(this); }

        public struct Enumerator : IEnumerator<FlatMapNode<Key, T>>, IEnumerator
        {
            private readonly FlatMap_Base<Key, T> _map;
            private int _index;
            private readonly int _version;
            private FlatMapNode<Key, T> _current;

            internal Enumerator(FlatMap_Base<Key, T> map)
            {
                _map = map;
                _index = 0;
                _version = map._version;
                _current = default;
            }

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                var localMap = _map;

                if (_version == localMap._version && ((uint) _index < (uint) localMap._count))
                {
                    _current = localMap.At_index(_index);
                    _index++;
                    return true;
                }
                return MoveNextRare();
            }

            private bool MoveNextRare()
            {
                if (_version != _map._version)
                    throw new InvalidOperationException("Enum failed - Map was updated");

                _index = _map._count + 1;
                _current = default;
                return false;
            }

            public FlatMapNode<Key, T> Current => _current;

            object? IEnumerator.Current
            {
                get
                {
                    if (_index == 0 || _index == _map._count + 1)
                        throw new InvalidOperationException("Enum Operation not possible");
                    return Current;
                }
            }

            void IEnumerator.Reset()
            {
                if (_version != _map._version)
                    throw new InvalidOperationException("Enum failed - Map was updated");

                _index = 0;
                _current = default;
            }
        }
    }

    public unsafe class FlatMap<Key, T> : FlatMap_Base<Key, T>
       where T : new()
       where Key : IComparable<Key>, IEquatable<Key>
    {
        private FlatMapNode<Key, T>[] _buffer = Array.Empty<FlatMapNode<Key, T>>();

        public FlatMap() { }
        public FlatMap(int capacity) { Capacity = capacity; }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _buffer = Array.Empty<FlatMapNode<Key, T>>();
            }

            _disposed = true;
        }

        public override int Capacity
        {
            get => _capacity;
            set
            {
                if (value < _count)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (value != _capacity)
                {
                    if (value > 0)
                    {
                        if (value > int.MaxValue)
                            value = int.MaxValue;

                        Array.Resize(ref _buffer, value);
                        _capacity = _buffer.Length;
                    }
                    else
                    {
                        _buffer = Array.Empty<FlatMapNode<Key, T>>();
                        _capacity = 0;
                    }
                    ++_version;

                }
            }
        }

        public ref T Insert(int index, Key key, T? obj)
        {
            CheckAndGrow();

            if (index >= _count)
                index = _count;
            else
                Array.Copy(_buffer, index, _buffer, index + 1, _count - index);

            ++_count;
            ref var node = ref _buffer[index];
            node.key = key;
            node.obj = obj != null ? obj : new();
            return ref node.obj;
        }

        public override ref T Add(Key key)
        {
            return ref Add(key, new());
        }

        public override void Add_NoReturn(Key key)
        {
            Add_NoReturn(key, new());
        }

        public override ref FlatMapNode<Key, T> At_index(int index)
        {
            return ref _buffer[index];
        }

        public void RemoveAt(int index)
        {
            if ((uint) index >= (uint) _count)
                throw new IndexOutOfRangeException();

            _buffer[index] = default;
            --_count;
            if (index < _count)
            {
                Array.Copy(_buffer, index + 1, _buffer, index, _count - index);
            }
            ++_version;
        }

        protected override int Find_or_emplace_index(int searchIndex, Key key)
        {
            int index = Find(searchIndex, key);
            if (index < 0)
            {
                CheckAndGrow();

                index = ~index;
                if (index < _count)
                {
                    Array.Copy(_buffer, index, _buffer, index + 1, _count - index);
                }

                ++_count;
                ref var node = ref _buffer[index];
                node.key = key;
                node.obj = new();
            }
            return index;
        }

        public override FlatMapNode<Key, T>[] ToArray()
        {
            var arr = new FlatMapNode<Key, T>[_count];
            Array.Copy(_buffer, arr, _count);
            return arr;
        }
    }

    public unsafe class NativeFlatMap<Key, T> : FlatMap_Base<Key, T>
       where T : unmanaged
       where Key : unmanaged, IComparable<Key>, IEquatable<Key>
    {
        internal static T BASE = new();
        internal static readonly int SIZEOFNODE = sizeof(FlatMapNode<Key, T>);

        static NativeFlatMap()
        {
            if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
                throw new Exception($"{nameof(T)} cannot be used in an unmanaged context");
        }

        private FlatMapNode<Key, T>* _buffer = null;

        public NativeFlatMap() { }
        public NativeFlatMap(int capacity) { Capacity = capacity; }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (_buffer != null)
            {
                Marshal.FreeHGlobal((IntPtr) _buffer);
                _buffer = null;
            }

            _disposed = true;
        }

        public override int Capacity
        {
            get => _capacity;
            set
            {
                if (value < _count)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (value != _capacity)
                {
                    if (value > 0)
                    {
                        if (value > int.MaxValue)
                            value = int.MaxValue;

                        void* newItems = (void*) Marshal.AllocHGlobal(value * SIZEOFNODE);

                        if (_count > 0)
                            Copier.MemCpy(newItems, _buffer, (nuint)(_count * SIZEOFNODE));

                        Marshal.FreeHGlobal((IntPtr) _buffer);
                        _buffer = (FlatMapNode<Key, T>*) newItems;
                    }
                    else
                    {
                        if (_capacity > 0)
                            Marshal.FreeHGlobal((IntPtr) _buffer);
                        _buffer = null;
                    }
                    _capacity = value;
                    ++_version;
                }
            }
        }

        public ref T Insert(int index, Key key)
        {
            return ref Insert(index, key, BASE);
        }

        public ref T Insert(int index, Key key, T obj)
        {
            CheckAndGrow();

            if (index >= _count)
                index = _count;
            else
                Copier.MemMove(_buffer + index + 1, _buffer + index, (nuint) ((_count - index) * SIZEOFNODE));

            ++_count;
            ref var node = ref _buffer[index];
            node.key = key;
            node.obj = obj;
            return ref node.obj;
        }

        public override ref T Add(Key key)
        {
            return ref Add(key, ref BASE);
        }

        public override void Add_NoReturn(Key key)
        {
            Add_NoReturn(key, BASE);
        }

        public override ref FlatMapNode<Key, T> At_index(int index)
        {
            if (index >= Count)
                throw new ArgumentOutOfRangeException(nameof(index));

            return ref _buffer[index];
        }

        public void RemoveAt(int index)
        {
            if ((uint) index >= (uint) _count)
                throw new IndexOutOfRangeException();

            _buffer[index] = default;
            --_count;
            if (index < _count)
                Copier.MemMove(_buffer + index, _buffer + index + 1, (nuint) ((_count - index) * SIZEOFNODE));
            ++_version;
        }

        protected override int Find_or_emplace_index(int searchIndex, Key key)
        {
            int index = Find(searchIndex, key);
            if (index < 0)
            {
                index = ~index;
                CheckAndGrow();

                if (index < _count)
                    Copier.MemMove(_buffer + index + 1, _buffer + index, (nuint) ((_count - index) * SIZEOFNODE));

                ++_count;
                ref var node = ref _buffer[index];
                node.key = key;
                node.obj = BASE;
                ++_version;
            }
            return index;
        }

        public override FlatMapNode<Key, T>[] ToArray()
        {
            var arr = new FlatMapNode<Key, T>[_count];
            fixed (FlatMapNode<Key, T>* b = arr)
            {
                Copier.MemCpy(b, _buffer, (nuint) (_count * sizeof(FlatMapNode<Key, T>)));
            }
            return arr;
        }
    }


    public class TimedFlatMap<T> : FlatMap<ulong, T>
        where T : new()
    {
    }

    public class TimedNativeFlatMap<T> : NativeFlatMap<ulong, T>
        where T : unmanaged
    {
    }
}
