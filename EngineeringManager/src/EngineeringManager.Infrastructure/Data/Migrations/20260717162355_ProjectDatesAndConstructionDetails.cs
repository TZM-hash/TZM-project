using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProjectDatesAndConstructionDetails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "ActualCompletionDate",
                table: "Projects",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "ActualStartDate",
                table: "Projects",
                type: "date",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectConstructionRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RecordType = table.Column<int>(type: "int", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CrewBusinessPartnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransferFromProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TransferToProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PreviousRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    NextRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ExitDate = table.Column<DateOnly>(type: "date", nullable: true),
                    StopDays = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDraft = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectConstructionRecords", x => x.Id);
                    table.CheckConstraint("CK_ProjectConstructionRecords_Subject", "([RecordType] = 1 AND [EquipmentId] IS NOT NULL AND [CrewBusinessPartnerId] IS NULL) OR ([RecordType] = 2 AND [EquipmentId] IS NULL AND [CrewBusinessPartnerId] IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_ProjectConstructionRecords_BusinessPartners_CrewBusinessPartnerId",
                        column: x => x.CrewBusinessPartnerId,
                        principalTable: "BusinessPartners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectConstructionRecords_Equipment_EquipmentId",
                        column: x => x.EquipmentId,
                        principalTable: "Equipment",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectConstructionRecords_ProjectConstructionRecords_NextRecordId",
                        column: x => x.NextRecordId,
                        principalTable: "ProjectConstructionRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectConstructionRecords_ProjectConstructionRecords_PreviousRecordId",
                        column: x => x.PreviousRecordId,
                        principalTable: "ProjectConstructionRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectConstructionRecords_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProjectConstructionRecords_Projects_TransferFromProjectId",
                        column: x => x.TransferFromProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectConstructionRecords_Projects_TransferToProjectId",
                        column: x => x.TransferToProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConstructionRecords_CrewBusinessPartnerId",
                table: "ProjectConstructionRecords",
                column: "CrewBusinessPartnerId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConstructionRecords_EquipmentId",
                table: "ProjectConstructionRecords",
                column: "EquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConstructionRecords_NextRecordId",
                table: "ProjectConstructionRecords",
                column: "NextRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConstructionRecords_PreviousRecordId",
                table: "ProjectConstructionRecords",
                column: "PreviousRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConstructionRecords_ProjectId_RecordType_EntryDate",
                table: "ProjectConstructionRecords",
                columns: new[] { "ProjectId", "RecordType", "EntryDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConstructionRecords_TransferFromProjectId",
                table: "ProjectConstructionRecords",
                column: "TransferFromProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectConstructionRecords_TransferToProjectId",
                table: "ProjectConstructionRecords",
                column: "TransferToProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectConstructionRecords");

            migrationBuilder.DropColumn(
                name: "ActualCompletionDate",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ActualStartDate",
                table: "Projects");
        }
    }
}
