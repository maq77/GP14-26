using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SSSP.DAL.Migrations
{
    /// <inheritdoc />
    public partial class updatedcameraembeddingtbls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "EmbeddingJson",
                table: "FaceProfiles");

            migrationBuilder.AddColumn<int>(
                name: "Capabilities",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<double>(
                name: "MatchThresholdOverride",
                table: "Cameras",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RecognitionMode",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "FaceEmbedding",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FaceProfileId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Vector = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SourceCameraId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FaceEmbedding", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FaceEmbedding_FaceProfiles_FaceProfileId",
                        column: x => x.FaceProfileId,
                        principalTable: "FaceProfiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FaceEmbedding_FaceProfileId",
                table: "FaceEmbedding",
                column: "FaceProfileId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FaceEmbedding");

            migrationBuilder.DropColumn(
                name: "Capabilities",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "MatchThresholdOverride",
                table: "Cameras");

            migrationBuilder.DropColumn(
                name: "RecognitionMode",
                table: "Cameras");

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingJson",
                table: "FaceProfiles",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.InsertData(
                table: "Operators",
                columns: new[] { "Id", "CreatedAt", "IsActive", "Location", "Name", "Type" },
                values: new object[,]
                {
                    { 1, new DateTime(2025, 11, 19, 12, 0, 0, 0, DateTimeKind.Unspecified), true, "Downtown", "City Security", 5 },
                    { 2, new DateTime(2025, 11, 19, 12, 0, 0, 0, DateTimeKind.Unspecified), true, "Medical District", "Hospital Central", 2 }
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
        }
    }
}
