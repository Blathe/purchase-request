using Microsoft.EntityFrameworkCore;
using PurchaseRequest.Data;

namespace PurchaseRequest.Services
{
    public interface IRequestNumberGenerator
    {
        Task<string> NextAsync(ApplicationDbContext db, CancellationToken cancellationToken = default);
    }

    // Formats PR-YYYY-NNNN using the RequestNumbers SQL Server sequence.
    public class SqlRequestNumberGenerator(TimeProvider clock) : IRequestNumberGenerator
    {
        public async Task<string> NextAsync(ApplicationDbContext db, CancellationToken cancellationToken = default)
        {
            // ToListAsync keeps EF from wrapping the query in a subquery, where SQL Server rejects NEXT VALUE FOR.
            var next = (await db.Database
                .SqlQueryRaw<int>("SELECT NEXT VALUE FOR " + ApplicationDbContext.RequestNumberSequence + " AS Value")
                .ToListAsync(cancellationToken)).Single();
            return $"PR-{clock.GetUtcNow().Year}-{next:D4}";
        }
    }
}
