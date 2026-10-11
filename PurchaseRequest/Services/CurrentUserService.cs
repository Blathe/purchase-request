using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    // Resolves the signed-in Entra ID user to their local AppUser row and app roles.
    public class CurrentUserService(AuthenticationStateProvider authenticationStateProvider, IDbContextFactory<ApplicationDbContext> dbFactory)
    {
        private CurrentUser? user;

        public async Task<CurrentUser> GetAsync(CancellationToken cancellationToken = default)
        {
            if (user is not null)
            {
                return user;
            }

            var state = await authenticationStateProvider.GetAuthenticationStateAsync();
            var objectId = Guid.Parse(state.User.GetObjectId()
                ?? throw new InvalidOperationException("The signed-in user has no object ID (oid) claim."));

            await using var db = await dbFactory.CreateDbContextAsync(cancellationToken);
            var appUser = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.EntraObjectId == objectId, cancellationToken)
                ?? throw new InvalidOperationException("The signed-in user has no AppUser row. Sign out and sign in again.");

            var roles = AppRoles.All.Where(state.User.IsInRole).ToHashSet();
            user = new CurrentUser(appUser.Id, appUser.DisplayName, roles);
            return user;
        }
    }
}
