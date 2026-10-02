-- Apply once before running the updated application. Existing sessions retain all-topic/all-difficulty defaults.
ALTER TABLE exam_sessions ADD COLUMN question_scope_json TEXT NULL;
