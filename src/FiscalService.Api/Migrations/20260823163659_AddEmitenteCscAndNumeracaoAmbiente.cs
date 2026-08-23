using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiscalService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmitenteCscAndNumeracaoAmbiente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_numeracoes_cnpj_modelo_serie",
                table: "numeracoes_sequenciais");

            migrationBuilder.AddColumn<string>(
                name: "ambiente",
                table: "numeracoes_sequenciais",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Homologacao");

            migrationBuilder.AddColumn<string>(
                name: "csc_homologacao_protegido",
                table: "emitentes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "csc_producao_protegido",
                table: "emitentes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "id_csc_homologacao",
                table: "emitentes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "id_csc_producao",
                table: "emitentes",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_numeracoes_cnpj_modelo_serie_ambiente",
                table: "numeracoes_sequenciais",
                columns: new[] { "cnpj", "modelo", "serie", "ambiente" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_numeracoes_cnpj_modelo_serie_ambiente",
                table: "numeracoes_sequenciais");

            migrationBuilder.DropColumn(
                name: "ambiente",
                table: "numeracoes_sequenciais");

            migrationBuilder.DropColumn(
                name: "csc_homologacao_protegido",
                table: "emitentes");

            migrationBuilder.DropColumn(
                name: "csc_producao_protegido",
                table: "emitentes");

            migrationBuilder.DropColumn(
                name: "id_csc_homologacao",
                table: "emitentes");

            migrationBuilder.DropColumn(
                name: "id_csc_producao",
                table: "emitentes");

            migrationBuilder.CreateIndex(
                name: "ix_numeracoes_cnpj_modelo_serie",
                table: "numeracoes_sequenciais",
                columns: new[] { "cnpj", "modelo", "serie" },
                unique: true);
        }
    }
}
