using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FiscalService.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "emissao_logs",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    modelo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    serie = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    numero = table.Column<int>(type: "integer", nullable: false),
                    chave_acesso = table.Column<string>(type: "character varying(44)", maxLength: 44, nullable: true),
                    protocolo = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    codigo_status = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: true),
                    mensagem_status = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ambiente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    data_emissao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    data_processamento = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xml_path = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emissao_logs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "numeracoes_sequenciais",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    modelo = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    serie = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    ultimo_numero = table.Column<int>(type: "integer", nullable: false),
                    ultima_atualizacao = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_numeracoes_sequenciais", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_emissao_logs_chave_acesso",
                table: "emissao_logs",
                column: "chave_acesso");

            migrationBuilder.CreateIndex(
                name: "ix_emissao_logs_cnpj_data",
                table: "emissao_logs",
                columns: new[] { "cnpj", "data_emissao" });

            migrationBuilder.CreateIndex(
                name: "ix_emissao_logs_status",
                table: "emissao_logs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_numeracoes_cnpj_modelo_serie",
                table: "numeracoes_sequenciais",
                columns: new[] { "cnpj", "modelo", "serie" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "emissao_logs");

            migrationBuilder.DropTable(
                name: "numeracoes_sequenciais");
        }
    }
}
