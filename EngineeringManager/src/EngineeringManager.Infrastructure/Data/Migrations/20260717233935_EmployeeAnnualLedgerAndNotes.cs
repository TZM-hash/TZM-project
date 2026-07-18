using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EmployeeAnnualLedgerAndNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Projects",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ProjectPartners",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ProjectMilestones",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ProjectAssignments",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "PartnerContacts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "FinancialAccounts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AdjustmentAmount",
                table: "ExpenseRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<Guid>(
                name: "AttachmentId",
                table: "ExpenseRecords",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OriginalAmount",
                table: "ExpenseRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ReceiptNumber",
                table: "ExpenseRecords",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "EquipmentSettlements",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Employees",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Contracts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BusinessYears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BusinessYears", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeFinancialAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdjustmentType = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ReversalOfId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeFinancialAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeFinancialAdjustments_BusinessYears_BusinessYearId",
                        column: x => x.BusinessYearId,
                        principalTable: "BusinessYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeFinancialAdjustments_EmployeeFinancialAdjustments_ReversalOfId",
                        column: x => x.ReversalOfId,
                        principalTable: "EmployeeFinancialAdjustments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeFinancialAdjustments_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeReceipts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceiptDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReceiptType = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentLegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    ActualRecipientName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LaborBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AccountTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeReceipts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeReceipts_BusinessPartners_LaborBusinessPartnerId",
                        column: x => x.LaborBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeReceipts_BusinessYears_BusinessYearId",
                        column: x => x.BusinessYearId,
                        principalTable: "BusinessYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeReceipts_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeReceipts_FinancialAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeReceipts_LegalEntities_PaymentLegalEntityId",
                        column: x => x.PaymentLegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeReceipts_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EmployeeWageEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    WageCategory = table.Column<int>(type: "int", nullable: false),
                    CalculationMethod = table.Column<int>(type: "int", nullable: false),
                    Nature = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    AutomaticAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LaborBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AdjustmentAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    FinalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    SourcePayrollItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeWageEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeWageEntries_BusinessPartners_LaborBusinessPartnerId",
                        column: x => x.LaborBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeWageEntries_BusinessYears_BusinessYearId",
                        column: x => x.BusinessYearId,
                        principalTable: "BusinessYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeWageEntries_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeWageEntries_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeWageEntries_PayrollItems_SourcePayrollItemId",
                        column: x => x.SourcePayrollItemId,
                        principalTable: "PayrollItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeWageEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseRecords_AttachmentId",
                table: "ExpenseRecords",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BusinessYears_Name",
                table: "BusinessYears",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BusinessYears_StartDate_EndDate",
                table: "BusinessYears",
                columns: ["StartDate", "EndDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFinancialAdjustments_BusinessYearId",
                table: "EmployeeFinancialAdjustments",
                column: "BusinessYearId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFinancialAdjustments_EmployeeId_BusinessYearId_AdjustmentDate",
                table: "EmployeeFinancialAdjustments",
                columns: ["EmployeeId", "BusinessYearId", "AdjustmentDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeFinancialAdjustments_ReversalOfId",
                table: "EmployeeFinancialAdjustments",
                column: "ReversalOfId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReceipts_AccountId",
                table: "EmployeeReceipts",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReceipts_BusinessYearId",
                table: "EmployeeReceipts",
                column: "BusinessYearId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReceipts_EmployeeId_BusinessYearId_ReceiptDate",
                table: "EmployeeReceipts",
                columns: ["EmployeeId", "BusinessYearId", "ReceiptDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReceipts_LaborBusinessPartnerId",
                table: "EmployeeReceipts",
                column: "LaborBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReceipts_PaymentLegalEntityId",
                table: "EmployeeReceipts",
                column: "PaymentLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeReceipts_ProjectId",
                table: "EmployeeReceipts",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWageEntries_BusinessYearId",
                table: "EmployeeWageEntries",
                column: "BusinessYearId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWageEntries_EmployeeId_BusinessYearId_StartDate",
                table: "EmployeeWageEntries",
                columns: ["EmployeeId", "BusinessYearId", "StartDate"]);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWageEntries_LaborBusinessPartnerId",
                table: "EmployeeWageEntries",
                column: "LaborBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWageEntries_LegalEntityId",
                table: "EmployeeWageEntries",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWageEntries_ProjectId",
                table: "EmployeeWageEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeWageEntries_SourcePayrollItemId",
                table: "EmployeeWageEntries",
                column: "SourcePayrollItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseRecords_Attachments_AttachmentId",
                table: "ExpenseRecords",
                column: "AttachmentId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseRecords_Attachments_AttachmentId",
                table: "ExpenseRecords");

            migrationBuilder.DropTable(
                name: "EmployeeFinancialAdjustments");

            migrationBuilder.DropTable(
                name: "EmployeeReceipts");

            migrationBuilder.DropTable(
                name: "EmployeeWageEntries");

            migrationBuilder.DropTable(
                name: "BusinessYears");

            migrationBuilder.DropIndex(
                name: "IX_ExpenseRecords_AttachmentId",
                table: "ExpenseRecords");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ProjectPartners");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ProjectMilestones");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ProjectAssignments");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "PartnerContacts");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "FinancialAccounts");

            migrationBuilder.DropColumn(
                name: "AdjustmentAmount",
                table: "ExpenseRecords");

            migrationBuilder.DropColumn(
                name: "AttachmentId",
                table: "ExpenseRecords");

            migrationBuilder.DropColumn(
                name: "OriginalAmount",
                table: "ExpenseRecords");

            migrationBuilder.DropColumn(
                name: "ReceiptNumber",
                table: "ExpenseRecords");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "EquipmentSettlements");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Contracts");
        }
    }
}
