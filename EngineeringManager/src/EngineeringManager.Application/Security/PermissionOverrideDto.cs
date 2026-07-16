using EngineeringManager.Domain.Security;

namespace EngineeringManager.Application.Security;

public sealed record PermissionOverrideDto(string PermissionKey, PermissionEffect Effect);
