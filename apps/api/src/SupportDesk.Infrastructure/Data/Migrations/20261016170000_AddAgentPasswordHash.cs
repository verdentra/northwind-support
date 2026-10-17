using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupportDesk.Infrastructure.Data;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations;

[DbContext(typeof(SupportDbContext))]
[Migration("20261016170000_AddAgentPasswordHash")]
public partial class AddAgentPasswordHash : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PasswordHash",
            table: "Agents",
            type: "nvarchar(512)",
            maxLength: 512,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PasswordHash",
            table: "Agents");
    }
}
