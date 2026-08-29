using AutoMapper;
using Domain;
using Microsoft.Extensions.Logging;
using Domain.DTOs.DoctorDTOs;
using Domain.Enums;
using Domain.Models;
using Domain.Repositories;
using Domain.Services;
using Domain.Utilities;
using Microsoft.EntityFrameworkCore;
using Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Service
{
    public class DoctorService : IDoctorService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IMapper mapper;
        private readonly ILogger<DoctorService> logger;

        public DoctorService(IUnitOfWork unitOfWork, IMapper mapper, ILogger<DoctorService> logger)
        {
            this.unitOfWork = unitOfWork;
            this.mapper = mapper;
            this.logger = logger;
        }

        public async Task<ResponseModel<IEnumerable<AppointmentDTO>>> GetAppointmentsAsync(string doctorId, int page = 1, int pageSize = 5)
        {
            var appointments = await unitOfWork.Appointments.GetAllPaginatedFilteredAsync(a => a.Doctor.Id == doctorId, page, pageSize);

            Metadata meta = new Metadata
            {
                Page = page,
                PageSize = pageSize,
                Next = page + 1,
                Previous = page - 1
            };

            return new ResponseModel<IEnumerable<AppointmentDTO>> { Success = true, Message = "Appointments retrieved.", Data = mapper.Map<IEnumerable<AppointmentDTO>>(appointments), MetaData = meta };
        }
        public async Task<ResponseModel<string>> ConfirmBookingAsync(string doctorId, int bookingId)
        {
            var booking = await unitOfWork.Bookings.GetByIdAsync(bookingId);

            if (booking is null)
                return new ResponseModel<string> { Message = "No such booking with that id", ErrorType = ErrorType.NotFound };

            if (booking.Slot.DoctorId != doctorId)
                return new ResponseModel<string> { Message = "No such booking with that id", ErrorType = ErrorType.NotFound };

            var request = booking.Request;

            if (!BookingTransitions.IsAllowed(request.RequestState, RequestState.Confirmed, BookingTransitions.Actor.Doctor))
                return new ResponseModel<string> { Message = $"Booking can't be confirmed from its current state ({request.RequestState}).", ErrorType = ErrorType.Conflict };

            try
            {
                request.RequestState = RequestState.Confirmed;
                unitOfWork.Complete();
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to confirm booking {BookingId} for doctor {DoctorId}", bookingId, doctorId);
                return new ResponseModel<string> { Message = "Something went wrong.", ErrorType = ErrorType.Unexpected };
            }

            logger.LogInformation("Doctor {DoctorId} confirmed booking {BookingId}", doctorId, bookingId);

            return new ResponseModel<string> { Message = "Booking confirmed", Success = true, Data = "" };
        }

        public async Task<ResponseModel<string>> CompleteBookingAsync(string doctorId, int bookingId)
        {
            var booking = await unitOfWork.Bookings.GetByIdAsync(bookingId);

            if (booking is null)
                return new ResponseModel<string> { Message = "No such booking with that id", ErrorType = ErrorType.NotFound };

            if (booking.Slot.DoctorId != doctorId)
                return new ResponseModel<string> { Message = "No such booking with that id", ErrorType = ErrorType.NotFound };

            var request = booking.Request;

            if (!BookingTransitions.IsAllowed(request.RequestState, RequestState.Completed, BookingTransitions.Actor.Doctor))
                return new ResponseModel<string> { Message = $"Booking can't be completed from its current state ({request.RequestState}). It must be Confirmed first.", ErrorType = ErrorType.Conflict };

            try
            {
                request.RequestState = RequestState.Completed;
                unitOfWork.Complete();
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to complete booking {BookingId} for doctor {DoctorId}", bookingId, doctorId);
                return new ResponseModel<string> { Message = "Something went wrong.", ErrorType = ErrorType.Unexpected };
            }

            logger.LogInformation("Doctor {DoctorId} marked booking {BookingId} as completed", doctorId, bookingId);

            return new ResponseModel<string> { Message = "Booking marked as completed", Success = true, Data = "" };
        }

        public async Task<ResponseModel<string>> CancelBookingAsync(string doctorId, int bookingId)
        {
            var booking = await unitOfWork.Bookings.GetByIdAsync(bookingId);

            if (booking is null)
                return new ResponseModel<string> { Message = "No such booking with that id", ErrorType = ErrorType.NotFound };

            if (booking.Slot.DoctorId != doctorId)
                return new ResponseModel<string> { Message = "No such booking with that id", ErrorType = ErrorType.NotFound };

            var request = booking.Request;
            var slot = booking.Slot;

            if (!BookingTransitions.IsAllowed(request.RequestState, RequestState.Cancelled, BookingTransitions.Actor.Doctor))
                return new ResponseModel<string> { Message = $"Booking can't be cancelled from its current state ({request.RequestState}).", ErrorType = ErrorType.Conflict };

            if (slot.Date.ToDateTime(slot.Time) >= DateTime.Now)
                slot.Status = SlotStatus.Available;

            try
            {
                request.RequestState = RequestState.Cancelled;
                unitOfWork.Complete();
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to cancel booking {BookingId} for doctor {DoctorId}", bookingId, doctorId);
                return new ResponseModel<string> { Message = "Something went wrong.", ErrorType = ErrorType.Unexpected };
            }

            logger.LogInformation("Doctor {DoctorId} cancelled booking {BookingId}", doctorId, bookingId);

            return new ResponseModel<string> { Message = "Booking cancelled", Success = true, Data = "" };
        }
        public async Task<ResponseModel<Appointment>> CreateAppointmentAsync(AppointmentDTO appointmentDTO, string doctorId)
        {
            var doctor = await unitOfWork.AuthRepository.GetUserByIdAsync(doctorId);

            var appointment = mapper.Map<Appointment>(appointmentDTO);

            appointment.Doctor = doctor;

            if (appointmentDTO.TimeOnly != null)
                appointment.Time = appointmentDTO.TimeOnly.Select(t => new DayTime { Time = t }).ToList();

            var currentAppointments = await unitOfWork.Appointments.GetAllPaginatedFilteredAsync(
                a => a.Doctor.Id == doctorId && a.Days == appointmentDTO.Days, 1, 7);

            if (currentAppointments.Count() >= 1)
                return new ResponseModel<Appointment> { Message = $"There is already an appointment at {appointmentDTO.Days}", ErrorType = ErrorType.Conflict };

            try
            {
                await unitOfWork.Appointments.CreateAsync(appointment);

                await GenerateSlotsAsync(appointment, doctorId);

                unitOfWork.Complete();
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to create appointment for doctor {DoctorId}", doctorId);
                return new ResponseModel<Appointment> { Message = "Something went wrong.", ErrorType = ErrorType.Unexpected };
            }

            logger.LogInformation("Doctor {DoctorId} created an appointment on {Days} with {SlotCount} generated slots", doctorId, appointmentDTO.Days, appointment.Time?.Count * SlotGenerationWeeks ?? 0);

            return new ResponseModel<Appointment> { Success = true, Message = "New appointment is added successfully.", Data = appointment };
        }
        public async Task<ResponseModel<Appointment>> UpdateAppointmentAsync(int appointmentId, AppointmentDTO appointmentDTO, string doctorId)
        {
            var doctor = await unitOfWork.AuthRepository.GetUserByIdAsync(doctorId);

            var appointment = await unitOfWork.Appointments.GetByIdAsync(appointmentId);

            if (appointment is null || appointment.Doctor.Id != doctorId)
                return new ResponseModel<Appointment> { Message = "No appointment with that ID.", ErrorType = ErrorType.NotFound };

            mapper.Map(appointmentDTO, appointment);

            appointment.Doctor = doctor;
            appointment.Time = appointmentDTO.TimeOnly.Select(t => new DayTime { Time = t }).ToList();

            try
            {
                unitOfWork.Appointments.Update(appointment);

                await RegenerateSlotsAsync(appointment, doctorId);

                unitOfWork.Complete();
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to update appointment {AppointmentId} for doctor {DoctorId}", appointmentId, doctorId);
                return new ResponseModel<Appointment> { Message = "Something went wrong.", ErrorType = ErrorType.Unexpected };
            }

            logger.LogInformation("Doctor {DoctorId} updated appointment {AppointmentId}", doctorId, appointmentId);

            return new ResponseModel<Appointment> { Success = true, Message = "Appointment is updated successfully.", Data = appointment };
        }
        public async Task<ResponseModel<Appointment>> DeleteAppointmentAsync(int appointmentId, string doctorId)
        {
            var appointment = await unitOfWork.Appointments.GetByIdAsync(appointmentId);

            if (appointment is null)
                return new ResponseModel<Appointment> { Message = "No appointment with that ID.", ErrorType = ErrorType.NotFound };

            if (appointment.Doctor.Id != doctorId)
                return new ResponseModel<Appointment> { Message = "No appointment with that ID.", ErrorType = ErrorType.NotFound };

            var today = DateOnly.FromDateTime(DateTime.Now);
            var activeBookedSlots = await unitOfWork.Slots.GetAllByPropertyAsync(
                s => s.SourceAppointmentId == appointment.Id && s.Status == SlotStatus.Booked && s.Date >= today);

            if (activeBookedSlots.Any())
                return new ResponseModel<Appointment> { Message = "Can't be deleted, has upcoming booked appointments", ErrorType = ErrorType.Conflict };

            try
            {
                unitOfWork.Appointments.Delete(appointment);

                unitOfWork.Complete();
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to delete appointment {AppointmentId} for doctor {DoctorId}", appointmentId, doctorId);
                return new ResponseModel<Appointment> { Message = "Something went wrong.", ErrorType = ErrorType.Unexpected };
            }

            logger.LogInformation("Doctor {DoctorId} deleted appointment {AppointmentId}", doctorId, appointmentId);

            return new ResponseModel<Appointment> { Success = true, Message = "Appointment is deleted successfully.", Data = appointment };
        }

        private const int SlotGenerationWeeks = 4;

        private static readonly Dictionary<Days, DayOfWeek> DaysMap = new()
        {
            [Days.Saturday] = DayOfWeek.Saturday,
            [Days.Sunday] = DayOfWeek.Sunday,
            [Days.Monday] = DayOfWeek.Monday,
            [Days.Tuesday] = DayOfWeek.Tuesday,
            [Days.Wednesday] = DayOfWeek.Wednesday,
            [Days.Thursday] = DayOfWeek.Thursday,
            [Days.Friday] = DayOfWeek.Friday,
        };

        private List<AppointmentSlot> BuildSlotsForTemplate(Appointment appointment, string doctorId)
        {
            var slots = new List<AppointmentSlot>();

            var today = DateOnly.FromDateTime(DateTime.Now);
            var targetDayOfWeek = DaysMap[appointment.Days];

            var firstOccurrence = today;
            while (firstOccurrence.DayOfWeek != targetDayOfWeek)
                firstOccurrence = firstOccurrence.AddDays(1);

            for (int week = 0; week < SlotGenerationWeeks; week++)
            {
                var date = firstOccurrence.AddDays(week * 7);

                foreach (var dayTime in appointment.Time)
                {
                    slots.Add(new AppointmentSlot
                    {
                        Date = date,
                        Time = dayTime.Time,
                        Price = appointment.Price,
                        Status = SlotStatus.Available,
                        DoctorId = doctorId
                    });
                }
            }

            return slots;
        }

        private async Task GenerateSlotsAsync(Appointment appointment, string doctorId)
        {
            foreach (var slot in BuildSlotsForTemplate(appointment, doctorId))
            {
                slot.SourceAppointment = appointment;
                await unitOfWork.Slots.CreateAsync(slot);
            }
        }

        private async Task RegenerateSlotsAsync(Appointment appointment, string doctorId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            var staleSlots = (await unitOfWork.Slots.GetAllByPropertyAsync(
                s => s.SourceAppointmentId == appointment.Id && s.Status == SlotStatus.Available && s.Date >= today)).ToList();

            foreach (var stale in staleSlots)
                unitOfWork.Slots.Delete(stale);

            var staleIds = staleSlots.Select(s => s.Id).ToHashSet();

            var occupied = (await unitOfWork.Slots.GetAllByPropertyAsync(
                    s => s.DoctorId == doctorId && s.Date >= today))
                .Where(s => !staleIds.Contains(s.Id))
                .Select(s => (s.Date, s.Time))
                .ToHashSet();

            foreach (var slot in BuildSlotsForTemplate(appointment, doctorId))
            {
                if (occupied.Contains((slot.Date, slot.Time)))
                    continue;

                slot.SourceAppointment = appointment;
                await unitOfWork.Slots.CreateAsync(slot);
            }
        }
    }
}
