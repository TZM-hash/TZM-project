using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class InternalFinanceLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FinancialAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    AccountNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    AccountType = table.Column<int>(type: "int", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancialAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FinancialAccounts_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InvoiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    InvoiceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TaxRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    NetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TaxAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    GrossAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceEntries_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceEntries_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceEntries_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayableEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayableEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayableEntries_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayableEntries_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayableEntries_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayableEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReceivableEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsVoided = table.Column<bool>(type: "bit", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReceivableEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReceivableEntries_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceivableEntries_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceivableEntries_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReceivableEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountTransactions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    SourceType = table.Column<int>(type: "int", nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransactionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountTransactions_FinancialAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AccountTransfers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToAccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    OutTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    InTransactionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountTransfers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AccountTransfers_FinancialAccounts_FromAccountId",
                        column: x => x.FromAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AccountTransfers_FinancialAccounts_ToAccountId",
                        column: x => x.ToAccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceLineItemLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractLineItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceLineItemLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceLineItemLinks_ContractLineItems_ContractLineItemId",
                        column: x => x.ContractLineItemId,
                        principalTable: "ContractLineItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceLineItemLinks_InvoiceEntries_InvoiceEntryId",
                        column: x => x.InvoiceEntryId,
                        principalTable: "InvoiceEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DeductionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayableEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeductionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeductionEntries_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeductionEntries_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeductionEntries_PayableEntries_PayableEntryId",
                        column: x => x.PayableEntryId,
                        principalTable: "PayableEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DeductionEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PayableEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentEntries_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEntries_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEntries_FinancialAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEntries_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEntries_PayableEntries_PayableEntryId",
                        column: x => x.PayableEntryId,
                        principalTable: "PayableEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CollectionEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivableEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentMethod = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CollectionEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CollectionEntries_BusinessPartners_BusinessPartnerId",
                        column: x => x.BusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionEntries_Contracts_ContractId",
                        column: x => x.ContractId,
                        principalTable: "Contracts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionEntries_FinancialAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionEntries_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionEntries_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CollectionEntries_ReceivableEntries_ReceivableEntryId",
                        column: x => x.ReceivableEntryId,
                        principalTable: "ReceivableEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvoiceReceivableLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReceivableEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AllocatedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoiceReceivableLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoiceReceivableLinks_InvoiceEntries_InvoiceEntryId",
                        column: x => x.InvoiceEntryId,
                        principalTable: "InvoiceEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvoiceReceivableLinks_ReceivableEntries_ReceivableEntryId",
                        column: x => x.ReceivableEntryId,
                        principalTable: "ReceivableEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PaymentReversalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdjustmentType = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentReversalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentReversalEntries_FinancialAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PaymentReversalEntries_PaymentEntries_PaymentEntryId",
                        column: x => x.PaymentEntryId,
                        principalTable: "PaymentEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "RefundOrReversalEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CollectionEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ReceivableEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    AccountId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    AdjustmentType = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefundOrReversalEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefundOrReversalEntries_CollectionEntries_CollectionEntryId",
                        column: x => x.CollectionEntryId,
                        principalTable: "CollectionEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundOrReversalEntries_FinancialAccounts_AccountId",
                        column: x => x.AccountId,
                        principalTable: "FinancialAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RefundOrReversalEntries_ReceivableEntries_ReceivableEntryId",
                        column: x => x.ReceivableEntryId,
                        principalTable: "ReceivableEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_AccountId",
                table: "AccountTransactions",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransactions_SourceType_SourceId_Direction",
                table: "AccountTransactions",
                columns: new[] { "SourceType", "SourceId", "Direction" });

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransfers_FromAccountId",
                table: "AccountTransfers",
                column: "FromAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AccountTransfers_ToAccountId",
                table: "AccountTransfers",
                column: "ToAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionEntries_AccountId",
                table: "CollectionEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionEntries_BusinessPartnerId",
                table: "CollectionEntries",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionEntries_ContractId",
                table: "CollectionEntries",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionEntries_LegalEntityId",
                table: "CollectionEntries",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionEntries_ProjectId",
                table: "CollectionEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CollectionEntries_ReceivableEntryId",
                table: "CollectionEntries",
                column: "ReceivableEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DeductionEntries_BusinessPartnerId",
                table: "DeductionEntries",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_DeductionEntries_LegalEntityId",
                table: "DeductionEntries",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_DeductionEntries_PayableEntryId",
                table: "DeductionEntries",
                column: "PayableEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_DeductionEntries_ProjectId",
                table: "DeductionEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_FinancialAccounts_LegalEntityId_AccountName",
                table: "FinancialAccounts",
                columns: new[] { "LegalEntityId", "AccountName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEntries_BusinessPartnerId",
                table: "InvoiceEntries",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEntries_ContractId",
                table: "InvoiceEntries",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEntries_LegalEntityId_Direction_InvoiceNumber",
                table: "InvoiceEntries",
                columns: new[] { "LegalEntityId", "Direction", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEntries_ProjectId",
                table: "InvoiceEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItemLinks_ContractLineItemId",
                table: "InvoiceLineItemLinks",
                column: "ContractLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceLineItemLinks_InvoiceEntryId_ContractLineItemId",
                table: "InvoiceLineItemLinks",
                columns: new[] { "InvoiceEntryId", "ContractLineItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceReceivableLinks_InvoiceEntryId_ReceivableEntryId",
                table: "InvoiceReceivableLinks",
                columns: new[] { "InvoiceEntryId", "ReceivableEntryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceReceivableLinks_ReceivableEntryId",
                table: "InvoiceReceivableLinks",
                column: "ReceivableEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PayableEntries_BusinessPartnerId",
                table: "PayableEntries",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PayableEntries_ContractId",
                table: "PayableEntries",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_PayableEntries_LegalEntityId",
                table: "PayableEntries",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_PayableEntries_ProjectId",
                table: "PayableEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntries_AccountId",
                table: "PaymentEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntries_BusinessPartnerId",
                table: "PaymentEntries",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntries_ContractId",
                table: "PaymentEntries",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntries_LegalEntityId",
                table: "PaymentEntries",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntries_PayableEntryId",
                table: "PaymentEntries",
                column: "PayableEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentEntries_ProjectId",
                table: "PaymentEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReversalEntries_AccountId",
                table: "PaymentReversalEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentReversalEntries_PaymentEntryId",
                table: "PaymentReversalEntries",
                column: "PaymentEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivableEntries_BusinessPartnerId",
                table: "ReceivableEntries",
                column: "BusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivableEntries_ContractId",
                table: "ReceivableEntries",
                column: "ContractId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivableEntries_LegalEntityId",
                table: "ReceivableEntries",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_ReceivableEntries_ProjectId",
                table: "ReceivableEntries",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundOrReversalEntries_AccountId",
                table: "RefundOrReversalEntries",
                column: "AccountId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundOrReversalEntries_CollectionEntryId",
                table: "RefundOrReversalEntries",
                column: "CollectionEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_RefundOrReversalEntries_ReceivableEntryId",
                table: "RefundOrReversalEntries",
                column: "ReceivableEntryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AccountTransactions");

            migrationBuilder.DropTable(
                name: "AccountTransfers");

            migrationBuilder.DropTable(
                name: "DeductionEntries");

            migrationBuilder.DropTable(
                name: "InvoiceLineItemLinks");

            migrationBuilder.DropTable(
                name: "InvoiceReceivableLinks");

            migrationBuilder.DropTable(
                name: "PaymentReversalEntries");

            migrationBuilder.DropTable(
                name: "RefundOrReversalEntries");

            migrationBuilder.DropTable(
                name: "InvoiceEntries");

            migrationBuilder.DropTable(
                name: "PaymentEntries");

            migrationBuilder.DropTable(
                name: "CollectionEntries");

            migrationBuilder.DropTable(
                name: "PayableEntries");

            migrationBuilder.DropTable(
                name: "FinancialAccounts");

            migrationBuilder.DropTable(
                name: "ReceivableEntries");
        }
    }
}
