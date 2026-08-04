using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProjectResponsibleEmployeeLinks : Migration
    {
        private static readonly string[] ProjectEmployeeIndexColumns = ["ProjectId", "EmployeeId"];
        private static readonly string[] ProjectSortIndexColumns = ["ProjectId", "SortOrder"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectResponsibleEmployees",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ConcurrencyStamp = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectResponsibleEmployees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectResponsibleEmployees_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectResponsibleEmployees_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResponsibleEmployees_EmployeeId",
                table: "ProjectResponsibleEmployees",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResponsibleEmployees_ProjectId_EmployeeId",
                table: "ProjectResponsibleEmployees",
                columns: ProjectEmployeeIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjectResponsibleEmployees_ProjectId_SortOrder",
                table: "ProjectResponsibleEmployees",
                columns: ProjectSortIndexColumns);

            migrationBuilder.Sql("""
                INSERT INTO [ProjectResponsibleEmployees]
                    ([Id], [ProjectId], [EmployeeId], [SortOrder], [IsPrimary], [CreatedAt], [UpdatedAt], [ConcurrencyStamp])
                SELECT NEWID(), project.[Id], project.[ResponsibleEmployeeId], 0, 1,
                    TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
                    TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
                    NEWID()
                FROM [Projects] project
                INNER JOIN [Employees] employee ON employee.[Id] = project.[ResponsibleEmployeeId]
                WHERE project.[ResponsibleEmployeeId] IS NOT NULL
                  AND NOT EXISTS (
                      SELECT 1
                      FROM [ProjectResponsibleEmployees] link
                      WHERE link.[ProjectId] = project.[Id]
                        AND link.[EmployeeId] = project.[ResponsibleEmployeeId]
                  );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProjectResponsibleEmployees");
        }
    }
}
