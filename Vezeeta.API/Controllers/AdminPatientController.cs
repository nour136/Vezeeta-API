using Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vezeeta.API.Extensions;

namespace Vezeeta.API.Controllers
{
    [Route("admin/patient")]
    [ApiController]
    [Authorize(Roles = "Admin")]
    public class AdminPatientController : ControllerBase
    {
        private readonly IAdminPatientService adminPatientService;

        public AdminPatientController(IAdminPatientService adminPatientService)
        {
            this.adminPatientService = adminPatientService;
        }

        [HttpGet("getAllPatients")]
        public async Task<IActionResult> GetPatientsAsync(string search = "", int page = 1, int pageSize = 5)
        {
            var result = await adminPatientService.GetAllPatientsAsync("Patient", search, page, pageSize);

            return result.ToActionResult();
        }

        [HttpGet("getPatient/id={patientId}")]
        public async Task<IActionResult> GetPatientById(string patientId)
        {
            var result = await adminPatientService.GetPatientByIdAsync(patientId);

            return result.ToActionResult();
        }
    }
}
