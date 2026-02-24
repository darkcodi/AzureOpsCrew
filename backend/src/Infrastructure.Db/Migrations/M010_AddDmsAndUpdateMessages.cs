using FluentMigrator;

namespace AzureOpsCrew.Infrastructure.Db.Migrations;

[Migration(2026_02_23_11_00_00, "Add Dms table and update Messages table")]
public class M010_AddDmsAndUpdateMessages : Migration
{
    public override void Up()
    {
        Create.Table("Dms")
            .WithColumn("Id").AsGuid().NotNullable().PrimaryKey()
            .WithColumn("User1Id").AsString().Nullable()
            .WithColumn("User2Id").AsString().Nullable()
            .WithColumn("Agent1Id").AsString().Nullable()
            .WithColumn("Agent2Id").AsString().Nullable()
            .WithColumn("CreatedAt").AsDateTime().NotNullable();

        Create.ForeignKey("FK_Dms_Users_User1Id")
            .FromTable("Dms").ForeignColumn("User1Id")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.ForeignKey("FK_Dms_Users_User2Id")
            .FromTable("Dms").ForeignColumn("User2Id")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.ForeignKey("FK_Dms_Agents_Agent1Id")
            .FromTable("Dms").ForeignColumn("Agent1Id")
            .ToTable("Agent").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.ForeignKey("FK_Dms_Agents_Agent2Id")
            .FromTable("Dms").ForeignColumn("Agent2Id")
            .ToTable("Agent").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.Index("IX_Dms_User1Id").OnTable("Dms").OnColumn("User1Id");
        Create.Index("IX_Dms_User2Id").OnTable("Dms").OnColumn("User2Id");
        Create.Index("IX_Dms_Agent1Id").OnTable("Dms").OnColumn("Agent1Id");
        Create.Index("IX_Dms_Agent2Id").OnTable("Dms").OnColumn("Agent2Id");

        Alter.Table("Messages")
            .AddColumn("AgentId").AsString().Nullable()
            .AddColumn("UserId").AsString().Nullable()
            .AddColumn("ChannelId").AsGuid().Nullable()
            .AddColumn("DmId").AsGuid().Nullable();

        Create.ForeignKey("FK_Messages_Agents_AgentId")
            .FromTable("Messages").ForeignColumn("AgentId")
            .ToTable("Agent").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.ForeignKey("FK_Messages_Users_UserId")
            .FromTable("Messages").ForeignColumn("UserId")
            .ToTable("Users").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.ForeignKey("FK_Messages_Channels_ChannelId")
            .FromTable("Messages").ForeignColumn("ChannelId")
            .ToTable("Channel").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.SetNull);

        Create.ForeignKey("FK_Messages_Dms_DmId")
            .FromTable("Messages").ForeignColumn("DmId")
            .ToTable("Dms").PrimaryColumn("Id")
            .OnDelete(System.Data.Rule.Cascade);

        Create.Index("IX_Messages_AgentId").OnTable("Messages").OnColumn("AgentId");
        Create.Index("IX_Messages_UserId").OnTable("Messages").OnColumn("UserId");
        Create.Index("IX_Messages_ChannelId").OnTable("Messages").OnColumn("ChannelId");
        Create.Index("IX_Messages_DmId").OnTable("Messages").OnColumn("DmId");
    }

    public override void Down()
    {
        Delete.Index("IX_Messages_DmId").OnTable("Messages");
        Delete.Index("IX_Messages_ChannelId").OnTable("Messages");
        Delete.Index("IX_Messages_UserId").OnTable("Messages");
        Delete.Index("IX_Messages_AgentId").OnTable("Messages");

        Delete.ForeignKey("FK_Messages_Dms_DmId").OnTable("Messages");
        Delete.ForeignKey("FK_Messages_Channels_ChannelId").OnTable("Messages");
        Delete.ForeignKey("FK_Messages_Users_UserId").OnTable("Messages");
        Delete.ForeignKey("FK_Messages_Agents_AgentId").OnTable("Messages");

        Delete.Column("DmId").FromTable("Messages");
        Delete.Column("ChannelId").FromTable("Messages");
        Delete.Column("UserId").FromTable("Messages");
        Delete.Column("AgentId").FromTable("Messages");

        Delete.Index("IX_Dms_Agent2Id").OnTable("Dms");
        Delete.Index("IX_Dms_Agent1Id").OnTable("Dms");
        Delete.Index("IX_Dms_User2Id").OnTable("Dms");
        Delete.Index("IX_Dms_User1Id").OnTable("Dms");

        Delete.ForeignKey("FK_Dms_Agents_Agent2Id").OnTable("Dms");
        Delete.ForeignKey("FK_Dms_Agents_Agent1Id").OnTable("Dms");
        Delete.ForeignKey("FK_Dms_Users_User2Id").OnTable("Dms");
        Delete.ForeignKey("FK_Dms_Users_User1Id").OnTable("Dms");

        Delete.Table("Dms");
    }
}
