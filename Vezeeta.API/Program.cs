using Domain;
using Domain.Models;
using Domain.Repositories;
using Domain.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Repository;
using Service;
using System.Text;
using System.Text.Json.Serialization;
using Vezeeta.API.Middlewares;

namespace Vezeeta.API
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);
            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(options =>
            {
                options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.ApiKey,
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Insert Token"
                });

                options.AddSecurityRequirement(new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecurityScheme
                        {
                            Reference = new OpenApiReference
                            {
                                Type = ReferenceType.SecurityScheme,
                                Id = "Bearer"
                            },
                            Name = "Bearer",
                            In = ParameterLocation.Header
                        },
                        new List<string>()
                    }
                });
            });
            // To map the section JWT from appsettings to class JWT
            builder.Services.Configure<JWT>(builder.Configuration.GetSection("JWT"));

            builder.Services.AddIdentity<ApplicationUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>();

            builder.Services.AddDbContext<ApplicationDbContext>(optionBuilder =>
                optionBuilder.UseSqlServer(builder.Configuration.GetConnectionString("VezeetaDB"))
            );

            builder.Services.AddTransient<IAuthService, AuthService>();
            builder.Services.AddTransient<IAdminDoctorService, AdminDoctorService>();
            builder.Services.AddTransient<IAdminPatientService, AdminPatientService>();
            builder.Services.AddTransient<IDiscountCodeService, DiscountCodeService>();
            builder.Services.AddTransient<IDoctorService, DoctorService>();
            builder.Services.AddTransient<IPatientService, PatientService>();
            builder.Services.AddTransient<IImageService, ImageService>();
            builder.Services.AddTransient<IReviewService, ReviewService>();
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
            builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());


            builder.Services.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
                .AddJwtBearer(o =>
                {
                    o.RequireHttpsMetadata = false;
                    o.SaveToken = false;
                    o.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidIssuer = builder.Configuration["JWT:Issuer"],
                        ValidAudience = builder.Configuration["JWT:Audience"],
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["JWT:Key"]))
                    };
                });

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            // Must be registered first so it can catch exceptions thrown by everything after it
            // (including auth, routing, and controller actions).
            app.UseGlobalExceptionHandling();

            // Registered right after exception handling so it still sees the final status code
            // (including 500s) even when a downstream request throws.
            app.UseRequestLogging();

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseAuthentication();
            app.UseAuthorization();


            app.MapControllers();

            // Apply any pending EF Core migrations automatically so a fresh database -
            // including a brand new Docker container - ends up with the correct schema
            // without a manual `dotnet ef database update` step. Idempotent: EF Core skips
            // migrations that are already applied, so this is safe on every restart.
            // Trade-off: auto-migrating on startup is convenient for a project meant to run
            // with `docker compose up`, but a team running this against a real production
            // database would normally want migrations reviewed and applied as a separate,
            // controlled deploy step instead.
            using (var migrationScope = app.Services.CreateScope())
            {
                var db = migrationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                await db.Database.MigrateAsync();
            }
            // One-time admin bootstrap: there's no self-registration or dedicated login flow
            // for Admin anywhere in this API, so without this, a fresh database - including a
            // fresh Docker container - has three roles seeded by migration but no way for
            // anyone to ever actually become an Admin. Reads credentials from configuration
            // (environment variables in Docker/production, user secrets locally) rather than
            // a hardcoded default, since this account has full admin access.
            using (var scope = app.Services.CreateScope())
            {
                await SeedDefaultAdminAsync(scope.ServiceProvider, app.Configuration, app.Logger);
            }

            await app.RunAsync();
        }

        private static async Task SeedDefaultAdminAsync(IServiceProvider services, IConfiguration configuration, ILogger logger)
        {
            var adminEmail = configuration["Admin:Email"];
            var adminPassword = configuration["Admin:Password"];

            if (string.IsNullOrWhiteSpace(adminEmail) || string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogWarning(
                    "Admin:Email / Admin:Password are not configured - skipping default admin bootstrap. " +
                    "No Admin account exists unless one was created previously. Set these via the " +
                    "ADMIN__EMAIL / ADMIN__PASSWORD environment variables (Docker) or user secrets (local dev).");
                return;
            }

            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            // Never overwrite or duplicate - if any Admin already exists, this is a no-op on
            // every subsequent restart, not just the first one.
            var existingAdmins = await userManager.GetUsersInRoleAsync("Admin");
            if (existingAdmins.Any())
                return;

            var admin = await userManager.FindByEmailAsync(adminEmail);

            if (admin is null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    FirstName = "Admin",
                    LastName = "Account",
                    Phone = "0000000000",
                    EmailConfirmed = true
                };

                // Must satisfy Identity's default password policy (min length 6, upper, lower,
                // digit, non-alphanumeric) since AddIdentity<>() below uses default options.
                var createResult = await userManager.CreateAsync(admin, adminPassword);

                if (!createResult.Succeeded)
                {
                    logger.LogError("Failed to create default admin account: {Errors}",
                        string.Join(", ", createResult.Errors.Select(e => e.Description)));
                    return;
                }
            }

            var roleResult = await userManager.AddToRoleAsync(admin, "Admin");

            if (roleResult.Succeeded)
                logger.LogInformation("Default admin account is ready ({Email})", adminEmail);
            else
                logger.LogError("Failed to add default admin to the Admin role: {Errors}",
                    string.Join(", ", roleResult.Errors.Select(e => e.Description)));
        }
    }
}
