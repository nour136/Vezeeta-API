using Hangfire.Dashboard;

namespace Vezeeta.API
{
    // The dashboard is a plain browser page, not an API endpoint - a browser won't attach the
    // JWT bearer token this API uses everywhere else, so gating it behind the same Admin-role
    // JWT auth as the rest of the API isn't practical here. Instead, it's restricted to the
    // Development environment only. That's fine for this project running locally or through the
    // current Docker Compose setup (which runs as Development) - but it is NOT a real
    // access-control mechanism. A genuine production deployment would need a separate
    // authentication scheme (e.g. cookie auth) in front of this dashboard.
    public class HangfireDashboardAuthFilter : IDashboardAuthorizationFilter
    {
        private readonly IWebHostEnvironment env;

        public HangfireDashboardAuthFilter(IWebHostEnvironment env)
        {
            this.env = env;
        }

        public bool Authorize(DashboardContext context) => env.IsDevelopment();
    }
}
