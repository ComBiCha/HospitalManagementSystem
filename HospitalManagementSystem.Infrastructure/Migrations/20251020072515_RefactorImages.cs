using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RefactorImages : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_Appointments_AppointmentId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Doctors_DoctorId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Patients_PatientId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_AppointmentId",
                table: "Images");

            migrationBuilder.DropIndex(
                name: "IX_Images_DoctorId",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "Images");

            migrationBuilder.DropColumn(
                name: "DoctorId",
                table: "Images");

            migrationBuilder.RenameColumn(
                name: "PatientId",
                table: "Images",
                newName: "MedicalRecordId");

            migrationBuilder.RenameIndex(
                name: "IX_Images_PatientId",
                table: "Images",
                newName: "IX_Images_MedicalRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_MedicalRecords_MedicalRecordId",
                table: "Images",
                column: "MedicalRecordId",
                principalTable: "MedicalRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Images_MedicalRecords_MedicalRecordId",
                table: "Images");

            migrationBuilder.RenameColumn(
                name: "MedicalRecordId",
                table: "Images",
                newName: "PatientId");

            migrationBuilder.RenameIndex(
                name: "IX_Images_MedicalRecordId",
                table: "Images",
                newName: "IX_Images_PatientId");

            migrationBuilder.AddColumn<int>(
                name: "AppointmentId",
                table: "Images",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DoctorId",
                table: "Images",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Images_AppointmentId",
                table: "Images",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Images_DoctorId",
                table: "Images",
                column: "DoctorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Appointments_AppointmentId",
                table: "Images",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Doctors_DoctorId",
                table: "Images",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Images_Patients_PatientId",
                table: "Images",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
