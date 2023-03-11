namespace API.Helpers.Builders;

public interface IEntityBuilder<out T>
{
    public T Build();
}
