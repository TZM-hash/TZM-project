using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EngineeringManager.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class UnifiedProjectQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccountingLabel",
                table: "ContractLineItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Quantity",
                table: "ContractLineItems",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresInvoice",
                table: "ContractLineItems",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitPrice",
                table: "ContractLineItems",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE lineItem
                SET Quantity = CASE
                        WHEN project.Stage IN (5, 6) THEN COALESCE(lineItem.SettledQuantity, lineItem.EstimatedQuantity)
                        ELSE COALESCE(lineItem.EstimatedQuantity, lineItem.SettledQuantity)
                    END,
                    UnitPrice = CASE
                        WHEN project.Stage IN (5, 6) THEN COALESCE(lineItem.SettledUnitPrice, lineItem.EstimatedUnitPrice)
                        ELSE COALESCE(lineItem.EstimatedUnitPrice, lineItem.SettledUnitPrice)
                    END,
                    AccountingLabel = CASE WHEN project.Stage IN (5, 6) THEN N'结算' ELSE N'暂估' END,
                    RequiresInvoice = 1
                FROM ContractLineItems AS lineItem
                INNER JOIN Contracts AS contract ON contract.Id = lineItem.ContractId
                INNER JOIN Projects AS project ON project.Id = contract.ProjectId;
                """);

            migrationBuilder.DropColumn(name: "EstimatedQuantity", table: "ContractLineItems");
            migrationBuilder.DropColumn(name: "EstimatedUnitPrice", table: "ContractLineItems");
            migrationBuilder.DropColumn(name: "SettledQuantity", table: "ContractLineItems");
            migrationBuilder.DropColumn(name: "SettledUnitPrice", table: "ContractLineItems");
            migrationBuilder.DropColumn(name: "IsSettlementConfirmed", table: "ContractLineItems");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedQuantity",
                table: "ContractLineItems",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedUnitPrice",
                table: "ContractLineItems",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsSettlementConfirmed",
                table: "ContractLineItems",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(name: "SettledQuantity", table: "ContractLineItems", type: "decimal(18,4)", precision: 18, scale: 4, nullable: true);
            migrationBuilder.AddColumn<decimal>(name: "SettledUnitPrice", table: "ContractLineItems", type: "decimal(18,4)", precision: 18, scale: 4, nullable: true);

            migrationBuilder.Sql("""
                UPDATE ContractLineItems
                SET EstimatedQuantity = Quantity,
                    EstimatedUnitPrice = UnitPrice,
                    SettledQuantity = Quantity,
                    SettledUnitPrice = UnitPrice,
                    IsSettlementConfirmed = CASE WHEN AccountingLabel = N'结算' THEN 1 ELSE 0 END;
                """);

            migrationBuilder.DropColumn(name: "Quantity", table: "ContractLineItems");
            migrationBuilder.DropColumn(name: "UnitPrice", table: "ContractLineItems");
            migrationBuilder.DropColumn(name: "RequiresInvoice", table: "ContractLineItems");
            migrationBuilder.DropColumn(name: "AccountingLabel", table: "ContractLineItems");
        }
    }
}
