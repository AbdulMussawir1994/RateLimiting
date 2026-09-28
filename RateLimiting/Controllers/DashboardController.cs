using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.RateLimiting;
using RateLimiting.ExternalHelpers;

namespace RateLimiting.Controllers;

[Route("api/[controller]")]
[ApiController]
[ProducesResponseType(StatusCodes.Status429TooManyRequests)]
public class DashboardController : ControllerBase
{
    [HttpGet("Global")]
    [EnableRateLimiting(RateLimitingPolicies.Global)]
    public ActionResult GetDashboard()
    {
        return Ok("Dashboard");
    }

    [HttpGet("UserBurst")]
    [EnableRateLimiting(RateLimitingPolicies.UserBurst)]
    public ActionResult GetUserBurst()
    {
        return Ok(new
        {
            message = "Request accepted.",
            policy = RateLimitingPolicies.UserBurst
        });
    }

    [HttpGet("Burst")]
    [EnableRateLimiting(RateLimitingPolicies.Burst)]
    public ActionResult GetBurst()
    {
        return Ok(new { message = "Dashboard token bucket limit", policy = RateLimitingPolicies.Burst });
    }

    [HttpGet("Expensive")]
    [EnableRateLimiting(RateLimitingPolicies.Expensive)]
    public async Task<ActionResult> GetExpensive(CancellationToken cancellationToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        return Ok(new { message = "Dashboard concurrency limit", policy = RateLimitingPolicies.Expensive });
    }

    [HttpGet("Strict")]
    [EnableRateLimiting(RateLimitingPolicies.Strict)]
    public ActionResult GetStrict()
    {
        return Ok(new { message = "Dashboard fixed window limit", policy = RateLimitingPolicies.Strict });
    }

    [HttpGet("Sliding")]
    [EnableRateLimiting(RateLimitingPolicies.Sliding)]
    public ActionResult GetSliding()
    {
        return Ok(new { message = "Dashboard sliding window limit", policy = RateLimitingPolicies.Sliding });
    }
}
