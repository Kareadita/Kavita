namespace Kavita.Models.Builders;

public interface IEntityBuilder<out T>
{
    public T Build();
}
