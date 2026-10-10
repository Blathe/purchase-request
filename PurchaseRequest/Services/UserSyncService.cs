using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Identity.Web;
using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    // Keeps the local AppUser row in step with the signed-in Entra ID user.
    public class UserSyncService(ApplicationDbContext db)
    {
        public async Task<AppUser> SyncAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
        {
            var objectId = Guid.Parse(principal.GetObjectId()
                ?? throw new InvalidOperationException("The sign-in token has no object ID (oid) claim."));

            var user = await db.Users.SingleOrDefaultAsync(u => u.EntraObjectId == objectId, cancellationToken);
            if (user is null)
            {
                user = new AppUser { EntraObjectId = objectId };
                db.Users.Add(user);
            }

            user.DisplayName = principal.FindFirstValue("name") ?? user.DisplayName;
            user.Email = principal.FindFirstValue("preferred_username")
                ?? principal.FindFirstValue(ClaimTypes.Email)
                ?? user.Email;

            await db.SaveChangesAsync(cancellationToken);
            return user;
        }
    }
}
