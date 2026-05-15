using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace FiscalService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmitentes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "emitentes",
                columns: table => new
                {
                    id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    cnpj = table.Column<string>(type: "character varying(14)", maxLength: 14, nullable: false),
                    razao_social = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    nome_fantasia = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ie = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    crt = table.Column<int>(type: "integer", nullable: false),
                    uf = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    ambiente = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    certificado_path = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    certificado_senha_protegida = table.Column<string>(type: "text", nullable: false),
                    logradouro = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    numero = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    complemento = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    bairro = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    municipio = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    codigo_municipio = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    cep = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    telefone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    ativo = table.Column<bool>(type: "boolean", nullable: false),
                    criado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    atualizado_em = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_emitentes", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_emitentes_cnpj",
                table: "emitentes",
                column: "cnpj",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "emitentes");
        }
    }
}
