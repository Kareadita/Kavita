using System;
using System.ComponentModel.DataAnnotations;
using API.Entities.Enums;

namespace API.Entities;

public class SyncHistory
{
    [Key]
    public required SyncKey Key { get; set; }
    public required DateTime Value { get; set; }

}
