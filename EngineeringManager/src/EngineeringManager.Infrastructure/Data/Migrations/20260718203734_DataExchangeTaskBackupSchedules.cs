using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
#pragma warning disable CA1861

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class DataExchangeTaskBackupSchedules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "ImportBatches",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<bool>(
                name: "IsRetained",
                table: "BackupTasks",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "BackupTasks",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "LocalStatus",
                table: "BackupTasks",
                type: "int",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<int>(
                name: "NasStatus",
                table: "BackupTasks",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "PackagePath",
                table: "BackupTasks",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Sha256",
                table: "BackupTasks",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BackupSchedules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Enabled = table.Column<bool>(type: "bit", nullable: false),
                    Mode = table.Column<int>(type: "int", nullable: false),
                    IntervalMinutes = table.Column<int>(type: "int", nullable: true),
                    FixedTime = table.Column<TimeOnly>(type: "time", nullable: true),
                    TimeZoneId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LocalTargetDirectory = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    NasTargetDirectory = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    LocalRetentionCount = table.Column<int>(type: "int", nullable: false),
                    NasRetentionCount = table.Column<int>(type: "int", nullable: false),
                    AlertOnFailure = table.Column<bool>(type: "bit", nullable: false),
                    LastRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    NextRunAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackupSchedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataExchangeTasks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Direction = table.Column<int>(type: "int", nullable: false),
                    DatasetsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SelectedFieldsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FilterJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    PackageFormat = table.Column<int>(type: "int", nullable: false),
                    IncludeAttachments = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ContentType = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResultContent = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataExchangeTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImportMappingTemplates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Dataset = table.Column<int>(type: "int", nullable: false),
                    Scope = table.Column<int>(type: "int", nullable: false),
                    DatasetVersion = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    MappingJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportMappingTemplates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BackupSchedules_Kind",
                table: "BackupSchedules",
                column: "Kind",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DataExchangeTasks_Status",
                table: "DataExchangeTasks",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_DataExchangeTasks_UserId_CreatedAt",
                table: "DataExchangeTasks",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportMappingTemplates_Dataset_Scope",
                table: "ImportMappingTemplates",
                columns: new[] { "Dataset", "Scope" });

            migrationBuilder.CreateIndex(
                name: "IX_ImportMappingTemplates_OwnerUserId_Dataset_Name",
                table: "ImportMappingTemplates",
                columns: new[] { "OwnerUserId", "Dataset", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackupSchedules");

            migrationBuilder.DropTable(
                name: "DataExchangeTasks");

            migrationBuilder.DropTable(
                name: "ImportMappingTemplates");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "ImportBatches");

            migrationBuilder.DropColumn(
                name: "IsRetained",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "LocalStatus",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "NasStatus",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "PackagePath",
                table: "BackupTasks");

            migrationBuilder.DropColumn(
                name: "Sha256",
                table: "BackupTasks");
        }
    }
}
#pragma warning restore CA1861
