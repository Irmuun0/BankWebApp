/*
    Copy this file into ./migrations and rename it:

    YYYYMMDD_NNN_short_description.sql

    Rules:
    - Do not edit migrations that were already applied.
    - Keep seed/demo data in separate seed scripts unless the data is required
      for the schema to work.
    - Prefer safe IF EXISTS / IF NOT EXISTS checks where practical.
*/

-- Example:
-- IF COL_LENGTH('dbo.users', 'example_column') IS NULL
-- BEGIN
--     ALTER TABLE dbo.users
--     ADD example_column NVARCHAR(100) NULL;
-- END
