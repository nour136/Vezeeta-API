using Domain;
using Domain.DTOs.ReviewDTOs;
using Domain.Enums;
using Domain.Models;
using Domain.Repositories;
using Domain.Services;
using Domain.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Service
{
    public class ReviewService : IReviewService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly ILogger<ReviewService> logger;

        public ReviewService(IUnitOfWork unitOfWork, ILogger<ReviewService> logger)
        {
            this.unitOfWork = unitOfWork;
            this.logger = logger;
        }

        public async Task<ResponseModel<ReviewDTO>> CreateReviewAsync(string patientId, CreateReviewDTO dto)
        {
            var booking = await unitOfWork.Bookings.GetByIdAsync(dto.BookingId);

            if (booking is null)
                return new ResponseModel<ReviewDTO> { Message = "No such booking with that ID", ErrorType = ErrorType.NotFound };

            if (booking.Patient.Id != patientId)
                return new ResponseModel<ReviewDTO> { Message = "No such booking with that ID", ErrorType = ErrorType.NotFound };

            if (booking.Request.RequestState != RequestState.Completed)
                return new ResponseModel<ReviewDTO> { Message = $"Only completed appointments can be reviewed (this one is {booking.Request.RequestState}).", ErrorType = ErrorType.Conflict };

            var existingReview = await unitOfWork.Reviews.GetAllByPropertyAsync(r => r.BookingId == booking.Id);
            if (existingReview.Any())
                return new ResponseModel<ReviewDTO> { Message = "This appointment has already been reviewed.", ErrorType = ErrorType.Conflict };

            var review = new Review
            {
                BookingId = booking.Id,
                PatientId = patientId,
                DoctorId = booking.Slot.DoctorId,
                Rating = dto.Rating,
                Comment = dto.Comment,
                CreatedAt = DateTime.Now
            };

            try
            {
                await unitOfWork.Reviews.CreateAsync(review);
                unitOfWork.Complete();
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to create review for booking {BookingId} by patient {PatientId}", dto.BookingId, patientId);
                return new ResponseModel<ReviewDTO> { Message = "Something went wrong.", ErrorType = ErrorType.Unexpected };
            }

            logger.LogInformation("Patient {PatientId} reviewed doctor {DoctorId} via booking {BookingId} (rating {Rating})", patientId, review.DoctorId, booking.Id, dto.Rating);

            var reviewDTO = new ReviewDTO
            {
                Id = review.Id,
                PatientName = booking.Patient.FirstName + " " + booking.Patient.LastName,
                Rating = review.Rating,
                Comment = review.Comment,
                CreatedAt = review.CreatedAt
            };

            return new ResponseModel<ReviewDTO> { Success = true, Message = "Review submitted successfully.", Data = reviewDTO };
        }

        public async Task<ResponseModel<IEnumerable<ReviewDTO>>> GetDoctorReviewsAsync(string doctorId, int page = 1, int pageSize = 5)
        {
            var reviews = await unitOfWork.Reviews.GetAllPaginatedFilteredAsync(r => r.DoctorId == doctorId, page, pageSize);

            var reviewDTOs = reviews.Select(r => new ReviewDTO
            {
                Id = r.Id,
                PatientName = r.Patient.FirstName + " " + r.Patient.LastName,
                Rating = r.Rating,
                Comment = r.Comment,
                CreatedAt = r.CreatedAt
            });

            var meta = new Metadata
            {
                Page = page,
                PageSize = pageSize,
                Next = page + 1,
                Previous = page - 1
            };

            return new ResponseModel<IEnumerable<ReviewDTO>> { Success = true, Message = "Reviews retrieved.", Data = reviewDTOs, MetaData = meta };
        }

        public async Task<ResponseModel<DoctorRatingDTO>> GetDoctorRatingAsync(string doctorId)
        {
            var reviews = (await unitOfWork.Reviews.GetAllByPropertyAsync(r => r.DoctorId == doctorId)).ToList();

            var ratingDTO = new DoctorRatingDTO
            {
                AverageRating = reviews.Any() ? Math.Round(reviews.Average(r => r.Rating), 2) : 0,
                ReviewCount = reviews.Count
            };

            return new ResponseModel<DoctorRatingDTO> { Success = true, Message = "Doctor rating retrieved.", Data = ratingDTO };
        }
    }
}
