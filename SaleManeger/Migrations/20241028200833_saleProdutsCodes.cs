using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaleManeger.Migrations
{
    /// <inheritdoc />
    public partial class saleProdutsCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ProductCode",
                table: "SalesProducts",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProductName",
                table: "SalesProducts",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ProductCode",
                table: "SalesProducts");

            migrationBuilder.DropColumn(
                name: "ProductName",
                table: "SalesProducts");
        }
    }
}
