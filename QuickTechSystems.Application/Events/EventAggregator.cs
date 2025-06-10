using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuickTechSystems.Application.Events
{
    public interface IEventAggregator
    {
        void Publish<TEvent>(TEvent eventToPublish);
        void Subscribe<TEvent>(Action<TEvent> action);
        void Unsubscribe<TEvent>(Action<TEvent> action);
    }

    public class EventAggregator : IEventAggregator
    {
        private readonly Dictionary<Type, List<object>> _subscribers = new();
        private readonly object _lock = new();

        public void Publish<TEvent>(TEvent eventToPublish)
        {
            Debug.WriteLine($"Publishing event of type: {typeof(TEvent).Name}");
            lock (_lock)
            {
                if (!_subscribers.ContainsKey(typeof(TEvent)))
                {
                    Debug.WriteLine($"No subscribers found for {typeof(TEvent).Name}");
                    return;
                }

                var actions = _subscribers[typeof(TEvent)]
                    .Cast<Action<TEvent>>()
                    .Where(action => action != null)
                    .ToList();

                Debug.WriteLine($"Found {actions.Count} subscribers for {typeof(TEvent).Name}");

                foreach (var action in actions)
                {
                    try
                    {
                        Debug.WriteLine("Invoking subscriber");
                        action.Invoke(eventToPublish);
                        Debug.WriteLine("Subscriber invoked successfully");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Error in subscriber: {ex}");
                    }
                }
            }
        }

        public void Subscribe<TEvent>(Action<TEvent> action)
        {
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                Debug.WriteLine($"Attempting to subscribe to {eventType.Name}");

                if (!_subscribers.ContainsKey(eventType))
                {
                    Debug.WriteLine($"Creating new subscriber list for {eventType.Name}");
                    _subscribers[eventType] = new List<object>();
                }

                _subscribers[eventType].Add(action);
                Debug.WriteLine($"Successfully subscribed. Total subscribers for {eventType.Name}: {_subscribers[eventType].Count}");
            }
        }

        public void Unsubscribe<TEvent>(Action<TEvent> action)
        {
            lock (_lock)
            {
                var eventType = typeof(TEvent);
                if (!_subscribers.ContainsKey(eventType))
                    return;

                _subscribers[eventType].Remove(action);
            }
        }
    }
}
