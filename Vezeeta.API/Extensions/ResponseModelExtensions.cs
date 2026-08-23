using Domain;
using Microsoft.AspNetCore.Mvc;

namespace Vezeeta.API.Extensions
{
    public static class ResponseModelExtensions
    {
        public static IActionResult ToActionResult<T>(this ResponseModel<T> response) where T : class
        {
            if (response.Success)
            {
                return new OkObjectResult(response);
            }

            return response.ErrorType switch
            {
                ErrorType.NotFound => new NotFoundObjectResult(response),
                ErrorType.Unauthorized => new UnauthorizedObjectResult(response),
                ErrorType.Forbidden => new ObjectResult(response) { StatusCode = StatusCodes.Status403Forbidden },
                ErrorType.Conflict => new ConflictObjectResult(response),
                ErrorType.Unexpected => new ObjectResult(response) { StatusCode = StatusCodes.Status500InternalServerError },
                _ => new BadRequestObjectResult(response)
            };
        }

        public static IActionResult ToCreatedResult<T>(this ResponseModel<T> response) where T : class
        {
            if (response.Success)
            {
                return new ObjectResult(response) { StatusCode = StatusCodes.Status201Created };
            }

            return response.ToActionResult();
        }
    }
}
