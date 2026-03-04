using Serilog;

namespace Front.Services;

public class Reactive<T>
{
    private T _value = default!;
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

    private void OnStateChanged()
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

    private class DisposableAction(Action action) : IDisposable
    {
        public void Dispose()
        {
            action();
        }
    }
}
