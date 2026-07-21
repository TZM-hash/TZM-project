using EngineeringManager.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations;

[DbContext(typeof(ApplicationDbContext))]
[Migration("20260722120000_AddFinanceSettlementDueDate")]
public sealed class AddFinanceSettlementDueDate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateOnly>(
            name: "DueDate",
            table: "FinanceSettlements",
            type: "date",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProjectId",
            table: "FinanceInvoices",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ContractId",
            table: "FinanceInvoices",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ProjectId",
            table: "FinanceCashEntries",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "ContractId",
            table: "FinanceCashEntries",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "PayrollPaymentId",
            table: "Attachments",
            type: "uniqueidentifier",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_FinanceInvoices_ProjectId",
            table: "FinanceInvoices",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_FinanceInvoices_ContractId",
            table: "FinanceInvoices",
            column: "ContractId");

        migrationBuilder.CreateIndex(
            name: "IX_FinanceCashEntries_ProjectId",
            table: "FinanceCashEntries",
            column: "ProjectId");

        migrationBuilder.CreateIndex(
            name: "IX_FinanceCashEntries_ContractId",
            table: "FinanceCashEntries",
            column: "ContractId");

        migrationBuilder.CreateIndex(
            name: "IX_Attachments_PayrollPaymentId",
            table: "Attachments",
            column: "PayrollPaymentId");

        migrationBuilder.AddForeignKey(
            name: "FK_FinanceInvoices_Projects_ProjectId",
            table: "FinanceInvoices",
            column: "ProjectId",
            principalTable: "Projects",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_FinanceInvoices_Contracts_ContractId",
            table: "FinanceInvoices",
            column: "ContractId",
            principalTable: "Contracts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_FinanceCashEntries_Projects_ProjectId",
            table: "FinanceCashEntries",
            column: "ProjectId",
            principalTable: "Projects",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_FinanceCashEntries_Contracts_ContractId",
            table: "FinanceCashEntries",
            column: "ContractId",
            principalTable: "Contracts",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);

        migrationBuilder.AddForeignKey(
            name: "FK_Attachments_PayrollPayments_PayrollPaymentId",
            table: "Attachments",
            column: "PayrollPaymentId",
            principalTable: "PayrollPayments",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "DueDate",
            table: "FinanceSettlements");

        migrationBuilder.DropIndex("IX_FinanceInvoices_ProjectId", "FinanceInvoices");
        migrationBuilder.DropIndex("IX_FinanceInvoices_ContractId", "FinanceInvoices");
        migrationBuilder.DropIndex("IX_FinanceCashEntries_ProjectId", "FinanceCashEntries");
        migrationBuilder.DropIndex("IX_FinanceCashEntries_ContractId", "FinanceCashEntries");
        migrationBuilder.DropForeignKey("FK_FinanceInvoices_Projects_ProjectId", "FinanceInvoices");
        migrationBuilder.DropForeignKey("FK_FinanceInvoices_Contracts_ContractId", "FinanceInvoices");
        migrationBuilder.DropForeignKey("FK_FinanceCashEntries_Projects_ProjectId", "FinanceCashEntries");
        migrationBuilder.DropForeignKey("FK_FinanceCashEntries_Contracts_ContractId", "FinanceCashEntries");
        migrationBuilder.DropForeignKey("FK_Attachments_PayrollPayments_PayrollPaymentId", "Attachments");
        migrationBuilder.DropIndex("IX_Attachments_PayrollPaymentId", "Attachments");
        migrationBuilder.DropColumn("PayrollPaymentId", "Attachments");
        migrationBuilder.DropColumn("ProjectId", "FinanceInvoices");
        migrationBuilder.DropColumn("ContractId", "FinanceInvoices");
        migrationBuilder.DropColumn("ProjectId", "FinanceCashEntries");
        migrationBuilder.DropColumn("ContractId", "FinanceCashEntries");
    }
}
