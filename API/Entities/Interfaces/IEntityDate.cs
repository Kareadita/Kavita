using System;

namespace API.Entities.Interfaces;

public interface IEntityDate
{
    DateTime Created { get; set; }
    DateTime CreatedUtc { get; set; }
    DateTime LastModified { get; set; }
    DateTime LastModifiedUtc { get; set; }
}
