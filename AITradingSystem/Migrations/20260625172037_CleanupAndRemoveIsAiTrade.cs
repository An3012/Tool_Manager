using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AITradingSystem.Migrations
{
    /// <inheritdoc />
    public partial class CleanupAndRemoveIsAiTrade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Xóa cột IsAiTrade từ TradePositions
            migrationBuilder.DropColumn(
                name: "IsAiTrade",
                table: "TradePositions");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Thêm lại cột IsAiTrade
            migrationBuilder.AddColumn<bool>(
                name: "IsAiTrade",
                table: "TradePositions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }
    }
}
