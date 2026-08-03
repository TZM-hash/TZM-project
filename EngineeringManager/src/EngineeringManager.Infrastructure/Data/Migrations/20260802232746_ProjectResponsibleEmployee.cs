using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProjectResponsibleEmployee : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ResponsibleEmployeeId",
                table: "Projects",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsProjectResponsible",
                table: "Employees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_ResponsibleEmployeeId",
                table: "Projects",
                column: "ResponsibleEmployeeId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_Employees_ResponsibleEmployeeId",
                table: "Projects",
                column: "ResponsibleEmployeeId",
                principalTable: "Employees",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_Employees_ResponsibleEmployeeId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_ResponsibleEmployeeId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "ResponsibleEmployeeId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "IsProjectResponsible",
                table: "Employees");
        }
    }
}
