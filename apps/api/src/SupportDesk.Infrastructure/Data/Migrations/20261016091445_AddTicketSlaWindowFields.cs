using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SupportDesk.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketSlaWindowFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "SlaWindowHours",
                table: "Tickets",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SlaWindowStartedAtUtc",
                table: "Tickets",
                type: "datetime2(3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SlaWindowHours",
                table: "Tickets");

            migrationBuilder.DropColumn(
                name: "SlaWindowStartedAtUtc",
                table: "Tickets");
        }
    }
}
