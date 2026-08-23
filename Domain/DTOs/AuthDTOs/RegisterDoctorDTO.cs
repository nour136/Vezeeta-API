using Domain.Enums;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.DTOs.AuthDTOs
{
    public class RegisterDoctorDTO : RegisterDTO
    {
        [Range(1, int.MaxValue, ErrorMessage = "A valid specialization must be selected.")]
        public int SpecializeId { get; set; }
    }
}
