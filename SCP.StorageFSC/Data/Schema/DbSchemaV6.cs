namespace SCP.StorageFSC.Data.Schema
{
    public sealed class DbSchemaV6 : DbSchemaBase
    {
        public override int CurrentSchemaVersion => 6;

        public override string Name => "Add tenant and description columns to background tasks";

        protected override string Sql => """
            ALTER TABLE background_tasks
                ADD COLUMN tenant_id BLOB NULL CHECK(tenant_id IS NULL OR length(tenant_id) = 16);

            ALTER TABLE background_tasks
                ADD COLUMN descr TEXT NOT NULL DEFAULT '';

            UPDATE background_tasks
            SET descr = CASE type
                WHEN 0 THEN 'Merge multipart upload'
                WHEN 1 THEN 'Check file storage database consistency'
                WHEN 2 THEN 'Cleanup deleted tenant files'
                WHEN 3 THEN 'Update tenant storage usage'
                ELSE 'Background task'
            END
            WHERE descr = '';

            CREATE INDEX IF NOT EXISTS ix_background_tasks_tenant_id
                ON background_tasks(tenant_id);
            """;
    }
}
