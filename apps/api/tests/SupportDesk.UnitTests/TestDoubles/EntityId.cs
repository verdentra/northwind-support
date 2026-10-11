using System.Reflection;
using SupportDesk.Domain.Common;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>Assigns database identities to domain entities in unit tests.</summary>
internal static class EntityId
{
    private static readonly PropertyInfo IdProperty =
        typeof(Entity).GetProperty(nameof(Entity.Id))
        ?? throw new InvalidOperationException("Entity.Id was not found.");

    public static T WithId<T>(T entity, int id) where T : Entity
    {
        IdProperty.SetValue(entity, id);
        return entity;
    }
}
