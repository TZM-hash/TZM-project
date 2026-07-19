using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861 // Generated migration arrays

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CentralLedgerMultiEntryFinance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinanceBusinessYears",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceBusinessYears", x => x.Id);
                    table.CheckConstraint("CK_FinanceBusinessYears_DateRange", "[StartDate] <= [EndDate]");
                });

            migrationBuilder.CreateTable(
                name: "FinanceCashEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    CashType = table.Column<int>(type: "int", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CounterLegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CounterAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsReversal = table.Column<bool>(type: "bit", nullable: false),
                    ReversesCashEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceCashEntries", x => x.Id);
                    table.CheckConstraint("CK_FinanceCashEntries_Party", "([Scope] = 1 AND [BusinessPartnerId] IS NOT NULL AND [CounterLegalEntityId] IS NULL) OR ([Scope] = 2 AND [BusinessPartnerId] IS NULL AND [CounterLegalEntityId] IS NOT NULL AND [LegalEntityId] <> [CounterLegalEntityId])");
                    table.ForeignKey(
                        name: "FK_FinanceCashEntries_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCashEntries_FinanceCashEntries_ReversesCashEntryId",
                        column: x => x.ReversesCashEntryId,
                        principalTable: "FinanceCashEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCashEntries_FinancialAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCashEntries_FinancialAccounts_CounterAccountId",
                        column: x => x.CounterAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCashEntries_LegalEntities_CounterLegalEntityId",
                        column: x => x.CounterLegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCashEntries_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceDeletionLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordType = table.Column<int>(type: "int", nullable: false),
                    RecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    DeletedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    EntryPoint = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    BeforeMetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AfterMetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CounterLegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceDeletionLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceInvoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CounterLegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ProjectTaxConfigurationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InvoiceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceInvoices", x => x.Id);
                    table.CheckConstraint("CK_FinanceInvoices_Party", "([Scope] = 1 AND [BusinessPartnerId] IS NOT NULL AND [CounterLegalEntityId] IS NULL) OR ([Scope] = 2 AND [BusinessPartnerId] IS NULL AND [CounterLegalEntityId] IS NOT NULL AND [LegalEntityId] <> [CounterLegalEntityId])");
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_LegalEntities_CounterLegalEntityId",
                        column: x => x.CounterLegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoices_ProjectTaxConfigurations_ProjectTaxConfigurationId",
                        column: x => x.ProjectTaxConfigurationId,
                        principalTable: "ProjectTaxConfigurations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceLegacyMaps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegacyEntityType = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    LegacyId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    CentralRecordType = table.Column<int>(type: "int", nullable: false),
                    CentralRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceLegacyMaps", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinanceSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    SettlementState = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CounterLegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractLineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    SettlementDate = table.Column<DateOnly>(type: "date", nullable: true),
                    OriginalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OriginalInvoiceAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceSettlements", x => x.Id);
                    table.CheckConstraint("CK_FinanceSettlements_Party", "([Scope] = 1 AND [BusinessPartnerId] IS NOT NULL AND [CounterLegalEntityId] IS NULL) OR ([Scope] = 2 AND [BusinessPartnerId] IS NULL AND [CounterLegalEntityId] IS NOT NULL AND [LegalEntityId] <> [CounterLegalEntityId])");
                    table.ForeignKey(
                        name: "FK_FinanceSettlements_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceSettlements_ContractLineItems_ContractLineItemId",
                        column: x => x.ContractLineItemId,
                        principalTable: "ContractLineItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceSettlements_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceSettlements_LegalEntities_CounterLegalEntityId",
                        column: x => x.CounterLegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceSettlements_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceSettlements_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceReconciliations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    ReconciliationScope = table.Column<int>(type: "int", nullable: false),
                    FinanceBusinessYearId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AsOfDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    QueryJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceReconciliations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceReconciliations_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceReconciliations_FinanceBusinessYears_FinanceBusinessYearId",
                        column: x => x.FinanceBusinessYearId,
                        principalTable: "FinanceBusinessYears",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceReconciliations_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceCashAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CashEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractLineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CounterLegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocationOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceCashAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceCashAllocations_ContractLineItems_ContractLineItemId",
                        column: x => x.ContractLineItemId,
                        principalTable: "ContractLineItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCashAllocations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCashAllocations_FinanceCashEntries_CashEntryId",
                        column: x => x.CashEntryId,
                        principalTable: "FinanceCashEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinanceCashAllocations_FinanceSettlements_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "FinanceSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceCashAllocations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceDeductions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ReduceInvoiceAmount = table.Column<bool>(type: "bit", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceDeductions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceDeductions_FinanceSettlements_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "FinanceSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceInvoiceAllocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractLineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CounterLegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AllocationOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceInvoiceAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceInvoiceAllocations_ContractLineItems_ContractLineItemId",
                        column: x => x.ContractLineItemId,
                        principalTable: "ContractLineItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoiceAllocations_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoiceAllocations_FinanceInvoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "FinanceInvoices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FinanceInvoiceAllocations_FinanceSettlements_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "FinanceSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FinanceInvoiceAllocations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceSettlementAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdjustmentType = table.Column<int>(type: "int", nullable: false),
                    AmountDelta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    InvoiceAmountDelta = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BusinessDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ActorUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActorUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceSettlementAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceSettlementAdjustments_FinanceSettlements_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "FinanceSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FinanceReconciliationLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReconciliationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CounterLegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ContractLineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MetricsJson = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinanceReconciliationLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinanceReconciliationLines_FinanceReconciliations_ReconciliationId",
                        column: x => x.ReconciliationId,
                        principalTable: "FinanceReconciliations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceBusinessYears_StartDate_EndDate",
                table: "FinanceBusinessYears",
                columns: new[] { "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCashAllocations_CashEntryId_AllocationOrder",
                table: "FinanceCashAllocations",
                columns: new[] { "CashEntryId", "AllocationOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCashAllocations_ContractId",
                table: "FinanceCashAllocations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCashAllocations_ContractLineItemId",
                table: "FinanceCashAllocations",
                column: "ContractLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCashAllocations_ProjectId",
                table: "FinanceCashAllocations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCashAllocations_SettlementId",
                table: "FinanceCashAllocations",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCashEntries_AccountId",
                table: "FinanceCashEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCashEntries_BusinessPartnerId",
                table: "FinanceCashEntries",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCashEntries_CounterAccountId",
                table: "FinanceCashEntries",
                column: "CounterAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCashEntries_CounterLegalEntityId",
                table: "FinanceCashEntries",
                column: "CounterLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCashEntries_LegalEntityId_BusinessDate",
                table: "FinanceCashEntries",
                columns: new[] { "LegalEntityId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceCashEntries_ReversesCashEntryId",
                table: "FinanceCashEntries",
                column: "ReversesCashEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceDeductions_SettlementId_BusinessDate",
                table: "FinanceDeductions",
                columns: new[] { "SettlementId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceDeletionLogs_DeletedAt",
                table: "FinanceDeletionLogs",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceDeletionLogs_RecordType_RecordId",
                table: "FinanceDeletionLogs",
                columns: new[] { "RecordType", "RecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoiceAllocations_ContractId",
                table: "FinanceInvoiceAllocations",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoiceAllocations_ContractLineItemId",
                table: "FinanceInvoiceAllocations",
                column: "ContractLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoiceAllocations_InvoiceId_AllocationOrder",
                table: "FinanceInvoiceAllocations",
                columns: new[] { "InvoiceId", "AllocationOrder" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoiceAllocations_ProjectId",
                table: "FinanceInvoiceAllocations",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoiceAllocations_SettlementId",
                table: "FinanceInvoiceAllocations",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_BusinessPartnerId",
                table: "FinanceInvoices",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_CounterLegalEntityId",
                table: "FinanceInvoices",
                column: "CounterLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_LegalEntityId_InvoiceNumber_InvoiceDate",
                table: "FinanceInvoices",
                columns: new[] { "LegalEntityId", "InvoiceNumber", "InvoiceDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceInvoices_ProjectTaxConfigurationId",
                table: "FinanceInvoices",
                column: "ProjectTaxConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceLegacyMaps_CentralRecordType_CentralRecordId",
                table: "FinanceLegacyMaps",
                columns: new[] { "CentralRecordType", "CentralRecordId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceLegacyMaps_LegacyEntityType_LegacyId",
                table: "FinanceLegacyMaps",
                columns: new[] { "LegacyEntityType", "LegacyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReconciliationLines_ReconciliationId_SettlementId",
                table: "FinanceReconciliationLines",
                columns: new[] { "ReconciliationId", "SettlementId" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReconciliations_BusinessPartnerId",
                table: "FinanceReconciliations",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReconciliations_FinanceBusinessYearId",
                table: "FinanceReconciliations",
                column: "FinanceBusinessYearId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReconciliations_LegalEntityId",
                table: "FinanceReconciliations",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceReconciliations_Scope_AsOfDate_Version",
                table: "FinanceReconciliations",
                columns: new[] { "Scope", "AsOfDate", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettlementAdjustments_SettlementId_BusinessDate",
                table: "FinanceSettlementAdjustments",
                columns: new[] { "SettlementId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettlements_BusinessPartnerId_BusinessDate",
                table: "FinanceSettlements",
                columns: new[] { "BusinessPartnerId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettlements_ContractId",
                table: "FinanceSettlements",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettlements_ContractLineItemId",
                table: "FinanceSettlements",
                column: "ContractLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettlements_CounterLegalEntityId",
                table: "FinanceSettlements",
                column: "CounterLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettlements_LegalEntityId",
                table: "FinanceSettlements",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettlements_ProjectId_BusinessDate",
                table: "FinanceSettlements",
                columns: new[] { "ProjectId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettlements_Scope_Direction_LegalEntityId_BusinessDate",
                table: "FinanceSettlements",
                columns: new[] { "Scope", "Direction", "LegalEntityId", "BusinessDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FinanceSettlements_SourceType_SourceId",
                table: "FinanceSettlements",
                columns: new[] { "SourceType", "SourceId" },
                unique: true,
                filter: "[SourceId] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FinanceCashAllocations");

            migrationBuilder.DropTable(
                name: "FinanceDeductions");

            migrationBuilder.DropTable(
                name: "FinanceDeletionLogs");

            migrationBuilder.DropTable(
                name: "FinanceInvoiceAllocations");

            migrationBuilder.DropTable(
                name: "FinanceLegacyMaps");

            migrationBuilder.DropTable(
                name: "FinanceReconciliationLines");

            migrationBuilder.DropTable(
                name: "FinanceSettlementAdjustments");

            migrationBuilder.DropTable(
                name: "FinanceCashEntries");

            migrationBuilder.DropTable(
                name: "FinanceInvoices");

            migrationBuilder.DropTable(
                name: "FinanceReconciliations");

            migrationBuilder.DropTable(
                name: "FinanceSettlements");

            migrationBuilder.DropTable(
                name: "FinanceBusinessYears");
        }
    }
}
