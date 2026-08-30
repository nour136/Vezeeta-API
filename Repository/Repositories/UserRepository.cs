using Domain.Models;
using Domain;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Security.Claims;
using Domain.Repositories;
using Microsoft.EntityFrameworkCore;
using Domain.DTOs.AuthDTOs;

namespace Repository.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext context;
        private readonly UserManager<ApplicationUser> userManager;
        private readonly ILogger<UserRepository> logger;

        public UserRepository(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<UserRepository> logger)
        {
            this.context = context;
            this.userManager = userManager;
            this.logger = logger;
        }

        public async Task<ResponseModel<AuthDTO>> RegisterAsync(ApplicationUser user, string password)
        {
            var result = await userManager.CreateAsync(user, password);

            if (!result.Succeeded)
            {
                return ErrorMessage(result);
            }

            return new ResponseModel<AuthDTO> { Success = true, Message = "Account created successfully" };
        }

        public async Task<ResponseModel<AuthDTO>> AddUserToRole(ApplicationUser user, string role)
        {
            var result = await userManager.AddToRoleAsync(user, role);

            if (!result.Succeeded)
            {
                return ErrorMessage(result);
            }

            return new ResponseModel<AuthDTO> { Success = true, Message = $"{user.FirstName} is added to role {role}" };
        }

        public async Task<ResponseModel<AuthDTO>> UpdateAsync(ApplicationUser user)
        {
            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return ErrorMessage(result);
            }

            return new ResponseModel<AuthDTO> { Success = true, Message = "Updated account successfully" };
        }

        public async Task<ResponseModel<AuthDTO>> DeleteAsync(ApplicationUser user)
        {
            try
            {
                await userManager.DeleteAsync(user);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to delete user account {UserId}", user.Id);
                return new ResponseModel<AuthDTO> { Message = "Something went wrong.", ErrorType = ErrorType.Unexpected };
            }

            return new ResponseModel<AuthDTO> { Message = "Deleted successfully", Success = true };
        }

        public async Task<ApplicationUser> GetUserByEmailAsync(string email)
        {
            return await userManager.FindByEmailAsync(email);
        }

        public async Task<ApplicationUser> GetUserByIdAsync(string id)
        {
            return await userManager.FindByIdAsync(id);
        }

        public async Task<IList<Claim>> GetClaimsAsync(ApplicationUser user)
        {
            return await userManager.GetClaimsAsync(user);
        }

        public async Task<IList<string>> GetRolesAsync(ApplicationUser user)
        {
            return await userManager.GetRolesAsync(user);
        }

        private IQueryable<ApplicationUser> UsersInRoleQuery(string role)
        {
            return from user in context.Users
                   join userRole in context.UserRoles on user.Id equals userRole.UserId
                   join r in context.Roles on userRole.RoleId equals r.Id
                   where r.Name == role
                   select user;
        }

        public async Task<IEnumerable<ApplicationUser>> GetUsersInRole(string role, string? search, int page = 1, int pageSize = 5)
        {
            var query = UsersInRoleQuery(role);

            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => (u.FirstName + " " + u.LastName).Contains(search));

            return await query
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
        }

        public async Task<(IEnumerable<DoctorSearchResult> Results, int TotalCount)> SearchDoctorsAsync(
            string? search, int? specializationId, int? minPrice, int? maxPrice, double? minRating,
            string? sortBy, int page = 1, int pageSize = 5)
        {
            var query = UsersInRoleQuery("Doctor");

            if (!string.IsNullOrEmpty(search))
                query = query.Where(u => (u.FirstName + " " + u.LastName).Contains(search));

            if (specializationId.HasValue)
                query = query.Where(u => u.Specialize != null && u.Specialize.Id == specializationId);

            if (minPrice.HasValue)
                query = query.Where(u => u.Appointments.Any(a => a.Price >= minPrice));

            if (maxPrice.HasValue)
                query = query.Where(u => u.Appointments.Any(a => a.Price <= maxPrice));

            if (minRating.HasValue)
                query = query.Where(u => context.Reviews.Where(r => r.DoctorId == u.Id).Average(r => (double?)r.Rating) >= minRating);

            var totalCount = await query.CountAsync();

            query = sortBy switch
            {
                "price_asc" => query.OrderBy(u => u.Appointments.Select(a => (int?)a.Price).Min() ?? int.MaxValue),
                "rating_desc" => query.OrderByDescending(u => context.Reviews.Where(r => r.DoctorId == u.Id).Average(r => (double?)r.Rating) ?? -1),
                "name" => query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName),
                _ => query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName),
            };

            var results = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(u => new DoctorSearchResult
                {
                    Doctor = u,
                    AverageRating = context.Reviews.Where(r => r.DoctorId == u.Id).Average(r => (double?)r.Rating),
                    ReviewCount = context.Reviews.Count(r => r.DoctorId == u.Id),
                    MinPrice = u.Appointments.Select(a => (int?)a.Price).Min(),
                    MaxPrice = u.Appointments.Select(a => (int?)a.Price).Max()
                })
                .ToListAsync();

            return (results, totalCount);
        }

        public async Task<bool> EmailExistAsync(string email)
        {
            if (await userManager.FindByEmailAsync(email) is not null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> UserNameExistAsync(string userName)
        {
            if (await userManager.FindByNameAsync(userName) is not null)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public async Task<bool> CheckPasswordAsync(ApplicationUser user, string password)
        {
            return await userManager.CheckPasswordAsync(user, password);
        }

        public async Task<int> GetUsersInRoleCount(string roleName)
        {
            return await UsersInRoleQuery(roleName).CountAsync();
        }

        internal ResponseModel<AuthDTO> ErrorMessage(IdentityResult result)
        {
            var errors = string.Empty;

            foreach (var error in result.Errors)
            {
                errors += $"{error.Description},";
            }

            return new ResponseModel<AuthDTO> { Success = false, Message = errors };
        }

    }
}
