using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1861 // Generated migration arrays

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSavedDataViews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SavedDataViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PageKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    FilterJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    ColumnJson = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    SortKey = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    SortDescending = table.Column<bool>(type: "bit", nullable: false),
                    RowDensity = table.Column<int>(type: "int", nullable: false),
                    PageSize = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedDataViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedDataViews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SavedDataViews_UserId_PageKey_IsDefault",
                table: "SavedDataViews",
                columns: new[] { "UserId", "PageKey", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedDataViews_UserId_PageKey_Name",
                table: "SavedDataViews",
                columns: new[] { "UserId", "PageKey", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SavedDataViews");
        }
    }
}
