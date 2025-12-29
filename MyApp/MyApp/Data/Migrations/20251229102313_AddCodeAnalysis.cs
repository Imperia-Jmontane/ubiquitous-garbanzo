using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyApp.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeAnalysis : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IndexedRepositories",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositoryId = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    RepositoryPath = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    CommitSha = table.Column<string>(type: "TEXT", maxLength: 40, nullable: true),
                    BranchName = table.Column<string>(type: "TEXT", maxLength: 250, nullable: true),
                    IndexedAtUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    FilesIndexed = table.Column<int>(type: "INTEGER", nullable: false),
                    SymbolsCollected = table.Column<int>(type: "INTEGER", nullable: false),
                    ReferencesCollected = table.Column<int>(type: "INTEGER", nullable: false),
                    IndexingDuration = table.Column<TimeSpan>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexedRepositories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CodeNodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    SerializedName = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    NormalizedName = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    Accessibility = table.Column<int>(type: "INTEGER", nullable: true),
                    IsStatic = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAbstract = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsVirtual = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsOverride = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsExtensionMethod = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAsync = table.Column<bool>(type: "INTEGER", nullable: false),
                    ParentNodeId = table.Column<long>(type: "INTEGER", nullable: true),
                    RepositorySnapshotId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeNodes_CodeNodes_ParentNodeId",
                        column: x => x.ParentNodeId,
                        principalTable: "CodeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_CodeNodes_IndexedRepositories_RepositorySnapshotId",
                        column: x => x.RepositorySnapshotId,
                        principalTable: "IndexedRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SourceFiles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Path = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 50, nullable: false, defaultValue: "csharp"),
                    FileHash = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ModificationTime = table.Column<DateTime>(type: "TEXT", nullable: true),
                    IsIndexed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsComplete = table.Column<bool>(type: "INTEGER", nullable: false),
                    LineCount = table.Column<int>(type: "INTEGER", nullable: true),
                    RepositorySnapshotId = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceFiles_IndexedRepositories_RepositorySnapshotId",
                        column: x => x.RepositorySnapshotId,
                        principalTable: "IndexedRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CodeEdges",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    SourceNodeId = table.Column<long>(type: "INTEGER", nullable: false),
                    TargetNodeId = table.Column<long>(type: "INTEGER", nullable: false),
                    RepositorySnapshotId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodeEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodeEdges_CodeNodes_SourceNodeId",
                        column: x => x.SourceNodeId,
                        principalTable: "CodeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CodeEdges_CodeNodes_TargetNodeId",
                        column: x => x.TargetNodeId,
                        principalTable: "CodeNodes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CodeEdges_IndexedRepositories_RepositorySnapshotId",
                        column: x => x.RepositorySnapshotId,
                        principalTable: "IndexedRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "IndexingErrors",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    RepositorySnapshotId = table.Column<long>(type: "INTEGER", nullable: false),
                    Message = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: true),
                    IsFatal = table.Column<bool>(type: "INTEGER", nullable: false),
                    FileId = table.Column<long>(type: "INTEGER", nullable: true),
                    Line = table.Column<int>(type: "INTEGER", nullable: true),
                    Column = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IndexingErrors", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IndexingErrors_IndexedRepositories_RepositorySnapshotId",
                        column: x => x.RepositorySnapshotId,
                        principalTable: "IndexedRepositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IndexingErrors_SourceFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "SourceFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SourceLocations",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FileId = table.Column<long>(type: "INTEGER", nullable: false),
                    StartLine = table.Column<int>(type: "INTEGER", nullable: false),
                    StartColumn = table.Column<int>(type: "INTEGER", nullable: false),
                    EndLine = table.Column<int>(type: "INTEGER", nullable: false),
                    EndColumn = table.Column<int>(type: "INTEGER", nullable: false),
                    StartOffset = table.Column<int>(type: "INTEGER", nullable: false),
                    EndOffset = table.Column<int>(type: "INTEGER", nullable: false),
                    Type = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SourceLocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SourceLocations_SourceFiles_FileId",
                        column: x => x.FileId,
                        principalTable: "SourceFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Occurrences",
                columns: table => new
                {
                    ElementId = table.Column<long>(type: "INTEGER", nullable: false),
                    SourceLocationId = table.Column<long>(type: "INTEGER", nullable: false),
                    CodeEdgeId = table.Column<long>(type: "INTEGER", nullable: true),
                    CodeNodeId = table.Column<long>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Occurrences", x => new { x.ElementId, x.SourceLocationId });
                    table.ForeignKey(
                        name: "FK_Occurrences_CodeEdges_CodeEdgeId",
                        column: x => x.CodeEdgeId,
                        principalTable: "CodeEdges",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Occurrences_CodeNodes_CodeNodeId",
                        column: x => x.CodeNodeId,
                        principalTable: "CodeNodes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Occurrences_SourceLocations_SourceLocationId",
                        column: x => x.SourceLocationId,
                        principalTable: "SourceLocations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_edge_snapshot",
                table: "CodeEdges",
                column: "RepositorySnapshotId");

            migrationBuilder.CreateIndex(
                name: "idx_edge_source",
                table: "CodeEdges",
                column: "SourceNodeId");

            migrationBuilder.CreateIndex(
                name: "idx_edge_source_target_type",
                table: "CodeEdges",
                columns: new[] { "SourceNodeId", "TargetNodeId", "Type" });

            migrationBuilder.CreateIndex(
                name: "idx_edge_target",
                table: "CodeEdges",
                column: "TargetNodeId");

            migrationBuilder.CreateIndex(
                name: "idx_edge_type",
                table: "CodeEdges",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "idx_node_display_name",
                table: "CodeNodes",
                column: "DisplayName");

            migrationBuilder.CreateIndex(
                name: "idx_node_normalized_name",
                table: "CodeNodes",
                column: "NormalizedName");

            migrationBuilder.CreateIndex(
                name: "idx_node_parent",
                table: "CodeNodes",
                column: "ParentNodeId");

            migrationBuilder.CreateIndex(
                name: "idx_node_serialized_name",
                table: "CodeNodes",
                column: "SerializedName");

            migrationBuilder.CreateIndex(
                name: "idx_node_snapshot",
                table: "CodeNodes",
                column: "RepositorySnapshotId");

            migrationBuilder.CreateIndex(
                name: "idx_node_type",
                table: "CodeNodes",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "idx_node_unique",
                table: "CodeNodes",
                columns: new[] { "RepositorySnapshotId", "SerializedName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_indexed_repository_commit",
                table: "IndexedRepositories",
                columns: new[] { "RepositoryId", "CommitSha" });

            migrationBuilder.CreateIndex(
                name: "idx_indexed_repository_id",
                table: "IndexedRepositories",
                column: "RepositoryId");

            migrationBuilder.CreateIndex(
                name: "idx_indexed_repository_status",
                table: "IndexedRepositories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "idx_error_file",
                table: "IndexingErrors",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "idx_error_snapshot",
                table: "IndexingErrors",
                column: "RepositorySnapshotId");

            migrationBuilder.CreateIndex(
                name: "idx_occurrence_element",
                table: "Occurrences",
                column: "ElementId");

            migrationBuilder.CreateIndex(
                name: "idx_occurrence_location",
                table: "Occurrences",
                column: "SourceLocationId");

            migrationBuilder.CreateIndex(
                name: "IX_Occurrences_CodeEdgeId",
                table: "Occurrences",
                column: "CodeEdgeId");

            migrationBuilder.CreateIndex(
                name: "IX_Occurrences_CodeNodeId",
                table: "Occurrences",
                column: "CodeNodeId");

            migrationBuilder.CreateIndex(
                name: "idx_file_hash",
                table: "SourceFiles",
                column: "FileHash");

            migrationBuilder.CreateIndex(
                name: "idx_file_path",
                table: "SourceFiles",
                columns: new[] { "RepositorySnapshotId", "Path" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_file_snapshot",
                table: "SourceFiles",
                column: "RepositorySnapshotId");

            migrationBuilder.CreateIndex(
                name: "idx_source_location_file",
                table: "SourceLocations",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "idx_source_location_position",
                table: "SourceLocations",
                columns: new[] { "FileId", "StartLine", "StartColumn" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IndexingErrors");

            migrationBuilder.DropTable(
                name: "Occurrences");

            migrationBuilder.DropTable(
                name: "CodeEdges");

            migrationBuilder.DropTable(
                name: "SourceLocations");

            migrationBuilder.DropTable(
                name: "CodeNodes");

            migrationBuilder.DropTable(
                name: "SourceFiles");

            migrationBuilder.DropTable(
                name: "IndexedRepositories");
        }
    }
}
