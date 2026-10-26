using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RESK.WIL.Security;

namespace RESK.WIL.Controllers.API
{
    /*
     * =========================================================
     * BASE CLASS FOR ALL /api CONTROLLERS
     * =========================================================
     *
     * - JSON in and out.
     * - Rate limited (see Security/ReskRateLimiting.cs).
     * - Every error uses the same shape: RFC 7807 "problem details".
     *
     *   {
     *     "type":    "https://tools.ietf.org/html/rfc9110#section-15.5.5",
     *     "title":   "Not Found",
     *     "status":  404,
     *     "detail":  "Proposal 42 was not found.",
     *     "traceId": "00-..."
     *   }
     *
     *   Validation errors add an "errors" object (field -> messages).
     *   The same shape is used for 401, 403, 429 and unexpected 500
     *   errors (see Program.cs).
     */
    [ApiController]
    [Produces("application/json")]
    [EnableRateLimiting(ReskRateLimiting.Api)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status500InternalServerError)]
    public abstract class ApiControllerBase : ControllerBase
    {
        // 400 - the request was understood but can't be accepted.
        protected ObjectResult BadRequestProblem(string detail) =>
            Problem(detail: detail, statusCode: StatusCodes.Status400BadRequest);

        // 401 - not signed in.
        protected ObjectResult UnauthorizedProblem(string detail = "Sign in to use this endpoint.") =>
            Problem(detail: detail, statusCode: StatusCodes.Status401Unauthorized);

        // 404 - nothing with that id (or not yours).
        protected ObjectResult NotFoundProblem(string detail) =>
            Problem(detail: detail, statusCode: StatusCodes.Status404NotFound);

        // 409 - clashes with the current state (duplicate email, wrong workflow step).
        protected ObjectResult ConflictProblem(string detail) =>
            Problem(detail: detail, statusCode: StatusCodes.Status409Conflict);
    }
}
