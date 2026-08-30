using Domain.DTOs.ReviewDTOs;
using Domain.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Vezeeta.API.Extensions;

namespace Vezeeta.API.Controllers
{
    [Route("/review")]
    [ApiController]
    public class ReviewController : ControllerBase
    {
        private readonly IReviewService reviewService;

        public ReviewController(IReviewService reviewService)
        {
            this.reviewService = reviewService;
        }

        [Authorize(Roles = "Patient")]
        [HttpPost]
        public async Task<IActionResult> CreateReviewAsync([FromBody] CreateReviewDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var patientId = User.FindFirst("uid")?.Value;

            var result = await reviewService.CreateReviewAsync(patientId, dto);

            return result.ToCreatedResult();
        }

        [Authorize]
        [HttpGet("doctor/id={doctorId}")]
        public async Task<IActionResult> GetDoctorReviewsAsync(string doctorId, int page = 1, int pageSize = 5)
        {
            var result = await reviewService.GetDoctorReviewsAsync(doctorId, page, pageSize);

            return result.ToActionResult();
        }

        [Authorize]
        [HttpGet("doctor/id={doctorId}/rating")]
        public async Task<IActionResult> GetDoctorRatingAsync(string doctorId)
        {
            var result = await reviewService.GetDoctorRatingAsync(doctorId);

            return result.ToActionResult();
        }
    }
}
