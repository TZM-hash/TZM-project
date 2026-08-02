using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class CentralFinanceLinksForEquipmentAndPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FinanceSettlementId",
                table: "PayrollCrewAllocations",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FinanceSettlementId",
                table: "EquipmentSettlements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayrollCrewAllocations_FinanceSettlementId",
                table: "PayrollCrewAllocations",
                column: "FinanceSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentSettlements_FinanceSettlementId",
                table: "EquipmentSettlements",
                column: "FinanceSettlementId",
                unique: true,
                filter: "[FinanceSettlementId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentSettlements_FinanceSettlements_FinanceSettlementId",
                table: "EquipmentSettlements",
                column: "FinanceSettlementId",
                principalTable: "FinanceSettlements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PayrollCrewAllocations_FinanceSettlements_FinanceSettlementId",
                table: "PayrollCrewAllocations",
                column: "FinanceSettlementId",
                principalTable: "FinanceSettlements",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentSettlements_FinanceSettlements_FinanceSettlementId",
                table: "EquipmentSettlements");

            migrationBuilder.DropForeignKey(
                name: "FK_PayrollCrewAllocations_FinanceSettlements_FinanceSettlementId",
                table: "PayrollCrewAllocations");

            migrationBuilder.DropIndex(
                name: "IX_PayrollCrewAllocations_FinanceSettlementId",
                table: "PayrollCrewAllocations");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentSettlements_FinanceSettlementId",
                table: "EquipmentSettlements");

            migrationBuilder.DropColumn(
                name: "FinanceSettlementId",
                table: "PayrollCrewAllocations");

            migrationBuilder.DropColumn(
                name: "FinanceSettlementId",
                table: "EquipmentSettlements");
        }
    }
}
