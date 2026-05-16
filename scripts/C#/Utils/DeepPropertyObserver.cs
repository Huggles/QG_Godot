using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

/// <summary>
/// Recursively subscribes to PropertyChanged events across an entire object graph.
/// When a property on any observed object changes, the provided callback fires.
/// When a property's value is replaced with a new INotifyPropertyChanged object,
/// the observer automatically subscribes to the new child.
/// </summary>
public sealed class DeepPropertyObserver : IDisposable
{
    private readonly Action<object, PropertyChangedEventArgs> _onChange;
    private readonly Dictionary<INotifyPropertyChanged, PropertyChangedEventHandler> _subscriptions
        = new(ReferenceEqualityComparer.Instance);

    public DeepPropertyObserver(Action<object, PropertyChangedEventArgs> onChange)
    {
        _onChange = onChange;
    }

    /// <summary>
    /// Start observing an object and all its INotifyPropertyChanged descendants.
    /// Replaces any previously observed root.
    /// </summary>
    public void Observe(INotifyPropertyChanged root)
    {
        DisposeSubscriptions();
        if (root != null)
            Subscribe(root);
    }

    private void Subscribe(INotifyPropertyChanged obj)
    {
        if (obj == null || _subscriptions.ContainsKey(obj)) return;

        PropertyChangedEventHandler handler = (sender, e) => HandleChange(sender, e);
        _subscriptions[obj] = handler;
        obj.PropertyChanged += handler;

        // Recursively subscribe to any existing INotifyPropertyChanged children
        foreach (var prop in GetObservableProperties(obj.GetType()))
        {
            var child = prop.GetValue(obj) as INotifyPropertyChanged;
            Subscribe(child);
        }
    }

    private void HandleChange(object sender, PropertyChangedEventArgs e)
    {
        _onChange(sender, e);

        // When a property is assigned a new INotifyPropertyChanged value, subscribe to it.
        // Note: we cannot unsubscribe the old value here since it's already replaced.
        // For long-lived graphs, call Observe() again when replacing major subtrees.
        if (sender is INotifyPropertyChanged source && e.PropertyName != null)
        {
            var prop = source.GetType().GetProperty(e.PropertyName,
                BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && typeof(INotifyPropertyChanged).IsAssignableFrom(prop.PropertyType))
            {
                var newChild = prop.GetValue(source) as INotifyPropertyChanged;
                Subscribe(newChild);
            }
        }
    }

    private static IEnumerable<PropertyInfo> GetObservableProperties(Type type)
    {
        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (prop.CanRead
                && prop.GetIndexParameters().Length == 0
                && typeof(INotifyPropertyChanged).IsAssignableFrom(prop.PropertyType))
            {
                yield return prop;
            }
        }
    }

    private void DisposeSubscriptions()
    {
        foreach (var (obj, handler) in _subscriptions)
            obj.PropertyChanged -= handler;
        _subscriptions.Clear();
    }

    public void Dispose() => DisposeSubscriptions();
}
