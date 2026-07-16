using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Generated migration arrays

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Equipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentNumber = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Model = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Category = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    OwnershipType = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    OwnerLegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LessorBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PurchaseDate = table.Column<DateOnly>(type: "date", nullable: true),
                    PurchaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    InternalDailyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Equipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Equipment_BusinessPartners_LessorBusinessPartnerId",
                        column: x => x.LessorBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Equipment_LegalEntities_OwnerLegalEntityId",
                        column: x => x.OwnerLegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentLeaseAgreements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LessorBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ContractNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RentMode = table.Column<int>(type: "int", nullable: false),
                    MonthlyProrationMode = table.Column<int>(type: "int", nullable: false),
                    UnitRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentLeaseAgreements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentLeaseAgreements_BusinessPartners_LessorBusinessPartnerId",
                        column: x => x.LessorBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentLeaseAgreements_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentMaintenanceRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MaintenanceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MaintenanceDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NextDueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Provider = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentMaintenanceRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentMaintenanceRecords_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentOwnershipHistories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TransferType = table.Column<int>(type: "int", nullable: false),
                    TransferDate = table.Column<DateOnly>(type: "date", nullable: false),
                    FromLegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ToLegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalRecipientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransferAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentOwnershipHistories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentOwnershipHistories_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentOwnershipHistories_LegalEntities_FromLegalEntityId",
                        column: x => x.FromLegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentOwnershipHistories_LegalEntities_ToLegalEntityId",
                        column: x => x.ToLegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentProjectUsages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LegalEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeaseAgreementId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    RentMode = table.Column<int>(type: "int", nullable: false),
                    MonthlyProrationMode = table.Column<int>(type: "int", nullable: false),
                    UnitRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    SharedUsageOverride = table.Column<bool>(type: "bit", nullable: false),
                    SharedUsageReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentProjectUsages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentProjectUsages_EquipmentLeaseAgreements_LeaseAgreementId",
                        column: x => x.LeaseAgreementId,
                        principalTable: "EquipmentLeaseAgreements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentProjectUsages_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentProjectUsages_LegalEntities_LegalEntityId",
                        column: x => x.LegalEntityId,
                        principalTable: "LegalEntities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentProjectUsages_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentAdvancePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentType = table.Column<int>(type: "int", nullable: false),
                    PaymentDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PaymentEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentAdvancePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentAdvancePayments_EquipmentProjectUsages_UsageId",
                        column: x => x.UsageId,
                        principalTable: "EquipmentProjectUsages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentAdvancePayments_PaymentEntries_PaymentEntryId",
                        column: x => x.PaymentEntryId,
                        principalTable: "PaymentEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentSettlements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementDate = table.Column<DateOnly>(type: "date", nullable: false),
                    BaseAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    OffsetAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    PayableEntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ModificationReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    PreviousSnapshotJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentSettlements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentSettlements_EquipmentProjectUsages_UsageId",
                        column: x => x.UsageId,
                        principalTable: "EquipmentProjectUsages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EquipmentSettlements_PayableEntries_PayableEntryId",
                        column: x => x.PayableEntryId,
                        principalTable: "PayableEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentWorkPeriods",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    PeriodType = table.Column<int>(type: "int", nullable: false),
                    IsChargeable = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentWorkPeriods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentWorkPeriods_EquipmentProjectUsages_UsageId",
                        column: x => x.UsageId,
                        principalTable: "EquipmentProjectUsages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OfflineEquipmentUsageSyncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ClientDraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EquipmentProjectUsageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastServerVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineEquipmentUsageSyncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfflineEquipmentUsageSyncs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfflineEquipmentUsageSyncs_EquipmentProjectUsages_EquipmentProjectUsageId",
                        column: x => x.EquipmentProjectUsageId,
                        principalTable: "EquipmentProjectUsages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EquipmentSettlementAdjustments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SettlementId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    AdjustmentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentSettlementAdjustments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentSettlementAdjustments_EquipmentSettlements_SettlementId",
                        column: x => x.SettlementId,
                        principalTable: "EquipmentSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_EquipmentNumber",
                table: "Equipment",
                column: "EquipmentNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_LessorBusinessPartnerId",
                table: "Equipment",
                column: "LessorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_OwnerLegalEntityId",
                table: "Equipment",
                column: "OwnerLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentAdvancePayments_PaymentEntryId",
                table: "EquipmentAdvancePayments",
                column: "PaymentEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentAdvancePayments_UsageId",
                table: "EquipmentAdvancePayments",
                column: "UsageId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentLeaseAgreements_EquipmentId_StartDate",
                table: "EquipmentLeaseAgreements",
                columns: new[] { "EquipmentId", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentLeaseAgreements_LessorBusinessPartnerId",
                table: "EquipmentLeaseAgreements",
                column: "LessorBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentMaintenanceRecords_EquipmentId",
                table: "EquipmentMaintenanceRecords",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentMaintenanceRecords_NextDueDate_EquipmentId",
                table: "EquipmentMaintenanceRecords",
                columns: new[] { "NextDueDate", "EquipmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentOwnershipHistories_EquipmentId",
                table: "EquipmentOwnershipHistories",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentOwnershipHistories_FromLegalEntityId",
                table: "EquipmentOwnershipHistories",
                column: "FromLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentOwnershipHistories_ToLegalEntityId",
                table: "EquipmentOwnershipHistories",
                column: "ToLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentProjectUsages_EquipmentId_EntryDate_ExitDate",
                table: "EquipmentProjectUsages",
                columns: new[] { "EquipmentId", "EntryDate", "ExitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentProjectUsages_LeaseAgreementId",
                table: "EquipmentProjectUsages",
                column: "LeaseAgreementId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentProjectUsages_LegalEntityId",
                table: "EquipmentProjectUsages",
                column: "LegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentProjectUsages_ProjectId",
                table: "EquipmentProjectUsages",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSettlementAdjustments_SettlementId",
                table: "EquipmentSettlementAdjustments",
                column: "SettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSettlements_PayableEntryId",
                table: "EquipmentSettlements",
                column: "PayableEntryId",
                unique: true,
                filter: "[PayableEntryId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSettlements_UsageId",
                table: "EquipmentSettlements",
                column: "UsageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentWorkPeriods_UsageId_StartDate_EndDate",
                table: "EquipmentWorkPeriods",
                columns: new[] { "UsageId", "StartDate", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_OfflineEquipmentUsageSyncs_EquipmentProjectUsageId",
                table: "OfflineEquipmentUsageSyncs",
                column: "EquipmentProjectUsageId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineEquipmentUsageSyncs_UserId_ClientDraftId",
                table: "OfflineEquipmentUsageSyncs",
                columns: new[] { "UserId", "ClientDraftId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipmentAdvancePayments");

            migrationBuilder.DropTable(
                name: "EquipmentMaintenanceRecords");

            migrationBuilder.DropTable(
                name: "EquipmentOwnershipHistories");

            migrationBuilder.DropTable(
                name: "EquipmentSettlementAdjustments");

            migrationBuilder.DropTable(
                name: "EquipmentWorkPeriods");

            migrationBuilder.DropTable(
                name: "OfflineEquipmentUsageSyncs");

            migrationBuilder.DropTable(
                name: "EquipmentSettlements");

            migrationBuilder.DropTable(
                name: "EquipmentProjectUsages");

            migrationBuilder.DropTable(
                name: "EquipmentLeaseAgreements");

            migrationBuilder.DropTable(
                name: "Equipment");
        }
    }
}
