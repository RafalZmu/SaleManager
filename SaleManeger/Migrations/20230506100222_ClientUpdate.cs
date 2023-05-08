using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SaleManeger.Migrations
{
    /// <inheritdoc />
    public partial class ClientUpdate : Migration
    {
        #region Protected Methods

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClientID",
                table: "Products",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_ClientID",
                table: "Products",
                column: "ClientID");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Clients_ClientID",
                table: "Products",
                column: "ClientID",
                principalTable: "Clients",
                principalColumn: "ID");
        }

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Clients_ClientID",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_ClientID",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "ClientID",
                table: "Products");
        }

        #endregion Protected Methods
    }
}