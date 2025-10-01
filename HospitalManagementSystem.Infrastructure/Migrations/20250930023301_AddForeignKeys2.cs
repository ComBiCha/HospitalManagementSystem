using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HospitalManagementSystem.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddForeignKeys2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_AppointmentId",
                table: "Notifications");

            migrationBuilder.DropIndex(
                name: "IX_Notifications_AppointmentId_Status",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "AppointmentId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DoctorName",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Message",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "PatientName",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "RecipientEmail",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "Type",
                table: "Notifications");

            // migrationBuilder.AlterColumn<int>(
            //     name: "UserId",
            //     table: "Notifications",
            //     type: "integer",
            //     maxLength: 50,
            //     nullable: false,
            //     oldClrType: typeof(string),
            //     oldType: "character varying(50)",
            //     oldMaxLength: 50);
            migrationBuilder.Sql(@"ALTER TABLE ""Notifications"" ALTER COLUMN ""UserId"" TYPE integer USING ""UserId""::integer;");
            
            migrationBuilder.CreateIndex(
                name: "IX_Users_DoctorId",
                table: "Users",
                column: "DoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_PatientId",
                table: "Users",
                column: "PatientId");

            migrationBuilder.AddForeignKey(
                name: "FK_Billings_Appointments_AppointmentId",
                table: "Billings",
                column: "AppointmentId",
                principalTable: "Appointments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Billings_Patients_PatientId",
                table: "Billings",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

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

            migrationBuilder.AddForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Doctors_DoctorId",
                table: "Users",
                column: "DoctorId",
                principalTable: "Doctors",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Patients_PatientId",
                table: "Users",
                column: "PatientId",
                principalTable: "Patients",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Billings_Appointments_AppointmentId",
                table: "Billings");

            migrationBuilder.DropForeignKey(
                name: "FK_Billings_Patients_PatientId",
                table: "Billings");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Appointments_AppointmentId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Doctors_DoctorId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Images_Patients_PatientId",
                table: "Images");

            migrationBuilder.DropForeignKey(
                name: "FK_Notifications_Users_UserId",
                table: "Notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Doctors_DoctorId",
                table: "Users");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_Patients_PatientId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_DoctorId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_PatientId",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "Notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldMaxLength: 50);

            migrationBuilder.AddColumn<int>(
                name: "AppointmentId",
                table: "Notifications",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DoctorName",
                table: "Notifications",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Message",
                table: "Notifications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientName",
                table: "Notifications",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientEmail",
                table: "Notifications",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Type",
                table: "Notifications",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AppointmentId",
                table: "Notifications",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_AppointmentId_Status",
                table: "Notifications",
                columns: new[] { "AppointmentId", "Status" });
        }
    }
}
