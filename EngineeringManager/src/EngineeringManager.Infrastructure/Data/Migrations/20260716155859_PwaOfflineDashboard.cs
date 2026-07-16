using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class PwaOfflineDashboard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfflineDraftSyncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ClientDraftId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastOperationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StageResultId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LastServerVersion = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    LastAttemptAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LastSyncedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineDraftSyncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfflineDraftSyncs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfflineDraftSyncs_StageResults_StageResultId",
                        column: x => x.StageResultId,
                        principalTable: "StageResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "OfflineAttachmentSyncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfflineDraftSyncId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineAttachmentSyncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfflineAttachmentSyncs_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfflineAttachmentSyncs_OfflineDraftSyncs_OfflineDraftSyncId",
                        column: x => x.OfflineDraftSyncId,
                        principalTable: "OfflineDraftSyncs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfflineAttachmentSyncs_AttachmentId",
                table: "OfflineAttachmentSyncs",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineAttachmentSyncs_OfflineDraftSyncId_ClientAttachmentId",
                table: "OfflineAttachmentSyncs",
                columns: new[] { "OfflineDraftSyncId", "ClientAttachmentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OfflineDraftSyncs_StageResultId",
                table: "OfflineDraftSyncs",
                column: "StageResultId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineDraftSyncs_Status_UpdatedAt",
                table: "OfflineDraftSyncs",
                columns: new[] { "Status", "UpdatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_OfflineDraftSyncs_UserId_ClientDraftId",
                table: "OfflineDraftSyncs",
                columns: new[] { "UserId", "ClientDraftId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfflineAttachmentSyncs");

            migrationBuilder.DropTable(
                name: "OfflineDraftSyncs");
        }
    }
}
