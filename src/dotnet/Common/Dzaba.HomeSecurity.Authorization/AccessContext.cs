namespace Dzaba.HomeSecurity.Authorization;

/// <summary>
/// A user's effective access in one tenant: whether they're a member at
/// all, and if so, which permission keys they hold. Carrying both in one
/// result lets a single cached round-trip answer both "is this user a
/// member of this tenant" (tenant resolution) and "does this user have
/// permission X" (authorization) - see IPermissionSourceLoader.
/// </summary>
public sealed record AccessContext(bool IsMember, string[] PermissionKeys);
