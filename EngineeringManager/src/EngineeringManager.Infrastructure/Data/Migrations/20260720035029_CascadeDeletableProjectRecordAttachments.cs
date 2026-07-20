using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CascadeDeletableProjectRecordAttachments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_ContractLineItems_ContractLineItemId",
                table: "Attachments",
                column: "ContractLineItemId",
                principalTable: "ContractLineItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_FinanceCashEntries_FinanceCashEntryId",
                table: "Attachments",
                column: "FinanceCashEntryId",
                principalTable: "FinanceCashEntries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_FinanceInvoices_FinanceInvoiceId",
                table: "Attachments",
                column: "FinanceInvoiceId",
                principalTable: "FinanceInvoices",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Attachments_FinanceSettlements_FinanceSettlementId",
                table: "Attachments",
                column: "FinanceSettlementId",
                principalTable: "FinanceSettlements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
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
        }
    }
}
