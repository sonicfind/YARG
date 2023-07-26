using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using YARG.Types;
using static UnityEditor.Experimental.GraphView.Port;

namespace YARG.Assets.Script.Types
{
    public class SemiQueue<T>
    {
        internal const int DEFAULTCAPACITY = 16;

        protected int _count = 0;
        protected int _head = 0;
        protected int _tail = 0;
        protected T[] _buffer = Array.Empty<T>();
        private int _version = 0;

        public SemiQueue() { }

        public SemiQueue(int capacity) : this() { Capacity = capacity; }

        public int Count { get { return _count; } }
        public int Capacity
        {
            get => _buffer.Length;
            set
            {
                if (value < _count)
                    throw new ArgumentOutOfRangeException(nameof(value));

                if (value != _buffer.Length)
                {
                    if (value > 0)
                    {
                        if (value > int.MaxValue)
                            value = int.MaxValue;

                        if (Count == 0)
                            Array.Resize(ref _buffer, value);
                        else
                        {
                            var newBuf = new T[value];
                            if (_head < _tail)
                                Array.Copy(_buffer, _head, newBuf, 0, _tail - _head);
                            else
                            {
                                int pivot = _buffer.Length - _head;
                                Array.Copy(_buffer, _head, newBuf, 0, pivot);
                                Array.Copy(_buffer, 0, newBuf, pivot, _count - pivot);
                            }
                            _buffer = newBuf;

                            _head = 0;
                            _tail = _count;
                        }
                    }
                    else
                    {
                        _buffer = Array.Empty<T>();
                    }
                    ++_version;
                }
            }
        }

        public void Enqueue(T item)
        {
            CheckAndGrow();
            _count++;
            if (_tail < _buffer.Length)
                _buffer[_tail++] = item;
            else
            {
                _buffer[0] = item;
                _tail = 1;
            }
        }

        public void Dequeue()
        {
            if (_count == 0)
                throw new InvalidOperationException("Queue is empty");

            if (!TryReset())
            {
                _buffer[_head] = default;
                --_count;
                if (_head + 1 < _buffer.Length)
                    ++_head;
                else
                    _head = 0;
                ++_version;
            }
        }

        public ref T Peek()
        {
            if (_count == 0)
                throw new InvalidOperationException("Queue is empty");
            return ref _buffer[_head];
        }

        public ref T At(int position)
        {
            if (_count == 0)
                throw new InvalidOperationException("Queue is empty");

            if (position < 0 || position >= _count)
                throw new ArgumentOutOfRangeException(nameof(position));

            int offset = _head + position;
            if (offset >= _buffer.Length)
                offset -= _buffer.Length;
            return ref _buffer[offset];
        }

        public void RemoveAt(int position)
        {
            if (position < 0 || position >= _count)
                throw new ArgumentOutOfRangeException(nameof(position));

            if (TryReset())
                return;

            int offset = _head + position;
            if (offset < _buffer.Length)
            {
                Array.Copy(_buffer, _head, _buffer, _head + 1, position);
                _buffer[_head++] = default;
            }
            else
            {
                offset -= _buffer.Length;
                Array.Copy(_buffer, offset + 1, _buffer, offset, _count - (position + 1));
                _buffer[_tail--] = default;
            }
            --_count;
            ++_version;
        }

        private bool TryReset()
        {
            if (_count != 1)
                return false;

            _buffer[_head] = default;
            _count = 0;
            _head = 0;
            _tail = 0;
            _version = 0;
            return true;
        }

        private void CheckAndGrow()
        {
            if (_count == int.MaxValue)
                throw new OverflowException("Element limit reached");

            if (_count == _buffer.Length)
                Grow();
            ++_version;
        }

        private void Grow()
        {
            int capacity = _buffer.Length;
            int newcapacity = capacity == 0 ? DEFAULTCAPACITY : 2 * capacity;
            if ((uint) newcapacity > int.MaxValue) newcapacity = int.MaxValue;
            Capacity = newcapacity;
        }
    }
}
