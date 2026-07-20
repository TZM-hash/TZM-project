using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProjectRecordAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ContractLineItemId",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceCashEntryId",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceInvoiceId",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceSettlementId",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectConstructionRecordId",
                table: "Attachments",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ContractLineItemId",
                table: "Attachments",
                column: "ContractLineItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_FinanceCashEntryId",
                table: "Attachments",
                column: "FinanceCashEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_FinanceInvoiceId",
                table: "Attachments",
                column: "FinanceInvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_FinanceSettlementId",
                table: "Attachments",
                column: "FinanceSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ProjectConstructionRecordId",
                table: "Attachments",
                column: "ProjectConstructionRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_ContractLineItems_ContractLineItemId",
                table: "Attachments",
                column: "ContractLineItemId",
                principalTable: "ContractLineItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_FinanceCashEntries_FinanceCashEntryId",
                table: "Attachments",
                column: "FinanceCashEntryId",
                principalTable: "FinanceCashEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_FinanceInvoices_FinanceInvoiceId",
                table: "Attachments",
                column: "FinanceInvoiceId",
                principalTable: "FinanceInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_FinanceSettlements_FinanceSettlementId",
                table: "Attachments",
                column: "FinanceSettlementId",
                principalTable: "FinanceSettlements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_ProjectConstructionRecords_ProjectConstructionRecordId",
                table: "Attachments",
                column: "ProjectConstructionRecordId",
                principalTable: "ProjectConstructionRecords",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_ContractLineItems_ContractLineItemId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_FinanceCashEntries_FinanceCashEntryId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_FinanceInvoices_FinanceInvoiceId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_FinanceSettlements_FinanceSettlementId",
                table: "Attachments");

            migrationBuilder.DropForeignKey(
                name: "FK_Attachments_ProjectConstructionRecords_ProjectConstructionRecordId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_ContractLineItemId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_FinanceCashEntryId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_FinanceInvoiceId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_FinanceSettlementId",
                table: "Attachments");

            migrationBuilder.DropIndex(
                name: "IX_Attachments_ProjectConstructionRecordId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ContractLineItemId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "FinanceCashEntryId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "FinanceInvoiceId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "FinanceSettlementId",
                table: "Attachments");

            migrationBuilder.DropColumn(
                name: "ProjectConstructionRecordId",
                table: "Attachments");
        }
    }
}
