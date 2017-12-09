namespace Khala.FakeDomain.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    public partial class AddContributorPropertyToPersistentEvent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Contributor",
                table: "PersistentEvents",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Contributor",
                table: "PersistentEvents");
        }
    }
}
