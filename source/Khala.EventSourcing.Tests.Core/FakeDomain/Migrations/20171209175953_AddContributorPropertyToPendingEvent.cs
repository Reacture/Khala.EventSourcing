namespace Khala.FakeDomain.Migrations
{
    using Microsoft.EntityFrameworkCore.Migrations;

    public partial class AddContributorPropertyToPendingEvent : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Contributor",
                table: "PendingEvents",
                type: "nvarchar(max)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Contributor",
                table: "PendingEvents");
        }
    }
}
