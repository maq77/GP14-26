using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SSSP.DAL.Migrations
{
    /// <inheritdoc />
    public partial class EditedCameraTbl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "OperatorId",
                table: "Cameras",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<double>(
                name: "Location_Longitude",
                table: "Cameras",
                type: "float",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<double>(
                name: "Location_Latitude",
                table: "Cameras",
                type: "float",
                nullable: true,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Cameras",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.AlterColumn<int>(
                name: "OperatorId",
                table: "Cameras",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "Location_Longitude",
                table: "Cameras",
                type: "float",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "Location_Latitude",
                table: "Cameras",
                type: "float",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "float",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedAt",
                table: "Cameras",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
