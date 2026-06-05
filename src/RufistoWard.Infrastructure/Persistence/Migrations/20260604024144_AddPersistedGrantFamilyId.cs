using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RufistoWard.Tokens.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistedGrantFamilyId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "FamilyId",
                table: "PersistedGrants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_PersistedGrants_FamilyId_ConsumedAt",
                table: "PersistedGrants",
                columns: new[] { "FamilyId", "ConsumedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PersistedGrants_FamilyId_ConsumedAt",
                table: "PersistedGrants");

            migrationBuilder.DropColumn(
                name: "FamilyId",
                table: "PersistedGrants");
        }
    }
}
