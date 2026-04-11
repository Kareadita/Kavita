using System.Collections.Generic;

namespace Kavita.Models.Entities.Interfaces;

public interface IHasTags<T> where T : class, ITag
{
    ICollection<T> Tags { get; set; }
}
