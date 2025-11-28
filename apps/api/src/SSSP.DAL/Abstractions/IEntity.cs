namespace SSSP.DAL.Abstractions
{
    public interface IEntity<TKey>
    {
        TKey Id { get; set; }
    }
}
