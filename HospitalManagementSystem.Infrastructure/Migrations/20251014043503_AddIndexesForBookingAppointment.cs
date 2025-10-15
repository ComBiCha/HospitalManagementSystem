using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIndexesForBookingAppointment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DoctorShifts_DoctorId",
                table: "DoctorShifts");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorShifts_DoctorId_DayOfWeek",
                table: "DoctorShifts",
                columns: new[] { "DoctorId", "DayOfWeek" });

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_DoctorId_Date",
                table: "Appointments",
                columns: new[] { "DoctorId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DoctorShifts_DoctorId_DayOfWeek",
                table: "DoctorShifts");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_DoctorId_Date",
                table: "Appointments");

            migrationBuilder.CreateIndex(
                name: "IX_DoctorShifts_DoctorId",
                table: "DoctorShifts",
                column: "DoctorId");
        }
    }
}
