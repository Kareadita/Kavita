using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using API.Entities;
using API.Entities.Enums.UserPreferences;
using API.Entities.Interfaces;
using API.Entities.Metadata;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace API.Data
{
    public sealed class DataContext : IdentityDbContext<AppUser, AppRole, int,
        IdentityUserClaim<int>, AppUserRole, IdentityUserLogin<int>,
        IdentityRoleClaim<int>, IdentityUserToken<int>>
    {
        public DataContext(DbContextOptions options) : base(options)
        {
            ChangeTracker.Tracked += OnEntityTracked;
            ChangeTracker.StateChanged += OnEntityStateChanged;
        }

        public DbSet<Library> Library { get; set; }
        public DbSet<Series> Series { get; set; }
        public DbSet<Chapter> Chapter { get; set; }
        public DbSet<Volume> Volume { get; set; }
        public DbSet<AppUser> AppUser { get; set; }
        public DbSet<MangaFile> MangaFile { get; set; }
        public DbSet<AppUserProgress> AppUserProgresses { get; set; }
        public DbSet<AppUserRating> AppUserRating { get; set; }
        public DbSet<ServerSetting> ServerSetting { get; set; }
        public DbSet<AppUserPreferences> AppUserPreferences { get; set; }
        public DbSet<SeriesMetadata> SeriesMetadata { get; set; }
        public DbSet<CollectionTag> CollectionTag { get; set; }
        public DbSet<AppUserBookmark> AppUserBookmark { get; set; }
        public DbSet<ReadingList> ReadingList { get; set; }
        public DbSet<ReadingListItem> ReadingListItem { get; set; }
        public DbSet<Person> Person { get; set; }
        public DbSet<Genre> Genre { get; set; }
        public DbSet<Tag> Tag { get; set; }
        public DbSet<SiteTheme> SiteTheme { get; set; }
        public DbSet<SeriesRelation> SeriesRelation { get; set; }
        public DbSet<FolderPath> FolderPath { get; set; }


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
                .OnDelete(DeleteBehavior.ClientCascade);

            builder.Entity<SeriesRelation>()
                .HasOne(pt => pt.TargetSeries)
                .WithMany(t => t.RelationOf)
                .HasForeignKey(pt => pt.TargetSeriesId)
                .OnDelete(DeleteBehavior.ClientCascade);


            builder.Entity<AppUserPreferences>()
                .Property(b => b.BookThemeName)
                .HasDefaultValue("Dark");
            builder.Entity<AppUserPreferences>()
                .Property(b => b.BackgroundColor)
                .HasDefaultValue("#000000");

            builder.Entity<AppUserPreferences>()
                .Property(b => b.GlobalPageLayoutMode)
                .HasDefaultValue(PageLayoutMode.Cards);
        }


        private static void OnEntityTracked(object sender, EntityTrackedEventArgs e)
        {
            if (!e.FromQuery && e.Entry.State == EntityState.Added && e.Entry.Entity is IEntityDate entity)
            {
                entity.Created = DateTime.Now;
                entity.LastModified = DateTime.Now;
            }

        }

        private static void OnEntityStateChanged(object sender, EntityStateChangedEventArgs e)
        {
            if (e.NewState == EntityState.Modified && e.Entry.Entity is IEntityDate entity)
                entity.LastModified = DateTime.Now;
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
            this.OnSaveChanges();

            return base.SaveChanges();
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            this.OnSaveChanges();

            return base.SaveChanges(acceptAllChangesOnSuccess);
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            this.OnSaveChanges();

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            this.OnSaveChanges();

            return base.SaveChangesAsync(cancellationToken);
        }

        #endregion
    }
}
