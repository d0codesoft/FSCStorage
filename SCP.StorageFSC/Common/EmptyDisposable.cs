namespace scp.filestorage.Common
{
    public sealed class CompositeDisposable : IDisposable
    {
        private readonly IDisposable[] _items;

        public CompositeDisposable(params IDisposable[] items)
        {
            _items = items;
        }

        public void Dispose()
        {
            for (var i = _items.Length - 1; i >= 0; i--)
            {
                _items[i].Dispose();
            }
        }
    }

    public sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();

        private EmptyDisposable()
        {
        }

        public void Dispose()
        {
        }
    }
}
