﻿using API.Entities.Enums;

namespace API.DTOs.Statistics;

public class PublicationCount : ICount<PublicationStatus>
{
    public PublicationStatus Value { get; set; }
    public int Count { get; set; }
}
