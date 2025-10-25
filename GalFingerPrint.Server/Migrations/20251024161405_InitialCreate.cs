using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GalFingerPrint.Server.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Galgames",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VndbId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Galgames", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Hashes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HashValue = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hashes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Address = table.Column<long>(type: "bigint", nullable: false),
                    VndbId = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Hashes = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GalgameHashes",
                columns: table => new
                {
                    HashId = table.Column<int>(type: "integer", nullable: false),
                    GalgameId = table.Column<int>(type: "integer", nullable: false),
                    Cnt = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GalgameHashes", x => new { x.HashId, x.GalgameId });
                    table.ForeignKey(
                        name: "FK_GalgameHashes_Galgames_GalgameId",
                        column: x => x.GalgameId,
                        principalTable: "Galgames",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GalgameHashes_Hashes_HashId",
                        column: x => x.HashId,
                        principalTable: "Hashes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GalgameHashes_GalgameId",
                table: "GalgameHashes",
                column: "GalgameId");

            migrationBuilder.CreateIndex(
                name: "IX_Galgames_VndbId",
                table: "Galgames",
                column: "VndbId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Hashes_HashValue",
                table: "Hashes",
                column: "HashValue",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Votes_Address_VndbId",
                table: "Votes",
                columns: new[] { "Address", "VndbId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GalgameHashes");

            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropTable(
                name: "Galgames");

            migrationBuilder.DropTable(
                name: "Hashes");
        }
    }
}
