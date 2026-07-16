using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Generated migration arrays

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentOfflinePhotos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OfflineEquipmentAttachmentSyncs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfflineEquipmentUsageSyncId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClientAttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OfflineEquipmentAttachmentSyncs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OfflineEquipmentAttachmentSyncs_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OfflineEquipmentAttachmentSyncs_OfflineEquipmentUsageSyncs_OfflineEquipmentUsageSyncId",
                        column: x => x.OfflineEquipmentUsageSyncId,
                        principalTable: "OfflineEquipmentUsageSyncs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OfflineEquipmentAttachmentSyncs_AttachmentId",
                table: "OfflineEquipmentAttachmentSyncs",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_OfflineEquipmentAttachmentSyncs_OfflineEquipmentUsageSyncId_ClientAttachmentId",
                table: "OfflineEquipmentAttachmentSyncs",
                columns: new[] { "OfflineEquipmentUsageSyncId", "ClientAttachmentId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OfflineEquipmentAttachmentSyncs");
        }
    }
}
