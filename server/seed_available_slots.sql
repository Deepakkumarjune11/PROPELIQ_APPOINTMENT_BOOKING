INSERT INTO appointment (
  "Id","SlotDatetime","Status","Provider","VisitType","Location",
  "DurationMinutes","IsWalkIn","IsDeleted","CreatedAt","UpdatedAt"
)
SELECT
  gen_random_uuid(),
  d + (h * interval '1 hour'),
  'available',
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
  AND NOT EXISTS (SELECT 1 FROM appointment WHERE "Status" = 'available' LIMIT 1);
