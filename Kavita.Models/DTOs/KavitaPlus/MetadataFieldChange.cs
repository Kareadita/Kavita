namespace Kavita.Models.DTOs.KavitaPlus;
#nullable enable

/// <summary>Records a single field's before/after state during a metadata write.</summary>
public sealed record MetadataFieldChange(string Field, object? From, object? To);
