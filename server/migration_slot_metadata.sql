START TRANSACTION;

ALTER TABLE appointment ALTER COLUMN "PatientId" DROP NOT NULL;

ALTER TABLE appointment ADD "DurationMinutes" integer;

ALTER TABLE appointment ADD "Location" character varying(300);

ALTER TABLE appointment ADD "Provider" character varying(200);

ALTER TABLE appointment ADD "VisitType" character varying(20);

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260422134224_AddAppointmentSlotMetadata', '8.0.26');

COMMIT;

