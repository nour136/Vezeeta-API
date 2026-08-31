using Domain;
using Domain.DTOs.AuthDTOs;
using Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vezeeta.API.Extensions;

namespace Vezeeta.API.Controllers
{
    [Route("admin")]
    [ApiController]
    public class AdminController : ControllerBase
    {
        private readonly IAuthService authService;

        public AdminController(IAuthService authService)
        {
            this.authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginAsync([FromForm] LoginDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await authService.LoginAsync(model);

            if (!result.Success)
                return result.ToActionResult();

            if (result.Data.Roles.Contains("Admin"))
                return result.ToActionResult();

            return new ResponseModel<AuthDTO> { Message = "Invalid role!", ErrorType = ErrorType.Forbidden }.ToActionResult();
        }
    }
}
