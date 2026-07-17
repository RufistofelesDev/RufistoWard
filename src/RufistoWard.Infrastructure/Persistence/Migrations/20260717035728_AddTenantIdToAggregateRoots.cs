using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RufistoWard.Tokens.Persistence.Migrations
{
    /// <inheritdoc />
/// <summary>
    /// Adds the tenant column to every aggregate root and recomposes each unique index as
    /// (TenantId, ...).
    /// </summary>
    /// <remarks>
    /// The backfill value is 1 (TenantDefaults.DefaultTenantId), not EF's scaffolded 0. Existing
    /// deployments are single-tenant, so their rows belong to tenant 1; left at 0 they would fall
    /// outside the query filter and every client, user, and signing key would read as absent while
    /// still sitting in the table. The literal is spelled out rather than referenced so that this
    /// migration keeps describing what it did even if the constant is ever changed.
    /// </remarks>
    public partial class AddTenantIdToAggregateRoots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UQ_Users_SubjectId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UQ_Users_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UQ_SigningKeys_KeyId",
                table: "SigningKeys");

            migrationBuilder.DropIndex(
                name: "UQ_Roles_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "UQ_DeviceCodes_DeviceCode",
                table: "DeviceCodes");

            migrationBuilder.DropIndex(
                name: "UQ_DeviceCodes_UserCode",
                table: "DeviceCodes");

            migrationBuilder.DropIndex(
                name: "UQ_Clients_ClientId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "UQ_ApiScopes_Name",
                table: "ApiScopes");

            migrationBuilder.DropIndex(
                name: "UQ_ApiResources_Name",
                table: "ApiResources");

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Users",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "SigningKeys",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Roles",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PersistedGrants",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "DeviceCodes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Clients",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ApiScopes",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ApiResources",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Users_TenantId_Email",
                table: "Users",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Users_TenantId_SubjectId",
                table: "Users",
                columns: new[] { "TenantId", "SubjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Users_TenantId_Username",
                table: "Users",
                columns: new[] { "TenantId", "Username" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SigningKeys_TenantId_KeyId",
                table: "SigningKeys",
                columns: new[] { "TenantId", "KeyId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Roles_TenantId_Name",
                table: "Roles",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_DeviceCodes_TenantId_DeviceCode",
                table: "DeviceCodes",
                columns: new[] { "TenantId", "DeviceCodeValue" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_DeviceCodes_TenantId_UserCode",
                table: "DeviceCodes",
                columns: new[] { "TenantId", "UserCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Clients_TenantId_ClientId",
                table: "Clients",
                columns: new[] { "TenantId", "ClientId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ApiScopes_TenantId_Name",
                table: "ApiScopes",
                columns: new[] { "TenantId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ApiResources_TenantId_Name",
                table: "ApiResources",
                columns: new[] { "TenantId", "Name" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_TenantId_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UQ_Users_TenantId_SubjectId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UQ_Users_TenantId_Username",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "UQ_SigningKeys_TenantId_KeyId",
                table: "SigningKeys");

            migrationBuilder.DropIndex(
                name: "UQ_Roles_TenantId_Name",
                table: "Roles");

            migrationBuilder.DropIndex(
                name: "UQ_DeviceCodes_TenantId_DeviceCode",
                table: "DeviceCodes");

            migrationBuilder.DropIndex(
                name: "UQ_DeviceCodes_TenantId_UserCode",
                table: "DeviceCodes");

            migrationBuilder.DropIndex(
                name: "UQ_Clients_TenantId_ClientId",
                table: "Clients");

            migrationBuilder.DropIndex(
                name: "UQ_ApiScopes_TenantId_Name",
                table: "ApiScopes");

            migrationBuilder.DropIndex(
                name: "UQ_ApiResources_TenantId_Name",
                table: "ApiResources");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SigningKeys");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Roles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PersistedGrants");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DeviceCodes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Clients");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ApiScopes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ApiResources");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Users_SubjectId",
                table: "Users",
                column: "SubjectId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_SigningKeys_KeyId",
                table: "SigningKeys",
                column: "KeyId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Roles_Name",
                table: "Roles",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_DeviceCodes_DeviceCode",
                table: "DeviceCodes",
                column: "DeviceCodeValue",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_DeviceCodes_UserCode",
                table: "DeviceCodes",
                column: "UserCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_Clients_ClientId",
                table: "Clients",
                column: "ClientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ApiScopes_Name",
                table: "ApiScopes",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UQ_ApiResources_Name",
                table: "ApiResources",
                column: "Name",
                unique: true);
        }
    }
}
