using Domain.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repository
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AppointmentSlot>()
                .HasOne(s => s.SourceAppointment)
                .WithMany()
                .HasForeignKey(s => s.SourceAppointmentId)
                .OnDelete(DeleteBehavior.SetNull);

            modelBuilder.Entity<AppointmentSlot>()
                .HasIndex(s => new { s.DoctorId, s.Date, s.Time })
                .IsUnique();

            modelBuilder.Entity<Booking>()
                .HasIndex(b => b.SlotId)
                .IsUnique();

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Booking)
                .WithMany()
                .HasForeignKey(r => r.BookingId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Patient)
                .WithMany()
                .HasForeignKey(r => r.PatientId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasOne(r => r.Doctor)
                .WithMany()
                .HasForeignKey(r => r.DoctorId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Review>()
                .HasIndex(r => r.BookingId)
                .IsUnique();

            modelBuilder.Entity<Review>()
                .ToTable(t => t.HasCheckConstraint("CK_Review_Rating", "[Rating] BETWEEN 1 AND 5"));
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseLazyLoadingProxies();

            base.OnConfiguring(optionsBuilder);
        }

        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<Booking> Bookings { get; set; }
        public DbSet<DayTime> Times { get; set; }
        public DbSet<AppointmentSlot> Slots { get; set; }
        public DbSet<Request> Requests { get; set; }
        public DbSet<Specialization> Specializations { get; set; }
        public DbSet<DiscountCode> Discounts { get; set; }
        public DbSet<ExpiredCode> ExpiredCodes { get; set; }
        public DbSet<Review> Reviews { get; set; }
    }
}
