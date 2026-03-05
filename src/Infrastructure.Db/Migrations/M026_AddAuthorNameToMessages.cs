using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_03_04_13_00_00, "Add AuthorName column to Messages table")]
public class M026_AddAuthorNameToMessages : Migration
{
    public override void Up()
    {
        Alter.Table("Messages")
            .AddColumn("AuthorName")
            .AsString()
            .Nullable();
    }

    public override void Down()
    {
        Delete.Column("AuthorName")
            .FromTable("Messages");
    }
}
