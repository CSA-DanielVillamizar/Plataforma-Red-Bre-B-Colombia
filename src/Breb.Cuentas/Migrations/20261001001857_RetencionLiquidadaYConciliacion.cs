using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Breb.Cuentas.Migrations
{
    /// <inheritdoc />
    public partial class RetencionLiquidadaYConciliacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Liquidada",
                table: "Retenciones",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "LiquidadaEn",
                table: "Retenciones",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Retenciones_Pendientes",
                table: "Retenciones",
                column: "CreadaEn",
                filter: "\"Liberada\" = false AND \"Liquidada\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Retenciones_Pendientes",
                table: "Retenciones");

            migrationBuilder.DropColumn(
                name: "Liquidada",
                table: "Retenciones");

            migrationBuilder.DropColumn(
                name: "LiquidadaEn",
                table: "Retenciones");
        }
    }
}
