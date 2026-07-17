using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Generated migration arrays

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeAndCompanyCertificateManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "IssuingAuthority",
                table: "CompanyCertificates",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SpecialtyLevelScope",
                table: "CompanyCertificates",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CertificateNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SpecialtyLevelScope = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IssuingAuthority = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IssuedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: true),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeCertificates_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeCertificates_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertificates_AttachmentId",
                table: "EmployeeCertificates",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertificates_EmployeeId_CertificateType_CertificateNumber",
                table: "EmployeeCertificates",
                columns: new[] { "EmployeeId", "CertificateType", "CertificateNumber" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeCertificates_ExpiresOn_IsDeleted",
                table: "EmployeeCertificates",
                columns: new[] { "ExpiresOn", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeCertificates");

            migrationBuilder.DropColumn(
                name: "IssuingAuthority",
                table: "CompanyCertificates");

            migrationBuilder.DropColumn(
                name: "SpecialtyLevelScope",
                table: "CompanyCertificates");
        }
    }
}
