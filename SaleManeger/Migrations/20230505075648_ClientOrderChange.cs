using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaleManeger.Migrations
{
    /// <inheritdoc />
    public partial class ClientOrderChange : Migration
    {
        #region Protected Methods

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsReserved",
                table: "ClientsOrders");

            migrationBuilder.DropColumn(
                name: "Value",
                table: "ClientsOrders");

            migrationBuilder.CreateIndex(
                name: "IX_ClientsOrders_ClientID",
                table: "ClientsOrders",
                column: "ClientID");

            migrationBuilder.CreateIndex(
                name: "IX_ClientsOrders_ProductID",
                table: "ClientsOrders",
                column: "ProductID");

            migrationBuilder.CreateIndex(
                name: "IX_ClientsOrders_SaleID",
                table: "ClientsOrders",
                column: "SaleID");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientsOrders_Clients_ClientID",
                table: "ClientsOrders",
                column: "ClientID",
                principalTable: "Clients",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientsOrders_Products_ProductID",
                table: "ClientsOrders",
                column: "ProductID",
                principalTable: "Products",
                principalColumn: "ID");

            migrationBuilder.AddForeignKey(
                name: "FK_ClientsOrders_Sales_SaleID",
                table: "ClientsOrders",
                column: "SaleID",
                principalTable: "Sales",
                principalColumn: "SaleID");
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ClientsOrders_Clients_ClientID",
                table: "ClientsOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientsOrders_Products_ProductID",
                table: "ClientsOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ClientsOrders_Sales_SaleID",
                table: "ClientsOrders");

            migrationBuilder.DropIndex(
                name: "IX_ClientsOrders_ClientID",
                table: "ClientsOrders");

            migrationBuilder.DropIndex(
                name: "IX_ClientsOrders_ProductID",
                table: "ClientsOrders");

            migrationBuilder.DropIndex(
                name: "IX_ClientsOrders_SaleID",
                table: "ClientsOrders");

            migrationBuilder.AddColumn<bool>(
                name: "IsReserved",
                table: "ClientsOrders",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Value",
                table: "ClientsOrders",
                type: "TEXT",
                nullable: true);
        }

        #endregion Protected Methods
    }
}