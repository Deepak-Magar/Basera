namespace Basera.Shared.Users;

/// <summary>The signed-in user, for audit columns and logs. Null outside a request (background jobs).</summary>
public interface ICurrentUser
{
    Guid? UserId { get; }
}
