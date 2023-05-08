using Microsoft.EntityFrameworkCore.Migrations;
using System;

#nullable disable

namespace SaleManeger.Migrations
{
    /// <inheritdoc />
    public partial class Update : Migration
    {
        #region Protected Methods

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientsOrders");
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClientsOrders",
                columns: table => new
                {
                    ClientOrderID = table.Column<string>(type: "TEXT", nullable: false),
                    ClientID = table.Column<string>(type: "TEXT", nullable: true),
                    ProductID = table.Column<string>(type: "TEXT", nullable: true),
                    SaleID = table.Column<string>(type: "TEXT", nullable: true),
                    Date = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientsOrders", x => x.ClientOrderID);
                    table.ForeignKey(
                        name: "FK_ClientsOrders_Clients_ClientID",
                        column: x => x.ClientID,
                        principalTable: "Clients",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_ClientsOrders_Products_ProductID",
                        column: x => x.ProductID,
                        principalTable: "Products",
                        principalColumn: "ID");
                    table.ForeignKey(
                        name: "FK_ClientsOrders_Sales_SaleID",
                        column: x => x.SaleID,
                        principalTable: "Sales",
                        principalColumn: "SaleID");
                });

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
        }

        #endregion Protected Methods
    }
}