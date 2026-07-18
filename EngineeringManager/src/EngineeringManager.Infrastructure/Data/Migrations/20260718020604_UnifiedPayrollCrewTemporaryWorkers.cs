using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedPayrollCrewTemporaryWorkers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PayrollPayments_PayrollBatchId",
                table: "PayrollPayments");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "PaymentDate",
                table: "PayrollPayments",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<Guid>(
                name: "EmployeeId",
                table: "PayrollPayments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                table: "PayrollPayments",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.AddColumn<string>(
                name: "BankAccountSnapshot",
                table: "PayrollPayments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ConstructionWorkerId",
                table: "PayrollPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CreatedAt",
                table: "PayrollPayments",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<Guid>(
                name: "CrewBusinessPartnerId",
                table: "PayrollPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CrewNameSnapshot",
                table: "PayrollPayments",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdentityNumberSnapshot",
                table: "PayrollPayments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhoneSnapshot",
                table: "PayrollPayments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientKey",
                table: "PayrollPayments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecipientNameSnapshot",
                table: "PayrollPayments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecipientType",
                table: "PayrollPayments",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "TemporaryWorkerId",
                table: "PayrollPayments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TradeSnapshot",
                table: "PayrollPayments",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountId",
                table: "PayrollBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "AccountTransactionId",
                table: "PayrollBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualAmount",
                table: "PayrollBatches",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsUnifiedDisbursement",
                table: "PayrollBatches",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateOnly>(
                name: "PaymentDate",
                table: "PayrollBatches",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PaymentMethod",
                table: "PayrollBatches",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ReviewedAt",
                table: "PayrollBatches",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedByUserId",
                table: "PayrollBatches",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "UpdatedAt",
                table: "PayrollBatches",
                type: "datetimeoffset",
                nullable: false,
                defaultValue: new DateTimeOffset(new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)));

            migrationBuilder.AddColumn<string>(
                name: "VoucherNumber",
                table: "PayrollBatches",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConstructionWorkers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdentityNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Trade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionWorkers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollCrewAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayrollBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CrewBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PayableEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollCrewAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollCrewAllocations_BusinessPartners_CrewBusinessPartnerId",
                        column: x => x.CrewBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollCrewAllocations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollCrewAllocations_PayableEntries_PayableEntryId",
                        column: x => x.PayableEntryId,
                        principalTable: "PayableEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollCrewAllocations_PayrollBatches_PayrollBatchId",
                        column: x => x.PayrollBatchId,
                        principalTable: "PayrollBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TemporaryWorkers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IdentityNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Trade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DefaultProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ConvertedEmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TemporaryWorkers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TemporaryWorkers_Employees_ConvertedEmployeeId",
                        column: x => x.ConvertedEmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TemporaryWorkers_Projects_DefaultProjectId",
                        column: x => x.DefaultProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConstructionCrewMemberships",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConstructionWorkerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CrewBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConstructionCrewMemberships", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConstructionCrewMemberships_BusinessPartners_CrewBusinessPartnerId",
                        column: x => x.CrewBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConstructionCrewMemberships_ConstructionWorkers_ConstructionWorkerId",
                        column: x => x.ConstructionWorkerId,
                        principalTable: "ConstructionWorkers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayments_ConstructionWorkerId",
                table: "PayrollPayments",
                column: "ConstructionWorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayments_CrewBusinessPartnerId",
                table: "PayrollPayments",
                column: "CrewBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayments_PayrollBatchId_RecipientKey",
                table: "PayrollPayments",
                columns: new[] { "PayrollBatchId", "RecipientKey" },
                unique: true,
                filter: "[RecipientKey] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayments_TemporaryWorkerId",
                table: "PayrollPayments",
                column: "TemporaryWorkerId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_PayrollPayments_Recipient",
                table: "PayrollPayments",
                sql: "([RecipientType] = 1 AND [EmployeeId] IS NOT NULL AND [ConstructionWorkerId] IS NULL AND [TemporaryWorkerId] IS NULL AND [CrewBusinessPartnerId] IS NULL) OR ([RecipientType] = 2 AND [EmployeeId] IS NULL AND [ConstructionWorkerId] IS NOT NULL AND [TemporaryWorkerId] IS NULL AND [CrewBusinessPartnerId] IS NOT NULL) OR ([RecipientType] = 3 AND [EmployeeId] IS NULL AND [ConstructionWorkerId] IS NULL AND [TemporaryWorkerId] IS NOT NULL AND [CrewBusinessPartnerId] IS NULL)");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollBatches_AccountId",
                table: "PayrollBatches",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionCrewMemberships_ConstructionWorkerId_CrewBusinessPartnerId_StartDate",
                table: "ConstructionCrewMemberships",
                columns: new[] { "ConstructionWorkerId", "CrewBusinessPartnerId", "StartDate" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionCrewMemberships_CrewBusinessPartnerId",
                table: "ConstructionCrewMemberships",
                column: "CrewBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ConstructionWorkers_IdentityNumber",
                table: "ConstructionWorkers",
                column: "IdentityNumber",
                filter: "[IdentityNumber] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCrewAllocations_ContractId",
                table: "PayrollCrewAllocations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCrewAllocations_CrewBusinessPartnerId",
                table: "PayrollCrewAllocations",
                column: "CrewBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCrewAllocations_PayableEntryId",
                table: "PayrollCrewAllocations",
                column: "PayableEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCrewAllocations_PayrollBatchId_CrewBusinessPartnerId",
                table: "PayrollCrewAllocations",
                columns: new[] { "PayrollBatchId", "CrewBusinessPartnerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryWorkers_ConvertedEmployeeId",
                table: "TemporaryWorkers",
                column: "ConvertedEmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryWorkers_DefaultProjectId",
                table: "TemporaryWorkers",
                column: "DefaultProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_TemporaryWorkers_IdentityNumber",
                table: "TemporaryWorkers",
                column: "IdentityNumber",
                filter: "[IdentityNumber] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollBatches_FinancialAccounts_AccountId",
                table: "PayrollBatches",
                column: "AccountId",
                principalTable: "FinancialAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollPayments_BusinessPartners_CrewBusinessPartnerId",
                table: "PayrollPayments",
                column: "CrewBusinessPartnerId",
                principalTable: "BusinessPartners",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollPayments_ConstructionWorkers_ConstructionWorkerId",
                table: "PayrollPayments",
                column: "ConstructionWorkerId",
                principalTable: "ConstructionWorkers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollPayments_TemporaryWorkers_TemporaryWorkerId",
                table: "PayrollPayments",
                column: "TemporaryWorkerId",
                principalTable: "TemporaryWorkers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PayrollBatches_FinancialAccounts_AccountId",
                table: "PayrollBatches");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollPayments_BusinessPartners_CrewBusinessPartnerId",
                table: "PayrollPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollPayments_ConstructionWorkers_ConstructionWorkerId",
                table: "PayrollPayments");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollPayments_TemporaryWorkers_TemporaryWorkerId",
                table: "PayrollPayments");

            migrationBuilder.DropTable(
                name: "ConstructionCrewMemberships");

            migrationBuilder.DropTable(
                name: "PayrollCrewAllocations");

            migrationBuilder.DropTable(
                name: "TemporaryWorkers");

            migrationBuilder.DropTable(
                name: "ConstructionWorkers");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPayments_ConstructionWorkerId",
                table: "PayrollPayments");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPayments_CrewBusinessPartnerId",
                table: "PayrollPayments");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPayments_PayrollBatchId_RecipientKey",
                table: "PayrollPayments");

            migrationBuilder.DropIndex(
                name: "IX_PayrollPayments_TemporaryWorkerId",
                table: "PayrollPayments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_PayrollPayments_Recipient",
                table: "PayrollPayments");

            migrationBuilder.DropIndex(
                name: "IX_PayrollBatches_AccountId",
                table: "PayrollBatches");

            migrationBuilder.DropColumn(
                name: "BankAccountSnapshot",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "ConstructionWorkerId",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "CrewBusinessPartnerId",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "CrewNameSnapshot",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "IdentityNumberSnapshot",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "PhoneSnapshot",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "RecipientKey",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "RecipientNameSnapshot",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "RecipientType",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "TemporaryWorkerId",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "TradeSnapshot",
                table: "PayrollPayments");

            migrationBuilder.DropColumn(
                name: "AccountId",
                table: "PayrollBatches");

            migrationBuilder.DropColumn(
                name: "AccountTransactionId",
                table: "PayrollBatches");

            migrationBuilder.DropColumn(
                name: "ActualAmount",
                table: "PayrollBatches");

            migrationBuilder.DropColumn(
                name: "IsUnifiedDisbursement",
                table: "PayrollBatches");

            migrationBuilder.DropColumn(
                name: "PaymentDate",
                table: "PayrollBatches");

            migrationBuilder.DropColumn(
                name: "PaymentMethod",
                table: "PayrollBatches");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "PayrollBatches");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "PayrollBatches");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PayrollBatches");

            migrationBuilder.DropColumn(
                name: "VoucherNumber",
                table: "PayrollBatches");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "PaymentDate",
                table: "PayrollPayments",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "EmployeeId",
                table: "PayrollPayments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "AccountId",
                table: "PayrollPayments",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollPayments_PayrollBatchId",
                table: "PayrollPayments",
                column: "PayrollBatchId");
        }
    }
}
