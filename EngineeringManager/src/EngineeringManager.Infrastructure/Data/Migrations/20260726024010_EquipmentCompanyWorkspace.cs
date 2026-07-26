using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentCompanyWorkspace : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ManagingLegalEntityId",
                table: "Equipment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "QualificationAttachmentId",
                table: "Equipment",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualificationCertificateNumber",
                table: "Equipment",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "QualificationExpiresOn",
                table: "Equipment",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "QualificationIssuedOn",
                table: "Equipment",
                type: "date",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE equipment
                SET ManagingLegalEntityId = OwnerLegalEntityId
                FROM Equipment AS equipment
                WHERE equipment.ManagingLegalEntityId IS NULL
                  AND equipment.OwnerLegalEntityId IS NOT NULL;

                UPDATE equipment
                SET ManagingLegalEntityId = recent.LegalEntityId
                FROM Equipment AS equipment
                OUTER APPLY (
                    SELECT TOP (1) usage.LegalEntityId
                    FROM EquipmentProjectUsages AS usage
                    WHERE usage.EquipmentId = equipment.Id
                    ORDER BY usage.EntryDate DESC, usage.Id DESC
                ) AS recent
                WHERE equipment.ManagingLegalEntityId IS NULL
                  AND recent.LegalEntityId IS NOT NULL;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_ManagingLegalEntityId",
                table: "Equipment",
                column: "ManagingLegalEntityId");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_QualificationAttachmentId",
                table: "Equipment",
                column: "QualificationAttachmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_Attachments_QualificationAttachmentId",
                table: "Equipment",
                column: "QualificationAttachmentId",
                principalTable: "Attachments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Equipment_LegalEntities_ManagingLegalEntityId",
                table: "Equipment",
                column: "ManagingLegalEntityId",
                principalTable: "LegalEntities",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_Attachments_QualificationAttachmentId",
                table: "Equipment");

            migrationBuilder.DropForeignKey(
                name: "FK_Equipment_LegalEntities_ManagingLegalEntityId",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_ManagingLegalEntityId",
                table: "Equipment");

            migrationBuilder.DropIndex(
                name: "IX_Equipment_QualificationAttachmentId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "ManagingLegalEntityId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "QualificationAttachmentId",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "QualificationCertificateNumber",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "QualificationExpiresOn",
                table: "Equipment");

            migrationBuilder.DropColumn(
                name: "QualificationIssuedOn",
                table: "Equipment");
        }
    }
}
