using System;
using UnityEngine;

namespace YARG.PlayMode
{
    public class VenueManager : MonoBehaviour
    {
        public static event Action<string> OnEventReceive;

        private int _eventIndex;

        private void Update()
        {
            if (!Play.Instance.SongStarted)
            {
                return;
            }

            var globals = Play.Instance.chartNew.m_events.globals;

            // Update venue events
            while (globals.Count > _eventIndex)
            {
                ref var node = ref globals.At_index(_eventIndex++);
                if (node.key > Play.Instance.SongTime)
                    break;

                foreach (var ev in node.obj)
                {
                    if (ev.StartsWith("venue_"))
                    {
                        OnEventReceive?.Invoke(ev);
                    }
                }
            }
        }
    }
}