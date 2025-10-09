using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace VladiCore.Api.Infrastructure
{
    public interface IRateLimiter
    {
        bool IsAllowed(string key, int limit, TimeSpan window);
    }

    public class SlidingWindowRateLimiter : IRateLimiter
    {
        private readonly ConcurrentDictionary<string, LinkedList<DateTime>> _hits = new ConcurrentDictionary<string, LinkedList<DateTime>>();

        public bool IsAllowed(string key, int limit, TimeSpan window)
        {
            var now = DateTime.UtcNow;
            var list = _hits.GetOrAdd(key, _ => new LinkedList<DateTime>());

            lock (list)
            {
                while (list.First != null && now - list.First.Value > window)
                {
                    list.RemoveFirst();
                }

                if (list.Count >= limit)
                {
                    return false;
                }

                list.AddLast(now);
                return true;
            }
        }
    }
}
