using AutoMapper;
using Domain.Models;
using Microsoft.Extensions.Logging;
using Domain;
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
using Domain.DTOs.PatientDTOs;
using Domain.Enums;
using static Azure.Core.HttpHeader;

namespace Service
{
    public class PatientService : IPatientService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IMapper mapper;
        private readonly IImageService imageService;
        private readonly ILogger<PatientService> logger;

        public PatientService(IUnitOfWork unitOfWork, IMapper mapper, IImageService imageService, ILogger<PatientService> logger)
        {
            this.unitOfWork = unitOfWork;
            this.mapper = mapper;
            this.imageService = imageService;
            this.logger = logger;
        }

        public async Task<ResponseModel<BookingDTO>> BookAppointmentAsync(string patientId, int slotId)
        {
            var patient = await unitOfWork.AuthRepository.GetUserByIdAsync(patientId);

            var slot = await unitOfWork.Slots.GetByIdAsync(slotId);

            if (slot is null)
                return new ResponseModel<BookingDTO> { Message = "No such slot.", ErrorType = ErrorType.NotFound };

            if (slot.Date.ToDateTime(slot.Time) < DateTime.Now)
                return new ResponseModel<BookingDTO> { Message = "Can't book a slot in the past.", ErrorType = ErrorType.ValidationError };

            if (slot.Status != SlotStatus.Available)
                return new ResponseModel<BookingDTO> { Message = "There's an appointment at this time", ErrorType = ErrorType.Conflict };

            var request = new Request
            {
                RequestState = RequestState.Pending
            };

            Booking booking = new Booking
            {
                Patient = patient,
                Slot = slot,
                Request = request
            };

            slot.Status = SlotStatus.Booked;

            await unitOfWork.Bookings.CreateAsync(booking);

            patient.Requests.Add(request);

            try
            {
                unitOfWork.Complete();
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to book slot {SlotId} for patient {PatientId}", slotId, patientId);
                return new ResponseModel<BookingDTO> { Message = "Something went wrong.", ErrorType = ErrorType.Unexpected };
            }

            logger.LogInformation("Patient {PatientId} booked slot {SlotId}", patientId, slotId);

            var bookingDTO = new BookingDTO
            {
                SlotId = slotId,
                Date = slot.Date,
                Time = slot.Time,
                RequestState = booking.Request.RequestState,
                DoctorName = slot.Doctor.FirstName + " " + slot.Doctor.LastName,
                Price = slot.Price,
                FinalPrice = slot.Price
            };

            return new ResponseModel<BookingDTO> { Message = "Appointment is booked successfully.", Success = true, Data = bookingDTO };
        }

        public async Task<ResponseModel<IEnumerable<AppointmentSlotDTO>>> GetAvailableSlotsAsync(string doctorId)
        {
            var today = DateOnly.FromDateTime(DateTime.Now);

            var slots = await unitOfWork.Slots.GetAllByPropertyAsync(
                s => s.DoctorId == doctorId && s.Status == SlotStatus.Available && s.Date >= today);

            var orderedSlots = slots.OrderBy(s => s.Date).ThenBy(s => s.Time);

            return new ResponseModel<IEnumerable<AppointmentSlotDTO>> { Success = true, Message = "Available slots retrieved.", Data = mapper.Map<IEnumerable<AppointmentSlotDTO>>(orderedSlots) };
        }
        public async Task<ResponseModel<IEnumerable<AllDoctorsDTO>>> GetAllAppointmentsAsync(string search, int page = 1, int pageSize = 5)
        {
            var doctors = await unitOfWork.AuthRepository.GetUsersInRole("Doctor", search, page, pageSize);

            foreach (var doctor in doctors)
                doctor.Image = imageService.GenerateUrl(doctor.Image);

            var doctorsAppointments = mapper.Map<IEnumerable<AllDoctorsDTO>>(doctors);

            var meta = new Metadata
            {
                Page = page,
                PageSize = pageSize,
                Next = page + 1,
                Previous = page - 1
            };

            return new ResponseModel<IEnumerable<AllDoctorsDTO>> { Message = "Doctors and thier appointments are retrieved.", Success = true, Data = doctorsAppointments, MetaData = meta };
        }
        public async Task<ResponseModel<IEnumerable<DoctorSearchResultDTO>>> SearchDoctorsAsync(
            string? search, int? specializationId, int? minPrice, int? maxPrice, double? minRating,
            string? sortBy, int page = 1, int pageSize = 5)
        {
            var (results, totalCount) = await unitOfWork.AuthRepository.SearchDoctorsAsync(
                search, specializationId, minPrice, maxPrice, minRating, sortBy, page, pageSize);

            var doctorDTOs = results.Select(r => new DoctorSearchResultDTO
            {
                Id = r.Doctor.Id,
                FirstName = r.Doctor.FirstName,
                LastName = r.Doctor.LastName,
                Specialization = r.Doctor.Specialize?.Name,
                Image = imageService.GenerateUrl(r.Doctor.Image),
                AverageRating = r.AverageRating.HasValue ? Math.Round(r.AverageRating.Value, 2) : 0,
                ReviewCount = r.ReviewCount,
                MinPrice = r.MinPrice,
                MaxPrice = r.MaxPrice
            });

            var meta = new Metadata
            {
                Page = page,
                PageSize = pageSize,
                Next = page + 1,
                Previous = page - 1,
                TotalCount = totalCount
            };

            return new ResponseModel<IEnumerable<DoctorSearchResultDTO>> { Success = true, Message = "Doctors retrieved.", Data = doctorDTOs, MetaData = meta };
        }
        public async Task<ResponseModel<IEnumerable<BookingDTO>>> GetAllBookingsAsync(string patientId, int page = 1, int pageSize = 5)
        {
            var bookings = await unitOfWork.Bookings.GetAllPaginatedFilteredAsync(b => b.Patient.Id == patientId, page, pageSize);

            var meta = new Metadata
            {
                Page = page,
                PageSize = pageSize,
                Next = page + 1,
                Previous = page - 1
            };

            return new ResponseModel<IEnumerable<BookingDTO>> { Message = "Bookings retrieved", Success = true, Data = mapper.Map<IEnumerable<BookingDTO>>(bookings), MetaData = meta };
        }
        public async Task<ResponseModel<Booking>> CancelBookingAsync(string patientId, int bookingId)
        {
            var patient = await unitOfWork.AuthRepository.GetUserByIdAsync(patientId);

            var booking = await unitOfWork.Bookings.GetByIdAsync(bookingId);

            if (booking is null)
                return new ResponseModel<Booking> { Message = "No such booking with that ID", ErrorType = ErrorType.NotFound };

            if (!patient.Bookings.Any(b => b.Id == booking.Id))
                return new ResponseModel<Booking> { Message = "No such booking with that ID", ErrorType = ErrorType.NotFound };

            var request = booking.Request;
            var slot = booking.Slot;

            if (!BookingTransitions.IsAllowed(request.RequestState, RequestState.Cancelled, BookingTransitions.Actor.Patient))
                return new ResponseModel<Booking> { Message = $"Booking can't be cancelled from its current state ({request.RequestState}).", ErrorType = ErrorType.Conflict };

            if (slot.Date.ToDateTime(slot.Time) >= DateTime.Now)
                slot.Status = SlotStatus.Available;

            try
            {
                request.RequestState = RequestState.Cancelled;
                unitOfWork.Complete();
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to cancel booking {BookingId} for patient {PatientId}", bookingId, patientId);
                return new ResponseModel<Booking> { Message = "Something went wrong", ErrorType = ErrorType.Unexpected };
            }

            logger.LogInformation("Patient {PatientId} cancelled booking {BookingId}", patientId, bookingId);

            return new ResponseModel<Booking> { Success = true, Message = "Booking canceled", Data = booking };
        }
        public async Task<ResponseModel<BookingDTO>> FinalPriceAsync(int codeId, ApplicationUser patient, string patientId, Booking booking, DayTime time)
        {

            if (codeId != 0)
            {
                var code = await unitOfWork.DiscountCodes.GetByIdAsync(codeId);

                if (code is null || !code.IsActive)
                {
                    return new ResponseModel<BookingDTO> { Message = "No such code available.", ErrorType = ErrorType.NotFound };
                }

                if (code.Patients.Where(p => p.Id == patientId).Count() == 0)
                {
                    return new ResponseModel<BookingDTO> { Message = "No discount code is rewarded", ErrorType = ErrorType.Forbidden };
                }

                var expiredCodes = await unitOfWork.ExpiredCodes.GetAllByPropertyAsync(u => u.PatientId == patientId);

                foreach (var expiredCode in expiredCodes)
                {
                    if (expiredCode.DiscountCode == code)
                    {
                        return new ResponseModel<BookingDTO> { Message = "Code expired", ErrorType = ErrorType.Conflict };
                    }
                }

                if (code.DiscountType == DiscountType.Value)
                {
                    booking.FinalPrice = time.Appointment.Price - code.Discount;
                }
                else
                {
                    booking.FinalPrice = time.Appointment.Price * (code.Discount / 100);
                }

                foreach (var patientRequest in patient.Requests)
                {
                    patientRequest.IsDiscountUsed = true;
                }

                await unitOfWork.ExpiredCodes.CreateAsync(new ExpiredCode
                {
                    PatientId = patientId,
                    DiscountCode = code
                });
            }
            else
            {
                var codes = await unitOfWork.DiscountCodes.GetAllAsync();

                foreach (var availableCodes in codes)
                {
                    if (availableCodes.BookingsNumber == patient.Requests.Where(r => r.RequestState == RequestState.Completed && !r.IsDiscountUsed).Count())
                    {
                        availableCodes.Patients.Add(patient);
                    }
                }
                booking.FinalPrice = time.Appointment.Price;
            }

            await unitOfWork.Bookings.CreateAsync(booking);
            try
            {
                unitOfWork.Complete();
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to finalize priced booking for patient {PatientId}", patientId);
                return new ResponseModel<BookingDTO> { Message = "Something went wrong.", ErrorType = ErrorType.Unexpected };
            }

            var bookingDTO = new BookingDTO
            {
                Price = time.Appointment.Price,
                FinalPrice = booking.FinalPrice,
                Id = booking.Id,
                RequestState = booking.Request.RequestState,
                DoctorName = time.Appointment.Doctor.FirstName + " " + time.Appointment.Doctor.LastName,
            };

            return new ResponseModel<BookingDTO> { Message = "Appointment is booked successfully", Success = true, Data = bookingDTO };
        }
    }
}
