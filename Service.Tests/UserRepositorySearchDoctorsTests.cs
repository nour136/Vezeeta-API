using Domain.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Repository;
using Repository.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Service.Tests
{
    public class UserRepositorySearchDoctorsTests : IDisposable
    {
        private readonly ApplicationDbContext context;
        private readonly UserRepository repository;

        private const string DoctorRoleId = "role-doctor";
        private const string PatientRoleId = "role-patient";

        public UserRepositorySearchDoctorsTests()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            context = new ApplicationDbContext(options);
            repository = new UserRepository(context, userManager: null!, NullLogger<UserRepository>.Instance);

            SeedRolesAndSpecializations();
        }

        public void Dispose() => context.Dispose();

        private void SeedRolesAndSpecializations()
        {
            context.Roles.Add(new IdentityRole { Id = DoctorRoleId, Name = "Doctor", NormalizedName = "DOCTOR" });
            context.Roles.Add(new IdentityRole { Id = PatientRoleId, Name = "Patient", NormalizedName = "PATIENT" });
            cardiology = new Specialization { Id = 1, Name = "Cardiology", Doctors = new List<ApplicationUser?>() };
            dermatology = new Specialization { Id = 2, Name = "Dermatology", Doctors = new List<ApplicationUser?>() };
            context.Specializations.Add(cardiology);
            context.Specializations.Add(dermatology);
            context.SaveChanges();
        }

        private Specialization cardiology = null!;
        private Specialization dermatology = null!;

        private ApplicationUser AddDoctor(string id, string firstName, Specialization? specialization = null)
        {
            var user = new ApplicationUser
            {
                Id = id,
                UserName = id,
                FirstName = firstName,
                LastName = "Doctor",
                Phone = "0100000000",
                Specialize = specialization
            };
            context.Users.Add(user);
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = id, RoleId = DoctorRoleId });
            return user;
        }

        private void AddAppointment(ApplicationUser doctor, int price)
        {
            context.Appointments.Add(new Appointment { Doctor = doctor, Price = price, Time = new List<DayTime>() });
        }

        private void AddReview(string doctorId, int rating)
        {
            context.Reviews.Add(new Review
            {
                DoctorId = doctorId,
                PatientId = "some-patient",
                BookingId = context.Reviews.Count() + 1000,
                Rating = rating
            });
        }

        [Fact]
        public async Task SearchDoctorsAsync_OnlyReturnsUsersInDoctorRole()
        {
            AddDoctor("d1", "Alice");
            var patient = new ApplicationUser { Id = "p1", UserName = "p1", FirstName = "Bob", LastName = "Patient", Phone = "0100000001" };
            context.Users.Add(patient);
            context.UserRoles.Add(new IdentityUserRole<string> { UserId = "p1", RoleId = PatientRoleId });
            await context.SaveChangesAsync();

            var (results, totalCount) = await repository.SearchDoctorsAsync(null, null, null, null, null, null, 1, 10);

            Assert.Equal(1, totalCount);
            Assert.Single(results);
            Assert.Equal("d1", results.First().Doctor.Id);
        }

        [Fact]
        public async Task SearchDoctorsAsync_FiltersBySpecialization()
        {
            AddDoctor("d1", "Alice", cardiology);
            AddDoctor("d2", "Bob", dermatology);
            await context.SaveChangesAsync();

            var (results, totalCount) = await repository.SearchDoctorsAsync(null, specializationId: cardiology.Id, null, null, null, null, 1, 10);

            Assert.Equal(1, totalCount);
            Assert.Equal("d1", results.Single().Doctor.Id);
        }

        [Fact]
        public async Task SearchDoctorsAsync_FiltersByPriceRange_MatchingAnyAppointment()
        {
            var d1 = AddDoctor("d1", "Alice");
            AddAppointment(d1, price: 100);
            AddAppointment(d1, price: 500);

            var d2 = AddDoctor("d2", "Bob");
            AddAppointment(d2, price: 900);

            await context.SaveChangesAsync();

            var (results, totalCount) = await repository.SearchDoctorsAsync(null, null, minPrice: 50, maxPrice: 200, null, null, 1, 10);

            Assert.Equal(1, totalCount);
            Assert.Equal("d1", results.Single().Doctor.Id);
        }

        [Fact]
        public async Task SearchDoctorsAsync_MinRating_ExcludesDoctorsWithNoReviews()
        {
            AddDoctor("d1", "Alice");
            AddReview("d1", 5);

            AddDoctor("d2", "Bob");

            await context.SaveChangesAsync();

            var (results, totalCount) = await repository.SearchDoctorsAsync(null, null, null, null, minRating: 3, null, 1, 10);

            Assert.Equal(1, totalCount);
            Assert.Equal("d1", results.Single().Doctor.Id);
        }

        [Fact]
        public async Task SearchDoctorsAsync_MinRating_FiltersOnAverage_NotAnySingleReview()
        {
            AddDoctor("d1", "Alice");
            AddReview("d1", 5);
            AddReview("d1", 1);

            await context.SaveChangesAsync();

            var (resultsAtFour, _) = await repository.SearchDoctorsAsync(null, null, null, null, minRating: 4, null, 1, 10);
            var (resultsAtThree, _) = await repository.SearchDoctorsAsync(null, null, null, null, minRating: 3, null, 1, 10);

            Assert.Empty(resultsAtFour);
            Assert.Single(resultsAtThree);
        }

        [Fact]
        public async Task SearchDoctorsAsync_ReturnsComputedAggregates_NotJustTheDoctor()
        {
            var d1 = AddDoctor("d1", "Alice");
            AddAppointment(d1, price: 100);
            AddAppointment(d1, price: 300);
            AddReview("d1", 4);
            AddReview("d1", 2);

            await context.SaveChangesAsync();

            var (results, _) = await repository.SearchDoctorsAsync(null, null, null, null, null, null, 1, 10);

            var result = results.Single();
            Assert.Equal(3.0, result.AverageRating);
            Assert.Equal(2, result.ReviewCount);
            Assert.Equal(100, result.MinPrice);
            Assert.Equal(300, result.MaxPrice);
        }

        [Fact]
        public async Task SearchDoctorsAsync_SortByPriceAsc_OrdersByLowestTemplatePrice()
        {
            var d1 = AddDoctor("d1", "Alice");
            AddAppointment(d1, price: 500);

            var d2 = AddDoctor("d2", "Bob");
            AddAppointment(d2, price: 100);

            await context.SaveChangesAsync();

            var (results, _) = await repository.SearchDoctorsAsync(null, null, null, null, null, sortBy: "price_asc", 1, 10);

            Assert.Equal(new[] { "d2", "d1" }, results.Select(r => r.Doctor.Id));
        }

        [Fact]
        public async Task SearchDoctorsAsync_SortByRatingDesc_OrdersHighestFirst_UnratedLast()
        {
            AddDoctor("d1", "Alice");
            AddReview("d1", 2);

            AddDoctor("d2", "Bob");
            AddReview("d2", 5);

            AddDoctor("d3", "Carol");

            await context.SaveChangesAsync();

            var (results, _) = await repository.SearchDoctorsAsync(null, null, null, null, null, sortBy: "rating_desc", 1, 10);

            Assert.Equal(new[] { "d2", "d1", "d3" }, results.Select(r => r.Doctor.Id));
        }

        [Fact]
        public async Task SearchDoctorsAsync_TotalCount_ReflectsFilteredSet_NotJustCurrentPage()
        {
            for (int i = 0; i < 7; i++)
                AddDoctor($"d{i}", $"Doctor{i}");

            await context.SaveChangesAsync();

            var (results, totalCount) = await repository.SearchDoctorsAsync(null, null, null, null, null, null, page: 1, pageSize: 3);

            Assert.Equal(3, results.Count());
            Assert.Equal(7, totalCount);
        }
    }
}
