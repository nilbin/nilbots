using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BotArena.App.Migrations
{
    /// <summary>
    /// Display names become unique, case-insensitively.
    /// <para>
    /// Hand-written because the index is a functional one — <c>lower("DisplayName")</c> —
    /// which EF cannot express in the model, so scaffolding produced an empty migration.
    /// The model documents that this file is where it lives.
    /// </para>
    /// </summary>
    public partial class AccountDisplayNames : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Existing rows may already collide — nothing stopped them until now — and the
            // index cannot be created while they do. Suffix the newer ones, keeping the
            // account that had the name first.
            //
            // A loop rather than a single pass, because a suffixed name can collide with
            // something already present ("bob" + "bob" + "bob2" all resolving to "bob2"),
            // and a deploy that fails on index creation after having already renamed people
            // is the worst of both outcomes. Each pass strictly reduces the number of
            // duplicates, so it terminates.
            migrationBuilder.Sql("""
                DO $$
                DECLARE renamed integer;
                BEGIN
                  LOOP
                    WITH ranked AS (
                        SELECT "Id",
                               "DisplayName",
                               row_number() OVER (
                                   PARTITION BY lower("DisplayName")
                                   ORDER BY "CreatedAt", "Id"
                               ) AS position
                        FROM "Users"
                    )
                    UPDATE "Users" AS u
                    SET "DisplayName" =
                        left(r."DisplayName", 40 - length(r.position::text)) || r.position::text
                    FROM ranked AS r
                    WHERE u."Id" = r."Id" AND r.position > 1;

                    GET DIAGNOSTICS renamed = ROW_COUNT;
                    EXIT WHEN renamed = 0;
                  END LOOP;
                END $$;
                """);

            migrationBuilder.Sql("""
                CREATE UNIQUE INDEX "IX_Users_DisplayName_Lower"
                ON "Users" (lower("DisplayName"));
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The renames are not reversed: the original names are gone, and inventing
            // them back would be worse than leaving people under the name they have been
            // playing as.
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_Users_DisplayName_Lower";""");
        }
    }
}
