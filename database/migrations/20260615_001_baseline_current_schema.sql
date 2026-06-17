/*
    Baseline migration.

    This project already had a working MSSQL schema managed through standalone
    SQL scripts before versioned migrations were introduced. This migration
    marks the current schema as the starting point for future changes.

    Do not add schema changes here. Create a new migration file instead.
*/

SELECT 1 AS baseline_current_schema;
