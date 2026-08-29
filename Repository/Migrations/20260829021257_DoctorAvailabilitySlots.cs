using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Repository.Migrations
{
    /// <inheritdoc />
    public partial class DoctorAvailabilitySlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Times_TimeId",
                table: "Bookings");

            migrationBuilder.RenameColumn(
                name: "TimeId",
                table: "Bookings",
                newName: "SlotId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_TimeId",
                table: "Bookings",
                newName: "IX_Bookings_SlotId");

            migrationBuilder.CreateTable(
                name: "Slots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Time = table.Column<TimeOnly>(type: "time", nullable: false),
                    Price = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    DoctorId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SourceAppointmentId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Slots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Slots_Appointments_SourceAppointmentId",
                        column: x => x.SourceAppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Slots_AspNetUsers_DoctorId",
                        column: x => x.DoctorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Slots_DoctorId_Date_Time",
                table: "Slots",
                columns: new[] { "DoctorId", "Date", "Time" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Slots_SourceAppointmentId",
                table: "Slots",
                column: "SourceAppointmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Slots_SlotId",
                table: "Bookings",
                column: "SlotId",
                principalTable: "Slots",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Bookings_Slots_SlotId",
                table: "Bookings");

            migrationBuilder.DropTable(
                name: "Slots");

            migrationBuilder.RenameColumn(
                name: "SlotId",
                table: "Bookings",
                newName: "TimeId");

            migrationBuilder.RenameIndex(
                name: "IX_Bookings_SlotId",
                table: "Bookings",
                newName: "IX_Bookings_TimeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Bookings_Times_TimeId",
                table: "Bookings",
                column: "TimeId",
                principalTable: "Times",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
