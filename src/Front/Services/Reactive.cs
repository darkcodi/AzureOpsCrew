using System.Collections;
using Serilog;

namespace Front.Services;

public class Reactive<T>
{
    public Reactive()
    {
        _value = default!;
    }

    public Reactive(T initialValue)
    {
        _value = initialValue;
    }

    private T _value;
    private event Action? OnChange;

    public T Value
    {
        get => _value;
        set
        {
            if (EqualityComparer<T>.Default.Equals(_value, value)) return;
            _value = value;
            OnStateChanged();
        }
    }

    public IDisposable Subscribe(Action handler)
    {
        OnChange += handler;
        return new DisposableAction(() => OnChange -= handler);
    }

    public void ForceNotify()
    {
        OnStateChanged();
    }

    protected void OnStateChanged()
    {
        try
        {
            OnChange?.Invoke();
        }
        catch (Exception e)
        {
            Log.Error("Error in Reactive OnChange event: {Message}", e.Message);
        }
    }
}

public class ReactiveList<T> : IList<T>
{
    public ReactiveList()
    {
        _inner = new(new());
    }

    public ReactiveList(IEnumerable<T> initialValue)
    {
        _inner = new(new List<T>(initialValue));
    }

    private readonly Reactive<List<T>> _inner;

    public void Add(T item)
    {
        _inner.Value.Add(item);
        _inner.ForceNotify();
    }

    public void Clear()
    {
        _inner.Value.Clear();
        _inner.ForceNotify();
    }

    public bool Contains(T item) => _inner.Value.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _inner.Value.CopyTo(array, arrayIndex);
    public IEnumerator<T> GetEnumerator() => _inner.Value.GetEnumerator();
    public int IndexOf(T item) => _inner.Value.IndexOf(item);
    public void Insert(int index, T item)
    {
        _inner.Value.Insert(index, item);
        _inner.ForceNotify();
    }

    public bool Remove(T item)
    {
        var removed = _inner.Value.Remove(item);
        if (removed) _inner.ForceNotify();
        return removed;
    }

    public int Count => _inner.Value.Count;
    public bool IsReadOnly => false;

    public void RemoveAt(int index)
    {
        _inner.Value.RemoveAt(index);
        _inner.ForceNotify();
    }

    public T this[int index]
    {
        get => _inner.Value[index];
        set
        {
            // compare old and new value to avoid unnecessary notifications
            if (EqualityComparer<T>.Default.Equals(_inner.Value[index], value)) return;
            _inner.Value[index] = value;
            _inner.ForceNotify();
        }
    }

    public IDisposable Subscribe(Action handler) => _inner.Subscribe(handler);

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
