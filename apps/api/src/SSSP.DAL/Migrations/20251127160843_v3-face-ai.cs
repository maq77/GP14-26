using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SSSP.DAL.Migrations
{
    /// <inheritdoc />
    public partial class v3faceai : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Operators_OperatorId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "RoleUser");

            migrationBuilder.DropTable(
                name: "Roles");

            migrationBuilder.DeleteData(
                table: "Cameras",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Cameras",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Incidents",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Sensors",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Sensors",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Operators",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Operators",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Operators");

            migrationBuilder.AddColumn<int>(
                name: "Role",
                table: "AspNetUsers",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "FaceProfiles",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EmbeddingJson = table.Column<string>(type: "text", nullable: false),
                    IsPrimary = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaceProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaceProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaceProfiles_UserId",
                table: "FaceProfiles",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Operators_OperatorId",
                table: "AspNetUsers",
                column: "OperatorId",
                principalTable: "Operators",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_Operators_OperatorId",
                table: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "FaceProfiles");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "AspNetUsers");

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Operators",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RoleUser",
                columns: table => new
                {
                    RolesId = table.Column<int>(type: "int", nullable: false),
                    UsersId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RoleUser", x => new { x.RolesId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_RoleUser_AspNetUsers_UsersId",
                        column: x => x.UsersId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RoleUser_Roles_RolesId",
                        column: x => x.RolesId,
                        principalTable: "Roles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Operators",
                columns: new[] { "Id", "CreatedAt", "IsActive", "Location", "Name", "Type", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 11, 19, 12, 0, 0, 0, DateTimeKind.Unspecified), true, "Downtown", "City Security", 5, null },
                    { 2, new DateTime(2025, 11, 19, 12, 0, 0, 0, DateTimeKind.Unspecified), true, "Medical District", "Hospital Central", 2, null }
                });

            migrationBuilder.InsertData(
                table: "Cameras",
                columns: new[] { "Id", "CreatedAt", "IsActive", "LastSeenAt", "Name", "OperatorId", "RtspUrl", "Location_Address", "Location_Latitude", "Location_Longitude" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 11, 19, 12, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Camera 1", 1, "rtsp://camera1", "Main Street", 30.0, 31.0 },
                    { 2, new DateTime(2025, 11, 19, 12, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Camera 2", 2, "rtsp://camera2", "Hospital Gate", 30.100000000000001, 31.100000000000001 }
                });

            migrationBuilder.InsertData(
                table: "Incidents",
                columns: new[] { "Id", "AssignedToUserId", "DedupeKey", "Description", "OperatorId", "PayloadJson", "ResolvedAt", "Severity", "Source", "Status", "Timestamp", "Title", "Type", "Location_Address", "Location_Latitude", "Location_Longitude" },
                values: new object[] { 1, null, null, "Example incident", 1, null, null, "High", "Sensor", "Open", new DateTime(2025, 11, 19, 12, 30, 0, 0, DateTimeKind.Unspecified), "Test Incident", "Waste", "Downtown", 30.0, 31.0 });

            migrationBuilder.InsertData(
                table: "Sensors",
                columns: new[] { "Id", "CreatedAt", "IsActive", "LastReadingAt", "Name", "OperatorId", "Type", "Location_Address", "Location_Latitude", "Location_Longitude" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 11, 19, 12, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Sensor 1", 1, 4, "City Park", 30.0, 31.0 },
                    { 2, new DateTime(2025, 11, 19, 12, 0, 0, 0, DateTimeKind.Unspecified), true, null, "Sensor 2", 2, 2, "Hospital Roof", 30.199999999999999, 31.199999999999999 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_RoleUser_UsersId",
                table: "RoleUser",
                column: "UsersId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Operators_OperatorId",
                table: "AspNetUsers",
                column: "OperatorId",
                principalTable: "Operators",
                principalColumn: "Id");
        }
    }
}
