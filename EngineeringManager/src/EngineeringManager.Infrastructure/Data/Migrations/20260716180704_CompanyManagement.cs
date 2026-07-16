using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814, CA1861 // Generated migration arrays

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CompanyManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BusinessAddress",
                table: "LegalEntities",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CompanyCategoryId",
                table: "LegalEntities",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConcurrencyStamp",
                table: "LegalEntities",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<string>(
                name: "InvoiceTitle",
                table: "LegalEntities",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegalRepresentative",
                table: "LegalEntities",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "LegalEntities",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "LegalEntities",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegisteredAddress",
                table: "LegalEntities",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultCollection",
                table: "FinancialAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultInvoice",
                table: "FinancialAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsDefaultPayment",
                table: "FinancialAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CompanyCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CompanyCertificates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CertificateType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CertificateNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("PK_CompanyCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CompanyCertificates_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CompanyCertificates_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "CompanyCategories",
                columns: new[] { "Id", "Code", "ConcurrencyStamp", "CreatedAt", "IsActive", "Name", "SortOrder", "UpdatedAt" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), "GENERAL_COMPANY", new Guid("20000000-0000-0000-0000-000000000001"), new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "一般纳税人有限公司", 10, new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("10000000-0000-0000-0000-000000000002"), "SMALL_COMPANY", new Guid("20000000-0000-0000-0000-000000000002"), new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "小规模纳税人有限公司", 20, new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("10000000-0000-0000-0000-000000000003"), "SMALL_SOLE", new Guid("20000000-0000-0000-0000-000000000003"), new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "小规模个体工商户", 30, new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("10000000-0000-0000-0000-000000000004"), "OTHER", new Guid("20000000-0000-0000-0000-000000000004"), new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, "其他主体", 90, new DateTimeOffset(new DateTime(2026, 7, 17, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_LegalEntities_CompanyCategoryId",
                table: "LegalEntities",
                column: "CompanyCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_LegalEntityId_IsDefaultCollection",
                table: "FinancialAccounts",
                columns: new[] { "LegalEntityId", "IsDefaultCollection" },
                unique: true,
                filter: "[IsDefaultCollection] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_LegalEntityId_IsDefaultInvoice",
                table: "FinancialAccounts",
                columns: new[] { "LegalEntityId", "IsDefaultInvoice" },
                unique: true,
                filter: "[IsDefaultInvoice] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_LegalEntityId_IsDefaultPayment",
                table: "FinancialAccounts",
                columns: new[] { "LegalEntityId", "IsDefaultPayment" },
                unique: true,
                filter: "[IsDefaultPayment] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCategories_Code",
                table: "CompanyCategories",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCategories_SortOrder_Name",
                table: "CompanyCategories",
                columns: new[] { "SortOrder", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCertificates_AttachmentId",
                table: "CompanyCertificates",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCertificates_ExpiresOn_IsDeleted",
                table: "CompanyCertificates",
                columns: new[] { "ExpiresOn", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_CompanyCertificates_LegalEntityId_CertificateType_CertificateNumber",
                table: "CompanyCertificates",
                columns: new[] { "LegalEntityId", "CertificateType", "CertificateNumber" });

            migrationBuilder.AddForeignKey(
                name: "FK_LegalEntities_CompanyCategories_CompanyCategoryId",
                table: "LegalEntities",
                column: "CompanyCategoryId",
                principalTable: "CompanyCategories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LegalEntities_CompanyCategories_CompanyCategoryId",
                table: "LegalEntities");

            migrationBuilder.DropTable(
                name: "CompanyCategories");

            migrationBuilder.DropTable(
                name: "CompanyCertificates");

            migrationBuilder.DropIndex(
                name: "IX_LegalEntities_CompanyCategoryId",
                table: "LegalEntities");

            migrationBuilder.DropIndex(
                name: "IX_FinancialAccounts_LegalEntityId_IsDefaultCollection",
                table: "FinancialAccounts");

            migrationBuilder.DropIndex(
                name: "IX_FinancialAccounts_LegalEntityId_IsDefaultInvoice",
                table: "FinancialAccounts");

            migrationBuilder.DropIndex(
                name: "IX_FinancialAccounts_LegalEntityId_IsDefaultPayment",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "BusinessAddress",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "CompanyCategoryId",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "ConcurrencyStamp",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "InvoiceTitle",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "LegalRepresentative",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "RegisteredAddress",
                table: "LegalEntities");

            migrationBuilder.DropColumn(
                name: "IsDefaultCollection",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "IsDefaultInvoice",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "IsDefaultPayment",
                table: "FinancialAccounts");
        }
    }
}
