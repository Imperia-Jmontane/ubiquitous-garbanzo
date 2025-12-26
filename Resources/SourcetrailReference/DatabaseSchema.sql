-- ============================================================================
-- REFERENCE FILE: SQLite Database Schema for Code Analysis
-- Based on SourcetrailDB schema, adapted for C# / Roslyn analysis.
-- ============================================================================
--
-- HOW TO USE THIS FILE:
-- 1. Review the schema to understand the data model
-- 2. Create EF Core entities that map to these tables
-- 3. Use EF Core migrations to generate the actual database
--
-- DO NOT run this SQL directly - use it as a reference for EF Core models.
--
-- IMPORTANT: This schema has been enhanced based on peer review to include:
-- - Repository versioning (RepositoryId, CommitSha)
-- - Incremental indexing support (FileHash)
-- - Extended symbol metadata (IsExtensionMethod, IsAsync, ParentNodeId)
-- - Byte offsets for precise navigation (StartOffset, EndOffset)
-- - Normalized names for efficient search
-- - long IDs to handle large repositories
-- ============================================================================

-- =============================================================================
-- INDEXED REPOSITORY TABLE (NEW)
-- Tracks indexed snapshots of repositories for multi-repo support and versioning
-- =============================================================================
CREATE TABLE indexed_repository (
    id INTEGER PRIMARY KEY AUTOINCREMENT,  -- Using INTEGER for SQLite (maps to long in C#)
    repository_id TEXT NOT NULL,            -- Unique identifier from LocalRepository
    repository_path TEXT NOT NULL,          -- Absolute path to repository root
    commit_sha TEXT,                        -- Git commit SHA at time of indexing
    branch_name TEXT,                       -- Git branch name at time of indexing
    indexed_at_utc TEXT NOT NULL,           -- ISO 8601 timestamp
    status INTEGER NOT NULL DEFAULT 0,      -- IndexingStatus enum
    error_message TEXT,                     -- Error details if status = Failed
    files_indexed INTEGER DEFAULT 0,
    symbols_collected INTEGER DEFAULT 0,
    references_collected INTEGER DEFAULT 0,
    indexing_duration_ms INTEGER DEFAULT 0  -- Duration in milliseconds
);

-- IndexingStatus enum values:
-- 0 = Queued
-- 1 = Running
-- 2 = Completed
-- 3 = Failed
-- 4 = Cancelled

-- =============================================================================
-- NODE TABLE
-- Represents code symbols: namespaces, types, methods, properties, fields, etc.
--
-- IMPORTANT FIELDS:
-- - type: The CSharpSymbolKind enum value (see below)
-- - serialized_name: Fully qualified name for unique identification
-- - display_name: Human-readable name for UI display
-- - normalized_name: Lowercase display_name for efficient search
-- - repository_snapshot_id: Links to indexed_repository for multi-repo support
-- =============================================================================
CREATE TABLE node (
    id INTEGER PRIMARY KEY AUTOINCREMENT,   -- Using INTEGER for SQLite (maps to long in C#)
    repository_snapshot_id INTEGER NOT NULL, -- FK to indexed_repository
    type INTEGER NOT NULL,                   -- CSharpSymbolKind enum value
    serialized_name TEXT NOT NULL,           -- e.g., "global::MyApp.Services.MyService.GetDataAsync()"
    display_name TEXT,                       -- e.g., "GetDataAsync()"
    normalized_name TEXT,                    -- e.g., "getdataasync()" for case-insensitive search
    parent_node_id INTEGER,                  -- FK to parent node (for containment hierarchy)
    accessibility INTEGER,                   -- 0=Public, 1=Protected, 2=Internal, etc.
    is_static INTEGER DEFAULT 0,             -- 1 if static
    is_abstract INTEGER DEFAULT 0,           -- 1 if abstract
    is_virtual INTEGER DEFAULT 0,            -- 1 if virtual
    is_override INTEGER DEFAULT 0,           -- 1 if override
    is_extension_method INTEGER DEFAULT 0,   -- 1 if extension method
    is_async INTEGER DEFAULT 0,              -- 1 if async method
    is_external INTEGER DEFAULT 0,           -- 1 if from external assembly (not in source)
    FOREIGN KEY(repository_snapshot_id) REFERENCES indexed_repository(id) ON DELETE CASCADE,
    FOREIGN KEY(parent_node_id) REFERENCES node(id) ON DELETE SET NULL
);

-- CSharpSymbolKind enum values:
-- 0 = Unknown
-- 1 = Namespace
-- 2 = Assembly
-- 3 = Module
-- 10 = Class
-- 11 = Struct
-- 12 = Interface
-- 13 = Enum
-- 14 = Delegate
-- 15 = Record
-- 16 = RecordStruct
-- 20 = Field
-- 21 = Property
-- 22 = Method
-- 23 = Constructor
-- 24 = Destructor
-- 25 = Operator
-- 26 = Indexer
-- 27 = Event
-- 28 = EnumMember
-- 30 = LocalVariable
-- 31 = Parameter
-- 32 = TypeParameter
-- 50 = File
-- 51 = Using
-- 52 = Attribute

-- =============================================================================
-- EDGE TABLE
-- Represents relationships between nodes
--
-- IMPORTANT FIELDS:
-- - type: The CSharpReferenceKind enum value (see below)
-- - source_node_id: The node that has/makes the reference
-- - target_node_id: The node being referenced
-- =============================================================================
CREATE TABLE edge (
    id INTEGER PRIMARY KEY AUTOINCREMENT,   -- Using INTEGER for SQLite (maps to long in C#)
    repository_snapshot_id INTEGER NOT NULL, -- FK to indexed_repository
    type INTEGER NOT NULL,                   -- CSharpReferenceKind enum value
    source_node_id INTEGER NOT NULL,         -- WHO is making the reference
    target_node_id INTEGER NOT NULL,         -- WHAT is being referenced
    FOREIGN KEY(repository_snapshot_id) REFERENCES indexed_repository(id) ON DELETE CASCADE,
    FOREIGN KEY(source_node_id) REFERENCES node(id) ON DELETE CASCADE,
    FOREIGN KEY(target_node_id) REFERENCES node(id) ON DELETE CASCADE
);

-- CSharpReferenceKind enum values:
-- 0 = Unknown
-- 1 = Inheritance           -- class Foo : BaseClass
-- 2 = InterfaceImplementation -- class Foo : IInterface
-- 10 = Call                  -- method invocation
-- 11 = TypeUsage             -- using a type in declaration
-- 12 = Override              -- method overrides base method
-- 20 = FieldAccess           -- accessing a field
-- 21 = PropertyAccess        -- accessing a property
-- 22 = EventAccess           -- accessing an event
-- 30 = Contains              -- namespace contains class, class contains method
-- 40 = Import                -- using directive
-- 41 = TypeArgument          -- generic type argument
-- 42 = AttributeUsage        -- [Attribute] usage
-- 50 = Instantiation         -- new T()
-- 51 = Cast                  -- (T)obj or obj as T
-- 52 = Throw                 -- throw new Exception()
-- 53 = Catch                 -- catch (Exception)

-- =============================================================================
-- FILE TABLE
-- Represents source code files
--
-- IMPORTANT: Includes file_hash for incremental indexing support
-- =============================================================================
CREATE TABLE file (
    id INTEGER PRIMARY KEY AUTOINCREMENT,   -- Using INTEGER for SQLite (maps to long in C#)
    repository_snapshot_id INTEGER NOT NULL, -- FK to indexed_repository
    path TEXT NOT NULL,                      -- Relative path within repository
    language TEXT DEFAULT 'csharp',          -- Language identifier
    file_hash TEXT,                          -- SHA256 hash for incremental indexing
    modification_time TEXT,                  -- Last modified timestamp (ISO 8601)
    indexed INTEGER DEFAULT 0,               -- 1 if fully indexed
    complete INTEGER DEFAULT 1,              -- 1 if parsing completed successfully
    line_count INTEGER,                      -- Total number of lines
    FOREIGN KEY(repository_snapshot_id) REFERENCES indexed_repository(id) ON DELETE CASCADE
);

-- =============================================================================
-- SOURCE LOCATION TABLE
-- Stores precise locations in source files (for click-to-navigate)
--
-- IMPORTANT:
-- - Line and column numbers are 1-based (not 0-based)
-- - StartOffset/EndOffset are 0-based byte offsets for precise selection
-- =============================================================================
CREATE TABLE source_location (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    file_id INTEGER NOT NULL,                -- FK to file
    start_line INTEGER NOT NULL,             -- Starting line (1-based)
    start_column INTEGER NOT NULL,           -- Starting column (1-based)
    end_line INTEGER NOT NULL,               -- Ending line (1-based)
    end_column INTEGER NOT NULL,             -- Ending column (1-based)
    start_offset INTEGER DEFAULT 0,          -- Starting byte offset (0-based)
    end_offset INTEGER DEFAULT 0,            -- Ending byte offset (0-based)
    location_type INTEGER DEFAULT 0,         -- 0=Definition, 1=Reference, 2=Scope
    FOREIGN KEY(file_id) REFERENCES file(id) ON DELETE CASCADE
);

-- LocationType enum values:
-- 0 = Definition  -- Where a symbol is defined
-- 1 = Reference   -- Where a symbol is used/referenced
-- 2 = Scope       -- The full scope of a symbol (e.g., method body)

-- =============================================================================
-- OCCURRENCE TABLE
-- Links elements (nodes/edges) to their source locations
-- This allows many-to-many relationship: one symbol can have multiple locations
-- =============================================================================
CREATE TABLE occurrence (
    element_id INTEGER NOT NULL,             -- The node or edge ID
    source_location_id INTEGER NOT NULL,     -- The location ID
    PRIMARY KEY(element_id, source_location_id),
    FOREIGN KEY(element_id) REFERENCES node(id) ON DELETE CASCADE,
    FOREIGN KEY(source_location_id) REFERENCES source_location(id) ON DELETE CASCADE
);

-- =============================================================================
-- INDEXING ERROR TABLE
-- Stores indexing/parsing errors for debugging
-- =============================================================================
CREATE TABLE indexing_error (
    id INTEGER PRIMARY KEY AUTOINCREMENT,
    repository_snapshot_id INTEGER NOT NULL, -- FK to indexed_repository
    message TEXT,                            -- Error message
    is_fatal INTEGER DEFAULT 0,              -- 1 if fatal error
    file_id INTEGER,                         -- File reference (optional)
    line INTEGER,                            -- Line number of error
    column_number INTEGER,                   -- Column number of error
    FOREIGN KEY(repository_snapshot_id) REFERENCES indexed_repository(id) ON DELETE CASCADE,
    FOREIGN KEY(file_id) REFERENCES file(id) ON DELETE SET NULL
);

-- =============================================================================
-- INDICES
-- For query performance - VERY IMPORTANT for large codebases
-- =============================================================================

-- Repository lookups
CREATE INDEX idx_indexed_repository_id ON indexed_repository(repository_id);
CREATE INDEX idx_indexed_repository_commit ON indexed_repository(repository_id, commit_sha);
CREATE INDEX idx_indexed_repository_status ON indexed_repository(status);

-- Node lookups by name (for search)
CREATE INDEX idx_node_serialized_name ON node(serialized_name);
CREATE INDEX idx_node_display_name ON node(display_name);
CREATE INDEX idx_node_normalized_name ON node(normalized_name);
CREATE INDEX idx_node_type ON node(type);
CREATE INDEX idx_node_parent ON node(parent_node_id);
CREATE INDEX idx_node_snapshot ON node(repository_snapshot_id);
CREATE UNIQUE INDEX idx_node_unique ON node(repository_snapshot_id, serialized_name);

-- Edge lookups (for graph traversal)
CREATE INDEX idx_edge_source ON edge(source_node_id);
CREATE INDEX idx_edge_target ON edge(target_node_id);
CREATE INDEX idx_edge_type ON edge(type);
CREATE INDEX idx_edge_snapshot ON edge(repository_snapshot_id);
CREATE INDEX idx_edge_source_target_type ON edge(source_node_id, target_node_id, type);

-- File lookups
CREATE UNIQUE INDEX idx_file_path ON file(repository_snapshot_id, path);
CREATE INDEX idx_file_hash ON file(file_hash);
CREATE INDEX idx_file_snapshot ON file(repository_snapshot_id);

-- Source location lookups (for navigation)
CREATE INDEX idx_source_location_file ON source_location(file_id);
CREATE INDEX idx_source_location_position ON source_location(file_id, start_line, start_column);

-- Occurrence lookups (for finding all usages)
CREATE INDEX idx_occurrence_element ON occurrence(element_id);
CREATE INDEX idx_occurrence_location ON occurrence(source_location_id);

-- Error lookups
CREATE INDEX idx_error_snapshot ON indexing_error(repository_snapshot_id);
CREATE INDEX idx_error_file ON indexing_error(file_id);

-- =============================================================================
-- EXAMPLE QUERIES
-- These show how to query the database for common use cases
-- =============================================================================

-- Find the latest snapshot for a repository
-- SELECT * FROM indexed_repository
-- WHERE repository_id = 'my-repo-id'
-- ORDER BY indexed_at_utc DESC
-- LIMIT 1;

-- Find all classes in a namespace (with pagination)
-- SELECT n.* FROM node n
-- WHERE n.repository_snapshot_id = :snapshot_id
-- AND n.type = 10  -- Class
-- AND n.serialized_name LIKE 'global::MyApp.Services.%'
-- LIMIT 100;

-- Find all methods that call a specific method
-- SELECT source.* FROM node source
-- JOIN edge e ON e.source_node_id = source.id
-- JOIN node target ON e.target_node_id = target.id
-- WHERE e.type = 10  -- Call
-- AND target.serialized_name = 'global::MyApp.Services.MyService.GetDataAsync()';

-- Find all references to a symbol with their locations
-- SELECT n.display_name, f.path, sl.start_line, sl.start_column
-- FROM node n
-- JOIN occurrence o ON o.element_id = n.id
-- JOIN source_location sl ON o.source_location_id = sl.id
-- JOIN file f ON sl.file_id = f.id
-- WHERE n.serialized_name = 'global::MyApp.Services.MyService';

-- Search symbols by normalized name (case-insensitive partial match)
-- SELECT * FROM node
-- WHERE repository_snapshot_id = :snapshot_id
-- AND normalized_name LIKE '%getdata%'
-- ORDER BY
--   CASE WHEN normalized_name = 'getdata' THEN 0  -- Exact match first
--        WHEN normalized_name LIKE 'getdata%' THEN 1  -- Prefix match
--        ELSE 2  -- Contains match
--   END,
--   display_name
-- LIMIT 20;

-- Find inheritance hierarchy (classes that inherit from a base)
-- SELECT child.display_name, parent.display_name
-- FROM node child
-- JOIN edge e ON e.source_node_id = child.id
-- JOIN node parent ON e.target_node_id = parent.id
-- WHERE e.type = 1  -- Inheritance
-- AND parent.serialized_name = 'global::MyApp.Services.BaseService';

-- Check if a file has changed (for incremental indexing)
-- SELECT id, file_hash FROM file
-- WHERE repository_snapshot_id = :snapshot_id
-- AND path = 'src/Services/MyService.cs';

-- Get graph data with limits (pagination)
-- SELECT n.id, n.serialized_name, n.display_name, n.type
-- FROM node n
-- WHERE n.repository_snapshot_id = :snapshot_id
-- AND n.type IN (10, 11, 12)  -- Class, Struct, Interface
-- LIMIT 100;
--
-- SELECT e.id, e.source_node_id, e.target_node_id, e.type
-- FROM edge e
-- WHERE e.repository_snapshot_id = :snapshot_id
-- AND (e.source_node_id IN (:node_ids) OR e.target_node_id IN (:node_ids))
-- LIMIT 500;
