namespace Cartex.Persistence;

public sealed class TransactionScoped<T>(IApplicationDbContext db, Func<T> create)
{
    private long _generation = -1;
    private T _value = default!;

    public T Value
    {
        get
        {
            if (_generation == db.TransactionGeneration)
                return _value;
            _generation = db.TransactionGeneration;
            _value = create();
            return _value;
        }
    }
}
