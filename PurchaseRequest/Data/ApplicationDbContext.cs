using Microsoft.EntityFrameworkCore;

namespace PurchaseRequest.Data
{
    public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
    {
    }
}
