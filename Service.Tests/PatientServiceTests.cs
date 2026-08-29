using AutoMapper;
using Domain;
using Domain.DTOs.PatientDTOs;
using Domain.Enums;
using Domain.Models;
using Domain.Repositories;
using Domain.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Service.Tests
{
    public class PatientServiceTests
    {
        private const string PatientId = "patient-1";

        private static (PatientService service, TestUnitOfWork uow) CreateService()
        {
            var uow = new TestUnitOfWork();

            var authMock = new Mock<IUserRepository>();
            authMock.Setup(r => r.GetUserByIdAsync(PatientId))
                .ReturnsAsync(new ApplicationUser { Id = PatientId, FirstName = "Test", LastName = "Patient", Requests = new List<Request>(), Bookings = new List<Booking>() });
            uow.AuthRepository = authMock.Object;

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(m => m.Map<IEnumerable<AppointmentSlotDTO>>(It.IsAny<IEnumerable<AppointmentSlot>>()))
                .Returns((IEnumerable<AppointmentSlot> slots) => slots.Select(s => new AppointmentSlotDTO
                {
                    Id = s.Id,
                    Date = s.Date,
                    Time = s.Time,
                    Price = s.Price
                }));

            var imageServiceMock = new Mock<IImageService>();

            var service = new PatientService(uow, mapperMock.Object, imageServiceMock.Object, NullLogger<PatientService>.Instance);
            return (service, uow);
        }

        private static AppointmentSlot MakeSlot(int id, DateOnly date, TimeOnly time, SlotStatus status, string doctorId = "doctor-1") => new AppointmentSlot
        {
            Id = id,
            Date = date,
            Time = time,
            Status = status,
            DoctorId = doctorId,
            Price = 300,
            Doctor = new ApplicationUser { Id = doctorId, FirstName = "Test", LastName = "Doctor" }
        };

        [Fact]
        public async Task BookAppointmentAsync_ReturnsNotFound_WhenSlotDoesNotExist()
        {
            var (service, _) = CreateService();

            var result = await service.BookAppointmentAsync(PatientId, slotId: 999);

            Assert.False(result.Success);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
        }

        [Fact]
        public async Task BookAppointmentAsync_ReturnsValidationError_WhenSlotIsInThePast()
        {
            var (service, uow) = CreateService();
            var slot = MakeSlot(1, DateOnly.FromDateTime(DateTime.Now.AddDays(-1)), new TimeOnly(9, 0), SlotStatus.Available);
            uow.SlotsFake.Items.Add(slot);

            var result = await service.BookAppointmentAsync(PatientId, slot.Id);

            Assert.False(result.Success);
            Assert.Equal(ErrorType.ValidationError, result.ErrorType);
            Assert.Equal(SlotStatus.Available, slot.Status); // untouched
        }

        [Fact]
        public async Task BookAppointmentAsync_ReturnsConflict_WhenSlotAlreadyBooked()
        {
            var (service, uow) = CreateService();
            var slot = MakeSlot(1, DateOnly.FromDateTime(DateTime.Now.AddDays(1)), new TimeOnly(9, 0), SlotStatus.Booked);
            uow.SlotsFake.Items.Add(slot);

            var result = await service.BookAppointmentAsync(PatientId, slot.Id);

            Assert.False(result.Success);
            Assert.Equal(ErrorType.Conflict, result.ErrorType);
        }

        [Fact]
        public async Task BookAppointmentAsync_Succeeds_AndMarksSlotBooked()
        {
            var (service, uow) = CreateService();
            var slot = MakeSlot(1, DateOnly.FromDateTime(DateTime.Now.AddDays(1)), new TimeOnly(9, 0), SlotStatus.Available);
            uow.SlotsFake.Items.Add(slot);

            var result = await service.BookAppointmentAsync(PatientId, slot.Id);

            Assert.True(result.Success);
            Assert.Equal(SlotStatus.Booked, slot.Status);
            Assert.Single(uow.BookingsFake.Items);
            Assert.Equal(slot.Id, uow.BookingsFake.Items[0].Slot.Id);
        }

        [Fact]
        public async Task CancelBookingAsync_ReturnsNotFound_WhenBookingNotOwnedByPatient()
        {
            var (service, uow) = CreateService();
            var slot = MakeSlot(1, DateOnly.FromDateTime(DateTime.Now.AddDays(1)), new TimeOnly(9, 0), SlotStatus.Booked);
            var booking = new Booking { Id = 1, Slot = slot, Request = new Request { Id = 1, RequestState = RequestState.Pending } };
            uow.BookingsFake.Items.Add(booking);
            // Note: patient.Bookings (from CreateService's mock) is empty, so this booking isn't "theirs".

            var result = await service.CancelBookingAsync(PatientId, booking.Id);

            Assert.False(result.Success);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
        }

        [Fact]
        public async Task CancelBookingAsync_FreesSlot_WhenSlotIsStillInTheFuture()
        {
            var (service, uow) = CreateService();
            var slot = MakeSlot(1, DateOnly.FromDateTime(DateTime.Now.AddDays(1)), new TimeOnly(9, 0), SlotStatus.Booked);
            var booking = new Booking { Id = 1, Slot = slot, Request = new Request { Id = 1, RequestState = RequestState.Pending } };
            uow.BookingsFake.Items.Add(booking);

            var patient = await uow.AuthRepository.GetUserByIdAsync(PatientId);
            patient.Bookings!.Add(booking);

            var result = await service.CancelBookingAsync(PatientId, booking.Id);

            Assert.True(result.Success);
            Assert.Equal(SlotStatus.Available, slot.Status);
            Assert.Equal(RequestState.Cancelled, booking.Request.RequestState);
            Assert.Contains(booking, uow.BookingsFake.Items); // never deleted
        }

        [Fact]
        public async Task CancelBookingAsync_DoesNotFreeSlot_WhenSlotIsInThePast()
        {
            var (service, uow) = CreateService();
            var slot = MakeSlot(1, DateOnly.FromDateTime(DateTime.Now.AddDays(-1)), new TimeOnly(9, 0), SlotStatus.Booked);
            var booking = new Booking { Id = 1, Slot = slot, Request = new Request { Id = 1, RequestState = RequestState.Pending } };
            uow.BookingsFake.Items.Add(booking);

            var patient = await uow.AuthRepository.GetUserByIdAsync(PatientId);
            patient.Bookings!.Add(booking);

            var result = await service.CancelBookingAsync(PatientId, booking.Id);

            Assert.True(result.Success);
            Assert.Equal(SlotStatus.Booked, slot.Status); // a past slot has no "available" meaning to restore
            Assert.Equal(RequestState.Cancelled, booking.Request.RequestState);
        }

        [Fact]
        public async Task GetAvailableSlotsAsync_OnlyReturnsAvailableFutureSlots_OrderedByDateThenTime()
        {
            var (service, uow) = CreateService();
            var today = DateOnly.FromDateTime(DateTime.Now);

            var pastAvailable = MakeSlot(1, today.AddDays(-1), new TimeOnly(9, 0), SlotStatus.Available);
            var futureBooked = MakeSlot(2, today.AddDays(1), new TimeOnly(9, 0), SlotStatus.Booked);
            var futureLate = MakeSlot(3, today.AddDays(2), new TimeOnly(15, 0), SlotStatus.Available);
            var futureEarly = MakeSlot(4, today.AddDays(2), new TimeOnly(9, 0), SlotStatus.Available);

            uow.SlotsFake.Items.AddRange(new[] { pastAvailable, futureBooked, futureLate, futureEarly });

            var result = await service.GetAvailableSlotsAsync("doctor-1");

            Assert.True(result.Success);
            var ids = result.Data!.Select(s => s.Id).ToList();
            Assert.Equal(new[] { futureEarly.Id, futureLate.Id }, ids);
        }
    }
}
