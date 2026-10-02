-- Reconcile legacy sessions. Safe to rerun; never alter candidate/answer records.
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
START TRANSACTION;
UPDATE exam_sessions AS s
SET s.status = 'Completed', s.updated_at = CURRENT_TIMESTAMP
WHERE s.status IN ('Scheduled', 'InProgress')
  AND EXISTS (SELECT 1 FROM exam_candidates c WHERE c.exam_id = s.exam_id)
  AND NOT EXISTS (
    SELECT 1 FROM exam_candidates c WHERE c.exam_id = s.exam_id
      AND (c.status IS NULL OR c.status NOT IN ('Completed', 'Absent', 'Cancelled'))
  );
SELECT ROW_COUNT() AS completed_sessions_updated;
COMMIT;
