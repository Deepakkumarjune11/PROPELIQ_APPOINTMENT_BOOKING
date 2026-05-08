-- BUG-013 fix: delete expired available slots so the idempotency guard below
-- checks only FUTURE available slots. Without this, slots seeded weeks ago
-- (now in the past but still Status='Available') block re-seeding forever.
-- NOTE: EF Core stores AppointmentStatus enum as PascalCase strings ('Available',
-- 'Booked', etc.) via HasConversion<string>(). SQL comparisons must match exactly.
DELETE FROM appointment
WHERE  "Status"       = 'Available'
  AND  "SlotDatetime" <= NOW();

-- Also remove any incorrectly-cased rows from previous seed runs
DELETE FROM appointment
WHERE  "Status" = 'available';

INSERT INTO appointment (
  "Id","SlotDatetime","Status","Provider","VisitType","Location",
  "DurationMinutes","IsWalkIn","IsDeleted","CreatedAt","UpdatedAt"
)
SELECT
  gen_random_uuid(),
  d + (h * interval '1 hour'),
  'Available',
  p.pname,
  p.vtype,
  CASE p.vtype WHEN 'telehealth' THEN 'Telehealth' ELSE 'PropelIQ Clinic - Suite 200' END,
  30,
  false,
  false,
  NOW(),
  NOW()
FROM
  generate_series(CURRENT_DATE + 1, CURRENT_DATE + 14, '1 day') d,
  generate_series(9, 16) h,
  (VALUES
    ('Dr. Sarah Chen',    'in-person'),
    ('Dr. Marcus Rivera', 'telehealth'),
    ('Dr. Priya Nair',    'in-person')
  ) AS p(pname, vtype)
WHERE
  h NOT IN (12, 13)
  -- BUG-013 fix: guard on FUTURE available slots only (PascalCase to match EF Core storage).
  -- Previously guarded on ANY available slot, so expired past slots blocked re-seeding.
  AND NOT EXISTS (
    SELECT 1 FROM appointment
    WHERE  "Status"       = 'Available'
      AND  "SlotDatetime" > NOW()
    LIMIT 1
  );
