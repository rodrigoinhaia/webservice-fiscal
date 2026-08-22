using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiscalService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNfseSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "serie",
                table: "numeracoes_sequenciais",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AddColumn<string>(
                name: "email",
                table: "emitentes",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "inscricao_municipal",
                table: "emitentes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "serie",
                table: "emissao_logs",
                type: "character varying(5)",
                maxLength: 5,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3);

            migrationBuilder.AlterColumn<string>(
                name: "codigo_status",
                table: "emissao_logs",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(3)",
                oldMaxLength: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "chave_acesso",
                table: "emissao_logs",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(44)",
                oldMaxLength: 44,
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "email",
                table: "emitentes");

            migrationBuilder.DropColumn(
                name: "inscricao_municipal",
                table: "emitentes");

            migrationBuilder.AlterColumn<string>(
                name: "serie",
                table: "numeracoes_sequenciais",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5);

            migrationBuilder.AlterColumn<string>(
                name: "serie",
                table: "emissao_logs",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(5)",
                oldMaxLength: 5);

            migrationBuilder.AlterColumn<string>(
                name: "codigo_status",
                table: "emissao_logs",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(10)",
                oldMaxLength: 10,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "chave_acesso",
                table: "emissao_logs",
                type: "character varying(44)",
                maxLength: 44,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(50)",
                oldMaxLength: 50,
                oldNullable: true);
        }
    }
}
