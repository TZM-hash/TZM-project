using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProjectOverviewStatusTaxEquipment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ContractSigningStatus",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE [Projects]
                SET [Stage] = CASE
                    WHEN [ArchiveStatus] = 3 OR [Stage] = 9 THEN 5
                    WHEN [ArchiveStatus] = 2 THEN 4
                    WHEN [Stage] IN (1, 2, 3) THEN 1
                    WHEN [Stage] = 4 THEN 2
                    WHEN [Stage] = 5 THEN 3
                    WHEN [Stage] IN (6, 7, 8) THEN 4
                    ELSE 1
                END;

                UPDATE [Projects]
                SET [ContractSigningStatus] = CASE
                    WHEN EXISTS (SELECT 1 FROM [Contracts] WHERE [Contracts].[ProjectId] = [Projects].[Id]) THEN 3
                    ELSE 1
                END;

                UPDATE [InvoiceEntries]
                SET [InvoiceType] = CASE
                    WHEN [InvoiceType] LIKE N'%专%' THEN N'专票'
                    WHEN [InvoiceType] LIKE N'%普%' THEN N'普票'
                    ELSE NULL
                END;
                """);

            migrationBuilder.DropColumn(
                name: "ArchiveStatus",
                table: "Projects");

            migrationBuilder.AddColumn<bool>(
                name: "ShowInProjectOverview",
                table: "ProjectConstructionRecords",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ProjectTaxConfigurationId",
                table: "InvoiceEntries",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProjectTaxConfigurations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaxRate = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: false),
                    InvoiceType = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectTaxConfigurations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectTaxConfigurations_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InvoiceEntries_ProjectTaxConfigurationId",
                table: "InvoiceEntries",
                column: "ProjectTaxConfigurationId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectTaxConfigurations_ProjectId_TaxRate_InvoiceType",
                table: "ProjectTaxConfigurations",
                columns: ["ProjectId", "TaxRate", "InvoiceType"],
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_InvoiceEntries_ProjectTaxConfigurations_ProjectTaxConfigurationId",
                table: "InvoiceEntries",
                column: "ProjectTaxConfigurationId",
                principalTable: "ProjectTaxConfigurations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvoiceEntries_ProjectTaxConfigurations_ProjectTaxConfigurationId",
                table: "InvoiceEntries");

            migrationBuilder.DropTable(
                name: "ProjectTaxConfigurations");

            migrationBuilder.DropIndex(
                name: "IX_InvoiceEntries_ProjectTaxConfigurationId",
                table: "InvoiceEntries");

            migrationBuilder.DropColumn(
                name: "ShowInProjectOverview",
                table: "ProjectConstructionRecords");

            migrationBuilder.DropColumn(
                name: "ProjectTaxConfigurationId",
                table: "InvoiceEntries");

            migrationBuilder.AddColumn<int>(
                name: "ArchiveStatus",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql(
                """
                UPDATE [Projects]
                SET [ArchiveStatus] = CASE WHEN [Stage] = 5 THEN 3 ELSE 1 END,
                    [Stage] = CASE
                        WHEN [Stage] = 1 THEN 3
                        WHEN [Stage] = 2 THEN 4
                        WHEN [Stage] = 3 THEN 5
                        WHEN [Stage] = 4 THEN 7
                        WHEN [Stage] = 5 THEN 9
                        ELSE 3
                    END;
                """);

            migrationBuilder.DropColumn(
                name: "ContractSigningStatus",
                table: "Projects");
        }
    }
}
