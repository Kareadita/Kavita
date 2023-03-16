﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums;
using API.Entities.Enums.UserPreferences;
using API.Entities.Interfaces;
using API.Entities.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace API.Data;

public sealed class DataContext : IdentityDbContext<AppUser, AppRole, int,
    IdentityUserClaim<int>, AppUserRole, IdentityUserLogin<int>,
    IdentityRoleClaim<int>, IdentityUserToken<int>>
{
    public DataContext(DbContextOptions options) : base(options)
    {
        ChangeTracker.Tracked += OnEntityTracked;
        ChangeTracker.StateChanged += OnEntityStateChanged;
    }

    public DbSet<Library> Library { get; set; } = null!;
    public DbSet<Series> Series { get; set; } = null!;
    public DbSet<Chapter> Chapter { get; set; } = null!;
    public DbSet<Volume> Volume { get; set; } = null!;
    public DbSet<AppUser> AppUser { get; set; } = null!;
    public DbSet<MangaFile> MangaFile { get; set; } = null!;
    public DbSet<AppUserProgress> AppUserProgresses { get; set; } = null!;
    public DbSet<AppUserRating> AppUserRating { get; set; } = null!;
    public DbSet<ServerSetting> ServerSetting { get; set; } = null!;
    public DbSet<AppUserPreferences> AppUserPreferences { get; set; } = null!;
    public DbSet<SeriesMetadata> SeriesMetadata { get; set; } = null!;
    public DbSet<CollectionTag> CollectionTag { get; set; } = null!;
    public DbSet<AppUserBookmark> AppUserBookmark { get; set; } = null!;
    public DbSet<ReadingList> ReadingList { get; set; } = null!;
    public DbSet<ReadingListItem> ReadingListItem { get; set; } = null!;
    public DbSet<Person> Person { get; set; } = null!;
    public DbSet<Genre> Genre { get; set; } = null!;
    public DbSet<Tag> Tag { get; set; } = null!;
    public DbSet<SiteTheme> SiteTheme { get; set; } = null!;
    public DbSet<SeriesRelation> SeriesRelation { get; set; } = null!;
    public DbSet<FolderPath> FolderPath { get; set; } = null!;
    public DbSet<Device> Device { get; set; } = null!;
    public DbSet<ServerStatistics> ServerStatistics { get; set; } = null!;


    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);


        builder.Entity<AppUser>()
            .HasMany(ur => ur.UserRoles)
            .WithOne(u => u.User)
            .HasForeignKey(ur => ur.UserId)
            .IsRequired();

        builder.Entity<AppRole>()
            .HasMany(ur => ur.UserRoles)
            .WithOne(u => u.Role)
            .HasForeignKey(ur => ur.RoleId)
            .IsRequired();

        builder.Entity<SeriesRelation>()
            .HasOne(pt => pt.Series)
            .WithMany(p => p.Relations)
            .HasForeignKey(pt => pt.SeriesId)
            .OnDelete(DeleteBehavior.Cascade);


        builder.Entity<SeriesRelation>()
            .HasOne(pt => pt.TargetSeries)
            .WithMany(t => t.RelationOf)
            .HasForeignKey(pt => pt.TargetSeriesId)
            .OnDelete(DeleteBehavior.Cascade);



        builder.Entity<AppUserPreferences>()
            .Property(b => b.BookThemeName)
            .HasDefaultValue("Dark");
        builder.Entity<AppUserPreferences>()
            .Property(b => b.BackgroundColor)
            .HasDefaultValue("#000000");
        builder.Entity<AppUserPreferences>()
            .Property(b => b.GlobalPageLayoutMode)
            .HasDefaultValue(PageLayoutMode.Cards);
        builder.Entity<AppUserPreferences>()
            .Property(b => b.BookReaderWritingStyle)
            .HasDefaultValue(WritingStyle.Horizontal);


        builder.Entity<Library>()
            .Property(b => b.FolderWatching)
            .HasDefaultValue(true);
        builder.Entity<Library>()
            .Property(b => b.IncludeInDashboard)
            .HasDefaultValue(true);
        builder.Entity<Library>()
            .Property(b => b.IncludeInRecommended)
            .HasDefaultValue(true);
        builder.Entity<Library>()
            .Property(b => b.IncludeInSearch)
            .HasDefaultValue(true);
        builder.Entity<Library>()
            .Property(b => b.ManageCollections)
            .HasDefaultValue(true);
    }


    private static void OnEntityTracked(object? sender, EntityTrackedEventArgs e)
    {
        if (e.FromQuery || e.Entry.State != EntityState.Added || e.Entry.Entity is not IEntityDate entity) return;

        entity.Created = DateTime.Now;
        entity.LastModified = DateTime.Now;
        entity.CreatedUtc = DateTime.UtcNow;
        entity.LastModifiedUtc = DateTime.UtcNow;
    }

    private static void OnEntityStateChanged(object? sender, EntityStateChangedEventArgs e)
    {
        if (e.NewState != EntityState.Modified || e.Entry.Entity is not IEntityDate entity) return;
        entity.LastModified = DateTime.Now;
        entity.LastModifiedUtc = DateTime.UtcNow;
    }

    private void OnSaveChanges()
    {
        foreach (var saveEntity in ChangeTracker.Entries()
                     .Where(e => e.State == EntityState.Modified)
                     .Select(entry => entry.Entity)
                     .OfType<IHasConcurrencyToken>())
        {
            saveEntity.OnSavingChanges();
        }
    }

    #region SaveChanges overrides

    public override int SaveChanges()
    {
        OnSaveChanges();

        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        OnSaveChanges();

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
    {
        OnSaveChanges();

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
    {
        OnSaveChanges();

        return base.SaveChangesAsync(cancellationToken);
    }

    #endregion
}
