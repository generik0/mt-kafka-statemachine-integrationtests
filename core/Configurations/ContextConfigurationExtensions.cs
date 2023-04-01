using Microsoft.EntityFrameworkCore;

namespace Mass.Transit.Outbox.Repo.Replicate.core.Configurations;

public static class ContextConfigurationExtensions
{
    public static ModelBuilder ContextConfiguration<TEntity, TConfiguration>(
        this ModelBuilder modelBuilder)
        where TEntity : class
        where TConfiguration : IEntityTypeConfiguration<TEntity>
    {
        var instance = (IEntityTypeConfiguration<TEntity>) Activator.CreateInstance(typeof (TConfiguration))!;
        modelBuilder.ApplyConfiguration(instance);
        return modelBuilder;
    }
}