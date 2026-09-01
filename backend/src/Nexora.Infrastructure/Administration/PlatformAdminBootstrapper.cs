using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nexora.Application.Administration;
using Nexora.Domain.Identity;
using Nexora.Infrastructure.Persistence;

namespace Nexora.Infrastructure.Administration;

public sealed class PlatformAdminBootstrapper(NexoraDbContext db, IConfiguration configuration,
    ILogger<PlatformAdminBootstrapper> logger) : IPlatformAdminBootstrapper
{
    private static readonly Action<ILogger, Exception?> LogUserNotFound = LoggerMessage.Define(
        LogLevel.Warning, new EventId(1, "PlatformAdminBootstrapUserNotFound"), "Platform Admin bootstrap user was not found");
    private static readonly Action<ILogger, Exception?> LogApplied = LoggerMessage.Define(
        LogLevel.Information, new EventId(2, "PlatformAdminBootstrapApplied"), "Platform Admin bootstrap applied to configured user");

    public async Task BootstrapAsync(CancellationToken ct)
    {
        var email = configuration["Administration:BootstrapAdminEmail"]?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(email)) return;
        var user = await db.Users.Include(x => x.Roles).SingleOrDefaultAsync(x => x.NormalizedEmail == email, ct);
        if (user is null) { LogUserNotFound(logger, null); return; }
        var role = await db.Roles.Include(x => x.Permissions).ThenInclude(x => x.Permission)
            .SingleOrDefaultAsync(x => x.Name == "PlatformAdmin", ct);
        if (role is null) { role = new Role("PlatformAdmin"); db.Roles.Add(role); }
        foreach (var permissionName in PlatformPermissions.All)
        {
            var permission = await db.Permissions.SingleOrDefaultAsync(x => x.Name == permissionName, ct);
            if (permission is null) { permission = new Permission(permissionName); db.Permissions.Add(permission); }
            if (role.Permissions.All(x => x.PermissionId != permission.Id))
                role.Permissions.Add(new RolePermission(role.Id, permission.Id));
        }
        if (user.Roles.All(x => x.RoleId != role.Id)) user.Roles.Add(new UserRole(user.Id, role.Id));
        await db.SaveChangesAsync(ct);
        LogApplied(logger, null);
    }
}
