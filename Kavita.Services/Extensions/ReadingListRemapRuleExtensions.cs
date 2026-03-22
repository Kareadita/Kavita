using System.Collections.Generic;
using System.Linq;
using Kavita.Models.DTOs.ReadingLists.CBL.Internal;
using Kavita.Models.Entities.ReadingLists;

namespace Kavita.Services.Extensions;

public static class ReadingListRemapRuleExtensions
{
    public static ReadingListRemapRule? FirstMatchVolumeAndIssueOrDefault(this List<ReadingListRemapRule> rules, ParsedCblItem item)
    {
        return rules.FirstOrDefault(r =>
            !string.IsNullOrEmpty(r.CblVolume) && r.CblVolume == item.Volume &&
            !string.IsNullOrEmpty(r.CblNumber) && r.CblNumber == item.Number);
    }

    public static ReadingListRemapRule? FirstMatchIssueOrDefault(this List<ReadingListRemapRule> rules, ParsedCblItem item)
    {
        return rules.FirstOrDefault(r =>
            !string.IsNullOrEmpty(r.CblNumber) && r.CblNumber == item.Number &&
            string.IsNullOrEmpty(r.CblVolume));
    }




}
