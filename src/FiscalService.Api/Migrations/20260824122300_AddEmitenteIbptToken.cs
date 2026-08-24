using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FiscalService.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddEmitenteIbptToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ibpt_token_protegido",
                table: "emitentes",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ibpt_token_protegido",
                table: "emitentes");
        }
    }
}
