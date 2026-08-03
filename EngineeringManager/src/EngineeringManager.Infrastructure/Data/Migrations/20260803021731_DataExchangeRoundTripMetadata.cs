using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DataExchangeRoundTripMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DatasetVersion",
                table: "ImportBatches",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "1");

            migrationBuilder.AddColumn<Guid>(
                name: "SourceExportTaskId",
                table: "ImportBatches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SourceSha256",
                table: "ImportBatches",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                table: "ImportBatches",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<string>(
                name: "DatasetVersion",
                table: "DataExchangeTasks",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "1");

            migrationBuilder.AddColumn<string>(
                name: "SourcePage",
                table: "DataExchangeTasks",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_SourceExportTaskId",
                table: "ImportBatches",
                column: "SourceExportTaskId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ImportBatches_SourceExportTaskId",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "DatasetVersion",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "SourceExportTaskId",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "SourceSha256",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "SourceType",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "DatasetVersion",
                table: "DataExchangeTasks");

            migrationBuilder.DropColumn(
                name: "SourcePage",
                table: "DataExchangeTasks");
        }
    }
}
