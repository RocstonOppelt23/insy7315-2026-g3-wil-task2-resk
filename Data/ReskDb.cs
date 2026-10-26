using Microsoft.EntityFrameworkCore;

namespace RESK.WIL.Data
{
    /*
     * =========================================================
     * DATABASE ACCESS FOR THE RESK STORES
     * =========================================================
     *
     * The account, role, review and audit stores are static classes
     * that are called from many controllers and middleware. Instead
     * of changing every caller, the stores open a short-lived
     * ApplicationDbContext through this class for each read or write.
     *
     * Program.cs calls Configure() once at startup with the
     * IDbContextFactory registered by AddDbContextFactory.
     * Tests call it with an in-memory factory.
     */
    public static class ReskDb
    {
        private static IDbContextFactory<ApplicationDbContext>? _factory;


        public static void Configure(IDbContextFactory<ApplicationDbContext> factory)
        {
            _factory = factory;
        }


        public static ApplicationDbContext Open()
        {
            if (_factory == null)
            {
                throw new InvalidOperationException(
                    "ReskDb has not been configured. Call ReskDb.Configure() at startup.");
            }

            return _factory.CreateDbContext();
        }
    }
}
