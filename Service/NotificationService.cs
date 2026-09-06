using Domain.Models;
using Domain.Notifications;
using Domain.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Service
{
    // Log/console-based notification sender for now (no real email/SMS provider is wired in -
    // that was a deliberate call, not an oversight; see the project's phase notes). Both the
    // patient and the doctor are notified on every event, per how this project defines the
    // feature. Every public method builds the two messages for an event and hands them to
    // SendAsync, which is the ONLY method that knows how a notification actually gets delivered.
    // Swapping this for a real provider (SendGrid, SES, Twilio, ...) later means replacing the
    // body of SendAsync only - every call site in PatientService/DoctorService, and everything
    // above SendAsync in this file, stays untouched.
    public class NotificationService : INotificationService
    {
        private readonly ILogger<NotificationService> logger;

        public NotificationService(ILogger<NotificationService> logger)
        {
            this.logger = logger;
        }

        public Task NotifyBookingCreatedAsync(Booking booking)
        {
            var doctor = booking.Slot.Doctor;
            var patient = booking.Patient;
            var when = FormatWhen(booking);

            return SendToBothAsync(
                patient, doctor,
                patientSubject: "Appointment requested",
                patientBody: $"Your appointment with Dr. {doctor.FirstName} {doctor.LastName} on {when} has been requested and is pending confirmation.",
                doctorSubject: "New booking request",
                doctorBody: $"{patient.FirstName} {patient.LastName} requested an appointment with you on {when}.");
        }

        public Task NotifyBookingConfirmedAsync(Booking booking)
        {
            var doctor = booking.Slot.Doctor;
            var patient = booking.Patient;
            var when = FormatWhen(booking);

            return SendToBothAsync(
                patient, doctor,
                patientSubject: "Appointment confirmed",
                patientBody: $"Dr. {doctor.FirstName} {doctor.LastName} confirmed your appointment on {when}.",
                doctorSubject: "Booking confirmed",
                doctorBody: $"You confirmed the appointment with {patient.FirstName} {patient.LastName} on {when}.");
        }

        public Task NotifyBookingCancelledAsync(Booking booking, string cancelledByRole)
        {
            var doctor = booking.Slot.Doctor;
            var patient = booking.Patient;
            var when = FormatWhen(booking);
            var by = cancelledByRole == "Doctor"
                ? $"Dr. {doctor.FirstName} {doctor.LastName}"
                : $"{patient.FirstName} {patient.LastName}";

            return SendToBothAsync(
                patient, doctor,
                patientSubject: "Appointment cancelled",
                patientBody: $"Your appointment on {when} was cancelled by {by}.",
                doctorSubject: "Booking cancelled",
                doctorBody: $"The appointment on {when} was cancelled by {by}.");
        }

        public Task NotifyBookingCompletedAsync(Booking booking)
        {
            var doctor = booking.Slot.Doctor;
            var patient = booking.Patient;
            var when = FormatWhen(booking);

            return SendToBothAsync(
                patient, doctor,
                patientSubject: "Appointment completed",
                patientBody: $"Your appointment with Dr. {doctor.FirstName} {doctor.LastName} on {when} is marked completed. You can now leave a review.",
                doctorSubject: "Booking completed",
                doctorBody: $"You marked the appointment with {patient.FirstName} {patient.LastName} on {when} as completed.");
        }

        private static string FormatWhen(Booking booking) =>
            $"{booking.Slot.Date:yyyy-MM-dd} at {booking.Slot.Time:HH:mm}";

        public Task NotifyBookingReminderAsync(Booking booking)
        {
            var doctor = booking.Slot.Doctor;
            var patient = booking.Patient;
            var when = FormatWhen(booking);

            return SendToBothAsync(
                patient, doctor,
                patientSubject: "Appointment reminder",
                patientBody: $"Reminder: your appointment with Dr. {doctor.FirstName} {doctor.LastName} is on {when} (within 24 hours).",
                doctorSubject: "Upcoming appointment reminder",
                doctorBody: $"Reminder: you have an appointment with {patient.FirstName} {patient.LastName} on {when} (within 24 hours).");
        }

        private async Task SendToBothAsync(
            ApplicationUser patient, ApplicationUser doctor,
            string patientSubject, string patientBody,
            string doctorSubject, string doctorBody)
        {
            await SendAsync(new NotificationMessage
            {
                ToEmail = patient.Email ?? string.Empty,
                ToName = $"{patient.FirstName} {patient.LastName}",
                Subject = patientSubject,
                Body = patientBody
            });

            await SendAsync(new NotificationMessage
            {
                ToEmail = doctor.Email ?? string.Empty,
                ToName = $"{doctor.FirstName} {doctor.LastName}",
                Subject = doctorSubject,
                Body = doctorBody
            });
        }

        // The single delivery point. Today this just logs at Information - swap the body for a
        // real provider call when one exists, without touching anything above it in this file.
        private Task SendAsync(NotificationMessage message)
        {
            logger.LogInformation(
                "Notification -> {ToName} <{ToEmail}> | {Subject} | {Body}",
                message.ToName, message.ToEmail, message.Subject, message.Body);

            return Task.CompletedTask;
        }
    }
}
