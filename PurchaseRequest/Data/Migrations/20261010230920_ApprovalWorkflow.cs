using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PurchaseRequest.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ApprovalSteps_RequestId_Sequence",
                table: "ApprovalSteps");

            migrationBuilder.AddColumn<string>(
                name: "Roles",
                table: "Users",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "Round",
                table: "ApprovalSteps",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    To = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Body = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_RequestId_Round_Sequence",
                table: "ApprovalSteps",
                columns: new[] { "RequestId", "Round", "Sequence" });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CreatedAt",
                table: "OutboxMessages",
                column: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_ApprovalSteps_RequestId_Round_Sequence",
                table: "ApprovalSteps");

            migrationBuilder.DropColumn(
                name: "Roles",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Round",
                table: "ApprovalSteps");

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalSteps_RequestId_Sequence",
                table: "ApprovalSteps",
                columns: new[] { "RequestId", "Sequence" });
        }
    }
}
