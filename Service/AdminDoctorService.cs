using AutoMapper;
using Domain.DTOs.AdminDTOs;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Domain.Models;
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
using System.Text.Json;
using System.Threading.Tasks;

namespace Service
{
    public class AdminDoctorService : IAdminDoctorService
    {
        private readonly IUnitOfWork unitOfWork;
        private readonly IMapper mapper;
        private readonly IImageService imageService;
        private readonly IDistributedCache cache;
        private readonly ILogger<AdminDoctorService> logger;

        public AdminDoctorService(IUnitOfWork unitOfWork, IMapper mapper, IImageService imageService, IDistributedCache cache, ILogger<AdminDoctorService> logger)
        {
            this.unitOfWork = unitOfWork;
            this.mapper = mapper;
            this.imageService = imageService;
            this.cache = cache;
            this.logger = logger;
        }

        public async Task<ResponseModel<IEnumerable<SpecializationDTO>>> GetAllSpecializationsAsync(string search = "", int page = 1, int pageSize = 5)
        {
            // Specializations have no create/update/delete endpoint anywhere in this API -
            // they're reference data, seeded and edited outside the app. That makes the
            // unfiltered listing a genuinely safe read to cache: no invalidation logic is
            // needed, just a TTL as a safety net for the rare out-of-band DB edit. A free-text
            // search isn't cached - too many unique key variants for the reuse to be worth it.
            var cacheKey = string.IsNullOrEmpty(search) ? $"specializations:page:{page}:size:{pageSize}" : null;

            if (cacheKey != null)
            {
                var cached = await cache.GetStringAsync(cacheKey);
                if (cached != null)
                {
                    logger.LogInformation("Specializations page {Page} served from cache", page);
                    return JsonSerializer.Deserialize<ResponseModel<IEnumerable<SpecializationDTO>>>(cached)!;
                }
            }

            IEnumerable<Specialization> specializations = new List<Specialization>();

            try
            {
                if (!string.IsNullOrEmpty(search))
                    specializations = await unitOfWork.Specializations.GetAllPaginatedFilteredAsync(s => s.Name.Contains(search), page, pageSize);
                else
                    specializations = await unitOfWork.Specializations.GetAllPaginatedFilteredAsync(null, page, pageSize);
            }
            catch (DbUpdateException ex)
            {
                logger.LogError(ex, "Failed to retrieve specializations (search: {Search})", search);
                return new ResponseModel<IEnumerable<SpecializationDTO>> { Message = "Something went wrong.", ErrorType = ErrorType.Unexpected };
            }

            var specializationsDTO = mapper.Map<IEnumerable<SpecializationDTO>>(specializations);

            Metadata meta = new Metadata
            {
                Page = 1,
                PageSize = pageSize,
                Next = page + 1,
                Previous = page - 1
            };

            var response = new ResponseModel<IEnumerable<SpecializationDTO>> { MetaData = meta, Success = true, Message = "Specializations retrieved.", Data = specializationsDTO };

            if (cacheKey != null)
            {
                try
                {
                    await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(response),
                        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) });
                }
                catch (Exception ex)
                {
                    // Redis being briefly unavailable shouldn't turn a successful DB read into
                    // a failed response - the endpoint still works, it just skips the cache.
                    logger.LogWarning(ex, "Failed to cache specializations page {Page}", page);
                }
            }

            return response;
        }

        public async Task<ResponseModel<IEnumerable<DoctorDTO>>> GetAllDoctorsAsync(string role, string search, int page = 1, int pageSize = 5)
        {
            var users = await unitOfWork.AuthRepository.GetUsersInRole("Doctor", search, page, pageSize);

            var doctorsDTO = mapper.Map<IEnumerable<DoctorDTO>>(users);

            foreach (var doctor in doctorsDTO)
                doctor.Image = imageService.GenerateUrl(doctor.Image);

            Metadata meta = new Metadata
            {
                Page = 1,
                PageSize = pageSize,
                Next = page + 1,
                Previous = page - 1
            };

            return new ResponseModel<IEnumerable<DoctorDTO>> { MetaData = meta, Success = true, Message = "Doctors retrieved.", Data = doctorsDTO };
        }

        public async Task<ResponseModel<DoctorDTO>> GetDoctorByIdAsync(string id)
        {
            var doctor = await unitOfWork.AuthRepository.GetUserByIdAsync(id);

            if (doctor is null)
                return new ResponseModel<DoctorDTO> { Message = "Invalid ID!", ErrorType = ErrorType.NotFound };

            var roles = await unitOfWork.AuthRepository.GetRolesAsync(doctor);

            if (!roles.Contains("Doctor"))
                return new ResponseModel<DoctorDTO> { Message = "Invalid role!", ErrorType = ErrorType.NotFound };

            var doctorDTO = mapper.Map<DoctorDTO>(doctor);

            doctorDTO.Image = imageService.GenerateUrl(doctorDTO.Image);

            return new ResponseModel<DoctorDTO> { Success = true, Message = "Doctor retrieved", Data = doctorDTO };
        }

    }
}
