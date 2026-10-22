using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Qoot.Api.Data.Migrations
{
    /// <summary>
    /// Quote requests become Contact us messages: name, phone, an optional email and the message.
    /// Messages already received keep everything: an email typed in the old "phone or email" box
    /// moves to Email, and the old quote details (organisation, location, people, how often) are
    /// added at the end of the message text before their columns are removed.
    /// The website section itself ("quote" → "contact") is converted by the seeder on start-up.
    /// </summary>
    public partial class ContactUs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "ContactSubmissions",
                type: "nvarchar(254)",
                maxLength: 254,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(
                """
                UPDATE [ContactSubmissions] SET [Email] = LEFT([Contact], 254), [Contact] = N'' WHERE [Contact] LIKE N'%@%';
                UPDATE [ContactSubmissions] SET [Need] = LEFT(CONCAT([Need],
                    CASE WHEN [Organisation] <> N'' THEN CONCAT(NCHAR(10), N'Organisation: ', [Organisation]) ELSE N'' END,
                    CASE WHEN [Location] <> N'' THEN CONCAT(NCHAR(10), N'Location: ', [Location]) ELSE N'' END,
                    CASE WHEN [People] IS NOT NULL THEN CONCAT(NCHAR(10), N'People: ', [People]) ELSE N'' END,
                    CASE WHEN [Frequency] <> N'' THEN CONCAT(NCHAR(10), N'How often: ', [Frequency]) ELSE N'' END), 2000);
                UPDATE [ContactSubmissions] SET [Contact] = LEFT([Contact], 40);
                """);

            migrationBuilder.DropColumn(
                name: "Frequency",
                table: "ContactSubmissions");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "ContactSubmissions");

            migrationBuilder.DropColumn(
                name: "Organisation",
                table: "ContactSubmissions");

            migrationBuilder.DropColumn(
                name: "People",
                table: "ContactSubmissions");

            migrationBuilder.RenameColumn(
                name: "Need",
                table: "ContactSubmissions",
                newName: "Message");

            migrationBuilder.RenameColumn(
                name: "Contact",
                table: "ContactSubmissions",
                newName: "Phone");

            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "ContactSubmissions",
                type: "nvarchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(160)",
                oldMaxLength: 160);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Phone",
                table: "ContactSubmissions",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(40)",
                oldMaxLength: 40);

            migrationBuilder.RenameColumn(
                name: "Phone",
                table: "ContactSubmissions",
                newName: "Contact");

            migrationBuilder.RenameColumn(
                name: "Message",
                table: "ContactSubmissions",
                newName: "Need");

            migrationBuilder.AddColumn<string>(
                name: "Frequency",
                table: "ContactSubmissions",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Location",
                table: "ContactSubmissions",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Organisation",
                table: "ContactSubmissions",
                type: "nvarchar(160)",
                maxLength: 160,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "People",
                table: "ContactSubmissions",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("UPDATE [ContactSubmissions] SET [Contact] = LEFT([Email], 160) WHERE [Contact] = N'' AND [Email] <> N'';");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "ContactSubmissions");
        }
    }
}
