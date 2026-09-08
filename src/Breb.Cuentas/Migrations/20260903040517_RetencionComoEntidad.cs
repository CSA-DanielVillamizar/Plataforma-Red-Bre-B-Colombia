using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Breb.Cuentas.Migrations
{
    /// <inheritdoc />
    public partial class RetencionComoEntidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "Cuentas",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "Retenciones",
                columns: table => new
                {
                    TransferenciaId = table.Column<Guid>(type: "uuid", nullable: false),
                    CuentaId = table.Column<Guid>(type: "uuid", nullable: false),
                    MontoUVB = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    CreadaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Liberada = table.Column<bool>(type: "boolean", nullable: false),
                    LiberadaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Retenciones", x => x.TransferenciaId);
                    table.ForeignKey(
                        name: "FK_Retenciones_Cuentas_CuentaId",
                        column: x => x.CuentaId,
                        principalTable: "Cuentas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Retenciones_CuentaId_Liberada",
                table: "Retenciones",
                columns: new[] { "CuentaId", "Liberada" },
                filter: "\"Liberada\" = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Retenciones");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "Cuentas");
        }
    }
}
