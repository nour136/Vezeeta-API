using Domain;
using Domain.DTOs.ReviewDTOs;
using Domain.Enums;
using Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Service.Tests
{
    public class ReviewServiceTests
    {
        private const string PatientId = "patient-1";
        private const string OtherPatientId = "patient-2";
        private const string DoctorId = "doctor-1";

        private static (ReviewService service, TestUnitOfWork uow) CreateService()
        {
            var uow = new TestUnitOfWork();
            var service = new ReviewService(uow, NullLogger<ReviewService>.Instance);
            return (service, uow);
        }

        private static Booking MakeBooking(int id, string patientId, string doctorId, RequestState state)
        {
            var patient = new ApplicationUser { Id = patientId, FirstName = "Test", LastName = "Patient" };
            var slot = new AppointmentSlot { Id = id, DoctorId = doctorId, Status = SlotStatus.Booked, Date = DateOnly.FromDateTime(DateTime.Now.AddDays(-1)), Time = new TimeOnly(9, 0) };
            var request = new Request { Id = id, RequestState = state };
            return new Booking { Id = id, Patient = patient, Slot = slot, Request = request };
        }

        [Fact]
        public async Task CreateReviewAsync_ReturnsNotFound_WhenBookingDoesNotExist()
        {
            var (service, _) = CreateService();

            var result = await service.CreateReviewAsync(PatientId, new CreateReviewDTO { BookingId = 999, Rating = 5 });

            Assert.False(result.Success);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
        }

        [Fact]
        public async Task CreateReviewAsync_ReturnsNotFound_WhenBookingNotOwnedByPatient()
        {
            var (service, uow) = CreateService();
            var booking = MakeBooking(1, OtherPatientId, DoctorId, RequestState.Completed);
            uow.BookingsFake.Items.Add(booking);

            var result = await service.CreateReviewAsync(PatientId, new CreateReviewDTO { BookingId = booking.Id, Rating = 5 });

            Assert.False(result.Success);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
        }

        [Fact]
        public async Task CreateReviewAsync_ReturnsConflict_WhenBookingNotCompleted()
        {
            var (service, uow) = CreateService();
            var booking = MakeBooking(1, PatientId, DoctorId, RequestState.Confirmed);
            uow.BookingsFake.Items.Add(booking);

            var result = await service.CreateReviewAsync(PatientId, new CreateReviewDTO { BookingId = booking.Id, Rating = 5 });

            Assert.False(result.Success);
            Assert.Equal(ErrorType.Conflict, result.ErrorType);
            Assert.Empty(uow.ReviewsFake.Items);
        }

        [Fact]
        public async Task CreateReviewAsync_ReturnsConflict_WhenAlreadyReviewed()
        {
            var (service, uow) = CreateService();
            var booking = MakeBooking(1, PatientId, DoctorId, RequestState.Completed);
            uow.BookingsFake.Items.Add(booking);
            uow.ReviewsFake.Items.Add(new Review { Id = 1, BookingId = booking.Id, PatientId = PatientId, DoctorId = DoctorId, Rating = 4 });

            var result = await service.CreateReviewAsync(PatientId, new CreateReviewDTO { BookingId = booking.Id, Rating = 5 });

            Assert.False(result.Success);
            Assert.Equal(ErrorType.Conflict, result.ErrorType);
            Assert.Single(uow.ReviewsFake.Items);
        }

        [Fact]
        public async Task CreateReviewAsync_Succeeds_WhenCompletedAndNotYetReviewed()
        {
            var (service, uow) = CreateService();
            var booking = MakeBooking(1, PatientId, DoctorId, RequestState.Completed);
            uow.BookingsFake.Items.Add(booking);

            var result = await service.CreateReviewAsync(PatientId, new CreateReviewDTO { BookingId = booking.Id, Rating = 5, Comment = "Great doctor" });

            Assert.True(result.Success);
            Assert.Single(uow.ReviewsFake.Items);
            Assert.Equal(DoctorId, uow.ReviewsFake.Items[0].DoctorId);
            Assert.Equal(PatientId, uow.ReviewsFake.Items[0].PatientId);
            Assert.Equal(5, result.Data!.Rating);
            Assert.Equal("Test Patient", result.Data.PatientName);
        }

        [Fact]
        public async Task GetDoctorRatingAsync_ReturnsZero_WhenNoReviews()
        {
            var (service, _) = CreateService();

            var result = await service.GetDoctorRatingAsync(DoctorId);

            Assert.True(result.Success);
            Assert.Equal(0, result.Data!.AverageRating);
            Assert.Equal(0, result.Data.ReviewCount);
        }

        [Fact]
        public async Task GetDoctorRatingAsync_ReturnsCorrectAverage_AndOnlyCountsThatDoctor()
        {
            var (service, uow) = CreateService();
            uow.ReviewsFake.Items.Add(new Review { Id = 1, BookingId = 1, PatientId = PatientId, DoctorId = DoctorId, Rating = 5 });
            uow.ReviewsFake.Items.Add(new Review { Id = 2, BookingId = 2, PatientId = PatientId, DoctorId = DoctorId, Rating = 3 });
            uow.ReviewsFake.Items.Add(new Review { Id = 3, BookingId = 3, PatientId = PatientId, DoctorId = "some-other-doctor", Rating = 1 });

            var result = await service.GetDoctorRatingAsync(DoctorId);

            Assert.True(result.Success);
            Assert.Equal(4, result.Data!.AverageRating);
            Assert.Equal(2, result.Data.ReviewCount);
        }

        [Fact]
        public async Task GetDoctorReviewsAsync_OnlyReturnsReviewsForThatDoctor()
        {
            var (service, uow) = CreateService();
            var patient = new ApplicationUser { Id = PatientId, FirstName = "Test", LastName = "Patient" };

            uow.ReviewsFake.Items.Add(new Review { Id = 1, BookingId = 1, PatientId = PatientId, DoctorId = DoctorId, Rating = 5, Patient = patient });
            uow.ReviewsFake.Items.Add(new Review { Id = 2, BookingId = 2, PatientId = PatientId, DoctorId = "some-other-doctor", Rating = 2, Patient = patient });

            var result = await service.GetDoctorReviewsAsync(DoctorId);

            Assert.True(result.Success);
            var reviews = result.Data!.ToList();
            Assert.Single(reviews);
            Assert.Equal(5, reviews[0].Rating);
        }
    }
}
