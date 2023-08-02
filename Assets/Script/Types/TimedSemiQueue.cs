using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UIElements;
using YARG.Song.Chart.Notes;

namespace YARG.Assets.Script.Types
{
    public class PlayableSemiQueue : SemiQueue<PlayableNote>
    {
        public int Find(float key)
        {
            int index = 0;
            float time;
            for (int i = _head; i < _buffer.Length && index < _count; i++, index++)
            {
                time = _buffer[i].position.seconds;
                if (time == key)
                    return index;
                else if (key < time)
                    return -1;
            }

            for (int i = 0; i < _tail && index < _count; i++, index++)
            {
                time = _buffer[i].position.seconds;
                if (time == key)
                    return index;
                else if (key < time)
                    return -1;
            }
            return -1;
        }

        public void Remove(float key)
        {
            int index = Find(key);
            if (index == -1)
                throw new InvalidOperationException("Key does not exist in queue");

            RemoveAt(index);
        }
    }
}
