namespace API.Tests.Helpers.Builders;

public interface IEntityBuilder<out T>
{
    public T Build();
}
