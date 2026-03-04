namespace Front.Services;

public class DisposableAction(Action action) : IDisposable
{
    public void Dispose()
    {
        action();
    }
}

public class CompositeDisposable : IDisposable
{
    private readonly List<IDisposable> _disposables = new();

    public CompositeDisposable(params IDisposable[] disposables)
    {
        _disposables.AddRange(disposables);
    }

    public void Add(IDisposable disposable)
    {
        _disposables.Add(disposable);
    }

    public void Dispose()
    {
        foreach (var disposable in _disposables)
        {
            disposable.Dispose();
        }

        _disposables.Clear();
    }
}

public static class CompositeDisposableExtensions
{
    public static CompositeDisposable Combine(this IDisposable disposable, IDisposable other)
    {
        if (disposable is CompositeDisposable composite)
        {
            composite.Add(other);
            return composite;
        }
        else
        {
            var newComposite = new CompositeDisposable(disposable, other);
            return newComposite;
        }
    }
}
