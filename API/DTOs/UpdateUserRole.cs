using System.Collections.Generic;
using MediatR;

namespace API.DTOs;

public class UpdateUserRole : IRequest<bool>
{
    public string Username { get; init; }
    public IList<string> Roles { get; init; }
}
