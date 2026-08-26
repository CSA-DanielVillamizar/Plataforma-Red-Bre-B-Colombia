using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Breb.Cuentas.Migrations
{
    /// <inheritdoc />
    public partial class AgregaSagaTransferencia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_OutboxState_BusName_Created",
                table: "OutboxState");

            migrationBuilder.DropColumn(
                name: "BusName",
                table: "OutboxState");

            migrationBuilder.CreateTable(
                name: "TransferenciaSagas",
                columns: table => new
                {
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentState = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    CuentaOrigenId = table.Column<Guid>(type: "uuid", nullable: false),
                    MontoUVB = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    IniciadaEn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MotivoCompensacion = table.Column<string>(type: "text", nullable: true),
                    TimeoutTokenId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferenciaSagas", x => x.CorrelationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_TransferenciaSagas_CurrentState",
                table: "TransferenciaSagas",
                column: "CurrentState");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TransferenciaSagas");

            migrationBuilder.DropIndex(
                name: "IX_OutboxState_Created",
                table: "OutboxState");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                table: "OutboxMessage");

            migrationBuilder.DropIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                table: "OutboxMessage");

            migrationBuilder.AddColumn<string>(
                name: "BusName",
                table: "OutboxState",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_BusName_Created",
                table: "OutboxState",
                columns: new[] { "BusName", "Created" });
        }
    }
}
