using Domain.DTOs.ReviewDTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Services
{
    public interface IReviewService
    {
        Task<ResponseModel<ReviewDTO>> CreateReviewAsync(string patientId, CreateReviewDTO dto);
        Task<ResponseModel<IEnumerable<ReviewDTO>>> GetDoctorReviewsAsync(string doctorId, int page = 1, int pageSize = 5);
        Task<ResponseModel<DoctorRatingDTO>> GetDoctorRatingAsync(string doctorId);
    }
}
