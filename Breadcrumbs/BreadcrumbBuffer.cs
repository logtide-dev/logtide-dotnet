namespace LogTide.SDK.Breadcrumbs;

internal sealed class BreadcrumbBuffer
{
    private readonly int _maxSize;
    private readonly Queue<Breadcrumb> _queue = new();
    private readonly object _lock = new();

    public BreadcrumbBuffer(int maxSize = 50) => _maxSize = maxSize;

    public void Add(Breadcrumb breadcrumb)
    {
        lock (_lock)
        {
            if (_queue.Count >= _maxSize) _queue.Dequeue();
            _queue.Enqueue(breadcrumb);
        }
    }

    public IReadOnlyList<Breadcrumb> GetAll()
    {
        lock (_lock) { return _queue.ToArray(); }
    }
}
