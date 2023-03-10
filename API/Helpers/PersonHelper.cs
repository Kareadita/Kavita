﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Entities.Enums;
using API.Extensions;

namespace API.Helpers;

public static class PersonHelper
{
    /// <summary>
    /// Given a list of all existing people, this will check the new names and roles and if it doesn't exist in allPeople, will create and
    /// add an entry. For each person in name, the callback will be executed.
    /// </summary>
    /// <remarks>This does not remove people if an empty list is passed into names</remarks>
    /// <remarks>This is used to add new people to a list without worrying about duplicating rows in the DB</remarks>
    /// <param name="allPeople"></param>
    /// <param name="names"></param>
    /// <param name="role"></param>
    /// <param name="action"></param>
    public static void UpdatePeople(ICollection<Person> allPeople, IEnumerable<string> names, PersonRole role, Action<Person> action)
    {
        var allPeopleTypeRole = allPeople.Where(p => p.Role == role).ToList();

        foreach (var name in names)
        {
            var normalizedName = name.ToNormalized();
            var person = allPeopleTypeRole.FirstOrDefault(p =>
                p.NormalizedName != null && p.NormalizedName.Equals(normalizedName));
            if (person == null)
            {
                person = DbFactory.Person(name, role);
                allPeople.Add(person);
            }

            action(person);
        }
    }

    /// <summary>
    /// Remove people on a list for a given role
    /// </summary>
    /// <remarks>Used to remove before we update/add new people</remarks>
    /// <param name="existingPeople">Existing people on Entity</param>
    /// <param name="people">People from metadata</param>
    /// <param name="role">Role to filter on</param>
    /// <param name="action">Callback which will be executed for each person removed</param>
    public static void RemovePeople(ICollection<Person> existingPeople, IEnumerable<string> people, PersonRole role, Action<Person>? action = null)
    {
        var normalizedPeople = people.Select(Services.Tasks.Scanner.Parser.Parser.Normalize).ToList();
        if (normalizedPeople.Count == 0)
        {
            var peopleToRemove = existingPeople.Where(p => p.Role == role).ToList();
            foreach (var existingRoleToRemove in peopleToRemove)
            {
                existingPeople.Remove(existingRoleToRemove);
                action?.Invoke(existingRoleToRemove);
            }
            return;
        }

        foreach (var person in normalizedPeople)
        {
            var existingPerson = existingPeople.FirstOrDefault(p => p.Role == role && person.Equals(p.NormalizedName));
            if (existingPerson == null) continue;

            existingPeople.Remove(existingPerson);
            action?.Invoke(existingPerson);
        }

    }

    /// <summary>
    /// Removes all people that are not present in the removeAllExcept list.
    /// </summary>
    /// <param name="existingPeople"></param>
    /// <param name="removeAllExcept"></param>
    /// <param name="action">Callback for all entities that should be removed</param>
    public static void KeepOnlySamePeopleBetweenLists(IEnumerable<Person> existingPeople, ICollection<Person> removeAllExcept, Action<Person>? action = null)
    {
        foreach (var person in existingPeople)
        {
            var existingPerson = removeAllExcept
                .FirstOrDefault(p => p.Role == person.Role && person.NormalizedName.Equals(p.NormalizedName));
            if (existingPerson == null)
            {
                action?.Invoke(person);
            }
        }
    }

    /// <summary>
    /// Adds the person to the list if it's not already in there
    /// </summary>
    /// <param name="metadataPeople"></param>
    /// <param name="person"></param>
    public static void AddPersonIfNotExists(ICollection<Person> metadataPeople, Person person)
    {
        if (string.IsNullOrEmpty(person.Name)) return;
        var existingPerson = metadataPeople.SingleOrDefault(p =>
            p.NormalizedName == person.Name.ToNormalized() && p.Role == person.Role);
        if (existingPerson == null)
        {
            metadataPeople.Add(person);
        }
    }

    /// <summary>
    /// Adds the person to the list if it's not already in there
    /// </summary>
    /// <param name="metadataPeople"></param>
    /// <param name="person"></param>
    public static void AddPersonIfNotExists(BlockingCollection<Person> metadataPeople, Person person)
    {
        var existingPerson = metadataPeople.SingleOrDefault(p =>
            p.NormalizedName == person.Name?.ToNormalized() && p.Role == person.Role);
        if (existingPerson == null)
        {
            metadataPeople.Add(person);
        }
    }

    public static void UpdatePeopleList(PersonRole role, ICollection<PersonDto>? tags, Series series, IReadOnlyCollection<Person> allTags,
        Action<Person> handleAdd, Action onModified)
    {
        if (tags == null) return;
        var isModified = false;
        // I want a union of these 2 lists. Return only elements that are in both lists, but the list types are different
        var existingTags = series.Metadata.People.Where(p => p.Role == role).ToList();
        foreach (var existing in existingTags)
        {
            if (tags.SingleOrDefault(t => t.Id == existing.Id) == null) // This needs to check against role
            {
                // Remove tag
                series.Metadata.People.Remove(existing);
                isModified = true;
            }
        }

        // At this point, all tags that aren't in dto have been removed.
        foreach (var tag in tags)
        {
            var existingTag = allTags.SingleOrDefault(t => t.Name == tag.Name && t.Role == tag.Role);
            if (existingTag != null)
            {
                if (series.Metadata.People.Where(t => t.Role == tag.Role).All(t => t.Name != null && !t.Name.Equals(tag.Name)))
                {
                    handleAdd(existingTag);
                    isModified = true;
                }
            }
            else
            {
                // Add new tag
                handleAdd(DbFactory.Person(tag.Name, role));
                isModified = true;
            }
        }

        if (isModified)
        {
            onModified();
        }
    }
}
