using AutoMapper;
using Domain;
using Domain.DTOs.DoctorDTOs;
using Domain.Enums;
using Domain.Models;
using Domain.Repositories;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Service.Tests
{
    public class DoctorServiceTests
    {
        private const string DoctorId = "doctor-1";

        private static (DoctorService service, TestUnitOfWork uow) CreateService()
        {
            var uow = new TestUnitOfWork();

            var authMock = new Mock<IUserRepository>();
            authMock.Setup(r => r.GetUserByIdAsync(DoctorId))
                .ReturnsAsync(new ApplicationUser { Id = DoctorId, FirstName = "Test", LastName = "Doctor" });
            uow.AuthRepository = authMock.Object;

            var mapperMock = new Mock<IMapper>();
            mapperMock.Setup(m => m.Map<Appointment>(It.IsAny<AppointmentDTO>()))
                .Returns((AppointmentDTO dto) => new Appointment { Price = dto.Price, Days = dto.Days });
            mapperMock.Setup(m => m.Map(It.IsAny<AppointmentDTO>(), It.IsAny<Appointment>()))
                .Returns((AppointmentDTO dto, Appointment appt) =>
                {
                    appt.Price = dto.Price;
                    appt.Days = dto.Days;
                    return appt;
                });

            var service = new DoctorService(uow, mapperMock.Object, NullLogger<DoctorService>.Instance);
            return (service, uow);
        }

        private static AppointmentDTO BuildDto(Days days, params TimeOnly[] times) => new AppointmentDTO
        {
            Price = 300,
            Days = days,
            TimeOnly = times.ToList()
        };

        [Fact]
        public async Task CreateAppointmentAsync_GeneratesFourWeeksOfSlots_PerSubmittedTime()
        {
            var (service, uow) = CreateService();
            var dto = BuildDto(Days.Saturday, new TimeOnly(9, 0), new TimeOnly(10, 30));

            var result = await service.CreateAppointmentAsync(dto, DoctorId);

            Assert.True(result.Success);
            Assert.Equal(8, uow.SlotsFake.Items.Count); // 2 times * 4 weeks
            Assert.All(uow.SlotsFake.Items, s =>
            {
                Assert.Equal(DoctorId, s.DoctorId);
                Assert.Equal(300, s.Price);
                Assert.Equal(SlotStatus.Available, s.Status);
            });
        }

        [Fact]
        public async Task CreateAppointmentAsync_GeneratedDates_AreOnRequestedWeekday_AndSevenDaysApart()
        {
            var (service, uow) = CreateService();
            var dto = BuildDto(Days.Saturday, new TimeOnly(9, 0));

            await service.CreateAppointmentAsync(dto, DoctorId);

            var dates = uow.SlotsFake.Items.Select(s => s.Date).Distinct().OrderBy(d => d).ToList();

            Assert.Equal(4, dates.Count);
            Assert.All(dates, d => Assert.Equal(DayOfWeek.Saturday, d.DayOfWeek));
            Assert.True(dates[0] >= DateOnly.FromDateTime(DateTime.Now));

            for (int i = 1; i < dates.Count; i++)
                Assert.Equal(7, dates[i].DayNumber - dates[i - 1].DayNumber);
        }

        [Fact]
        public async Task CreateAppointmentAsync_ReturnsConflict_WhenAppointmentAlreadyExistsForThatWeekday()
        {
            var (service, uow) = CreateService();
            var doctor = new ApplicationUser { Id = DoctorId, FirstName = "Test", LastName = "Doctor" };
            uow.AppointmentsFake.Items.Add(new Appointment { Id = 1, Doctor = doctor, Days = Days.Saturday, Price = 100, Time = new List<DayTime>() });

            var dto = BuildDto(Days.Saturday, new TimeOnly(9, 0));

            var result = await service.CreateAppointmentAsync(dto, DoctorId);

            Assert.False(result.Success);
            Assert.Equal(ErrorType.Conflict, result.ErrorType);
            Assert.Empty(uow.SlotsFake.Items);
        }

        [Fact]
        public async Task DeleteAppointmentAsync_ReturnsConflict_WhenUpcomingBookedSlotExists()
        {
            var (service, uow) = CreateService();
            var doctor = new ApplicationUser { Id = DoctorId, FirstName = "Test", LastName = "Doctor" };
            var appointment = new Appointment { Id = 1, Doctor = doctor, Days = Days.Saturday, Price = 100 };
            uow.AppointmentsFake.Items.Add(appointment);

            uow.SlotsFake.Items.Add(new AppointmentSlot
            {
                Id = 1,
                DoctorId = DoctorId,
                SourceAppointmentId = appointment.Id,
                Status = SlotStatus.Booked,
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(7)),
                Time = new TimeOnly(9, 0)
            });

            var result = await service.DeleteAppointmentAsync(appointment.Id, DoctorId);

            Assert.False(result.Success);
            Assert.Equal(ErrorType.Conflict, result.ErrorType);
            Assert.Contains(appointment, uow.AppointmentsFake.Items);
        }

        [Fact]
        public async Task DeleteAppointmentAsync_Succeeds_WhenOnlyPastBookedSlotsExist()
        {
            var (service, uow) = CreateService();
            var doctor = new ApplicationUser { Id = DoctorId, FirstName = "Test", LastName = "Doctor" };
            var appointment = new Appointment { Id = 1, Doctor = doctor, Days = Days.Saturday, Price = 100 };
            uow.AppointmentsFake.Items.Add(appointment);

            uow.SlotsFake.Items.Add(new AppointmentSlot
            {
                Id = 1,
                DoctorId = DoctorId,
                SourceAppointmentId = appointment.Id,
                Status = SlotStatus.Booked,
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(-7)),
                Time = new TimeOnly(9, 0)
            });

            var result = await service.DeleteAppointmentAsync(appointment.Id, DoctorId);

            Assert.True(result.Success);
            Assert.DoesNotContain(appointment, uow.AppointmentsFake.Items);
        }

        [Fact]
        public async Task ConfirmBookingAsync_Succeeds_FromPending()
        {
            var (service, uow) = CreateService();

            var slot = new AppointmentSlot { Id = 1, DoctorId = DoctorId, Status = SlotStatus.Booked, Date = DateOnly.FromDateTime(DateTime.Now.AddDays(1)), Time = new TimeOnly(9, 0) };
            var request = new Request { Id = 1, RequestState = RequestState.Pending };
            var booking = new Booking { Id = 1, Slot = slot, Request = request };
            uow.BookingsFake.Items.Add(booking);

            var result = await service.ConfirmBookingAsync(DoctorId, booking.Id);

            Assert.True(result.Success);
            Assert.Equal(RequestState.Confirmed, request.RequestState);
        }

        [Fact]
        public async Task ConfirmBookingAsync_ReturnsConflict_WhenAlreadyConfirmed()
        {
            var (service, uow) = CreateService();

            var slot = new AppointmentSlot { Id = 1, DoctorId = DoctorId, Status = SlotStatus.Booked, Date = DateOnly.FromDateTime(DateTime.Now.AddDays(1)), Time = new TimeOnly(9, 0) };
            var request = new Request { Id = 1, RequestState = RequestState.Confirmed };
            var booking = new Booking { Id = 1, Slot = slot, Request = request };
            uow.BookingsFake.Items.Add(booking);

            var result = await service.ConfirmBookingAsync(DoctorId, booking.Id);

            Assert.False(result.Success);
            Assert.Equal(ErrorType.Conflict, result.ErrorType);
            Assert.Equal(RequestState.Confirmed, request.RequestState);
        }

        [Fact]
        public async Task ConfirmBookingAsync_ReturnsNotFound_WhenSlotBelongsToDifferentDoctor()
        {
            var (service, uow) = CreateService();

            var slot = new AppointmentSlot { Id = 1, DoctorId = "someone-else", Status = SlotStatus.Booked, Date = DateOnly.FromDateTime(DateTime.Now), Time = new TimeOnly(9, 0) };
            var request = new Request { Id = 1, RequestState = RequestState.Pending };
            var booking = new Booking { Id = 1, Slot = slot, Request = request };
            uow.BookingsFake.Items.Add(booking);

            var result = await service.ConfirmBookingAsync(DoctorId, booking.Id);

            Assert.False(result.Success);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal(RequestState.Pending, request.RequestState);
        }

        [Fact]
        public async Task CompleteBookingAsync_ReturnsConflict_WhenNotYetConfirmed()
        {
            var (service, uow) = CreateService();

            var slot = new AppointmentSlot { Id = 1, DoctorId = DoctorId, Status = SlotStatus.Booked, Date = DateOnly.FromDateTime(DateTime.Now), Time = new TimeOnly(9, 0) };
            var request = new Request { Id = 1, RequestState = RequestState.Pending };
            var booking = new Booking { Id = 1, Slot = slot, Request = request };
            uow.BookingsFake.Items.Add(booking);

            var result = await service.CompleteBookingAsync(DoctorId, booking.Id);

            Assert.False(result.Success);
            Assert.Equal(ErrorType.Conflict, result.ErrorType);
            Assert.Equal(RequestState.Pending, request.RequestState);
        }

        [Fact]
        public async Task CompleteBookingAsync_Succeeds_AndKeepsBookingRow_WhenAlreadyConfirmed()
        {
            var (service, uow) = CreateService();

            var slot = new AppointmentSlot { Id = 1, DoctorId = DoctorId, Status = SlotStatus.Booked, Date = DateOnly.FromDateTime(DateTime.Now), Time = new TimeOnly(9, 0) };
            var request = new Request { Id = 1, RequestState = RequestState.Confirmed };
            var booking = new Booking { Id = 1, Slot = slot, Request = request };
            uow.BookingsFake.Items.Add(booking);

            var result = await service.CompleteBookingAsync(DoctorId, booking.Id);

            Assert.True(result.Success);
            Assert.Equal(RequestState.Completed, request.RequestState);
            Assert.Contains(booking, uow.BookingsFake.Items); // never deleted
        }

        [Fact]
        public async Task CompleteBookingAsync_ReturnsNotFound_WhenSlotBelongsToDifferentDoctor()
        {
            var (service, uow) = CreateService();

            var slot = new AppointmentSlot { Id = 1, DoctorId = "someone-else", Status = SlotStatus.Booked, Date = DateOnly.FromDateTime(DateTime.Now), Time = new TimeOnly(9, 0) };
            var request = new Request { Id = 1, RequestState = RequestState.Confirmed };
            var booking = new Booking { Id = 1, Slot = slot, Request = request };
            uow.BookingsFake.Items.Add(booking);

            var result = await service.CompleteBookingAsync(DoctorId, booking.Id);

            Assert.False(result.Success);
            Assert.Equal(ErrorType.NotFound, result.ErrorType);
            Assert.Equal(RequestState.Confirmed, request.RequestState);
        }

        [Fact]
        public async Task DoctorCancelBookingAsync_FreesFutureSlot_AndSetsCancelled()
        {
            var (service, uow) = CreateService();

            var slot = new AppointmentSlot { Id = 1, DoctorId = DoctorId, Status = SlotStatus.Booked, Date = DateOnly.FromDateTime(DateTime.Now.AddDays(1)), Time = new TimeOnly(9, 0) };
            var request = new Request { Id = 1, RequestState = RequestState.Confirmed };
            var booking = new Booking { Id = 1, Slot = slot, Request = request };
            uow.BookingsFake.Items.Add(booking);

            var result = await service.CancelBookingAsync(DoctorId, booking.Id);

            Assert.True(result.Success);
            Assert.Equal(RequestState.Cancelled, request.RequestState);
            Assert.Equal(SlotStatus.Available, slot.Status);
        }

        [Fact]
        public async Task DoctorCancelBookingAsync_ReturnsConflict_WhenAlreadyCompleted()
        {
            var (service, uow) = CreateService();

            var slot = new AppointmentSlot { Id = 1, DoctorId = DoctorId, Status = SlotStatus.Booked, Date = DateOnly.FromDateTime(DateTime.Now.AddDays(-1)), Time = new TimeOnly(9, 0) };
            var request = new Request { Id = 1, RequestState = RequestState.Completed };
            var booking = new Booking { Id = 1, Slot = slot, Request = request };
            uow.BookingsFake.Items.Add(booking);

            var result = await service.CancelBookingAsync(DoctorId, booking.Id);

            Assert.False(result.Success);
            Assert.Equal(ErrorType.Conflict, result.ErrorType);
            Assert.Equal(RequestState.Completed, request.RequestState);
            Assert.Equal(SlotStatus.Booked, slot.Status); // untouched
        }
    }
}
