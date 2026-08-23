using Domain;
using Domain.DTOs.AuthDTOs;
using Domain.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Service;
using Vezeeta.API.Extensions;

namespace Vezeeta.API.Controllers
{
    [Route("admin/doctor")]
    [ApiController]
    public class AdminDoctorController : ControllerBase
    {
        private readonly IAuthService authService;
        private readonly IAdminDoctorService adminDoctorService;

        public AdminDoctorController(IAuthService authService, IAdminDoctorService adminDoctorService)
        {
            this.authService = authService;
            this.adminDoctorService = adminDoctorService;
        }

        [HttpPost("registerDoctor")]
        public async Task<IActionResult> Register([FromForm] RegisterDoctorDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await authService.RegisterAsync(model, "Doctor");

            return result.ToCreatedResult();
        }

        [HttpPut("updateDoctor")]
        public async Task<IActionResult> RegisterAsync([FromForm] EditDoctorDTO model)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var result = await authService.UpdateAsync(model);

            return result.ToActionResult();
        }

        [HttpDelete("deleteDoctor/id={doctorId}")]
        public async Task<IActionResult> RegisterAsync(string doctorId)
        {
            var result = await authService.DeleteAsync(doctorId);

            return result.ToActionResult();
        }

        [HttpGet("getAllSpecializations")]
        public async Task<IActionResult> GetSpecializationsAsync(string search = null, int page = 1, int pageSize = 5)
        {
            var result = await adminDoctorService.GetAllSpecializationsAsync(search, page, pageSize);

            return result.ToActionResult();
        }

        [HttpGet("getAllDoctors")]
        public async Task<IActionResult> GetDoctorsAsync(string search = "", int page = 1, int pageSize = 5)
        {
            var result = await adminDoctorService.GetAllDoctorsAsync("Doctor", search, page, pageSize);

            return result.ToActionResult();
        }

        [HttpGet("getDoctor/id={doctorId}")]
        public async Task<IActionResult> GetDoctorByIdAsync(string doctorId)
        {
            var result = await adminDoctorService.GetDoctorByIdAsync(doctorId);

            return result.ToActionResult();
        }

        //[HttpPost("addrole")]
        //public async Task<IActionResult> AddRoleAsync([FromBody] AddRoleDTO model, string role)
        //{
        //    if (!ModelState.IsValid)
        //        return BadRequest(ModelState);

        //    var result = await authService.AddRoleAsync(model, role);

        //    if (!string.IsNullOrEmpty(result))
        //        return BadRequest(result);

        //    return Ok(model);
        //}
    }
}
