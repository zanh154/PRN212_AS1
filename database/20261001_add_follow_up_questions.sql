-- =====================================================================================
--  Module 6 - Cau hoi dao sau & ket qua | Script them cot
-- =====================================================================================
--  Muc dich
--    Sau khi sinh vien nop vong 1 (cac cau chinh), he thong co the hoi them cau dao sau
--    cho tung cau chinh. Cau dao sau van la mot dong trong `exam_questions`, nhung phai
--    biet no dao sau cho cau chinh nao. Script nay them cot tro ve dong cau chinh do.
--
--  Chay khi nao
--    Sau 20260930_add_answer_selected_option.sql. Chay 1 lan tren database dung chung.
--
--  Cach chay
--    mysql -u root -p <ten_db> < 20261001_add_follow_up_questions.sql
--    (Workbench: File > Open > chon file nay > Execute)
--
--  An toan
--    - Idempotent: chay bao nhieu lan cung ra ket qua, khong loi.
--    - Chi THEM cot, khoa ngoai va index; khong xoa va khong sua dong du lieu nao.
--    - Dong cu co cot moi = NULL, nghia la cau chinh. Luong thi hien tai khong doi.
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- 0. Kiem tra truoc: bang exam_questions phai co san
-- -------------------------------------------------------------------------------------
SELECT 'Prerequisite check' AS step, TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'exam_questions';

-- -------------------------------------------------------------------------------------
-- 1. Them cot exam_questions.parent_exam_question_id
--
--    - NULL: cau chinh (vong 1).
--    - Co gia tri: cau dao sau, tro toi exam_question_id cua cau chinh no dao sau.
-- -------------------------------------------------------------------------------------
SET @column_exists = (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'exam_questions'
      AND COLUMN_NAME = 'parent_exam_question_id'
);

SET @add_column = IF(
    @column_exists = 0,
    'ALTER TABLE exam_questions ADD COLUMN parent_exam_question_id INT NULL AFTER question_id',
    'SELECT ''exam_questions.parent_exam_question_id da ton tai, bo qua'' AS step'
);

PREPARE stmt FROM @add_column;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- -------------------------------------------------------------------------------------
-- 2. Khoa ngoai tu tham chieu
--
--    ON DELETE CASCADE: cau dao sau khong co nghia neu cau chinh bi xoa (vi du huy de
--    de phat lai), nen xoa theo. De da co nguoi tra loi thi ung dung da chan xoa tu truoc.
--    MySQL tu tao index cho cot khoa ngoai, khong can them index rieng.
-- -------------------------------------------------------------------------------------
SET @fk_exists = (
    SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'exam_questions'
      AND CONSTRAINT_NAME = 'fk_exam_questions_parent'
);

SET @add_fk = IF(
    @fk_exists = 0,
    'ALTER TABLE exam_questions ADD CONSTRAINT fk_exam_questions_parent
        FOREIGN KEY (parent_exam_question_id) REFERENCES exam_questions (exam_question_id)
        ON DELETE CASCADE',
    'SELECT ''fk_exam_questions_parent da ton tai, bo qua'' AS step'
);

PREPARE stmt FROM @add_fk;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- -------------------------------------------------------------------------------------
-- 3. Kiem tra ket qua - chay xong phai thay dung nhu phan "Ket qua mong doi"
-- -------------------------------------------------------------------------------------
SELECT '3.1 cot parent_exam_question_id' AS check_name, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'exam_questions'
  AND COLUMN_NAME = 'parent_exam_question_id';

SELECT '3.2 khoa ngoai' AS check_name, COUNT(*) AS found
FROM information_schema.TABLE_CONSTRAINTS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'exam_questions'
  AND CONSTRAINT_NAME = 'fk_exam_questions_parent';

SELECT '3.3 so cau dao sau hien co' AS check_name, COUNT(*) AS found
FROM exam_questions
WHERE parent_exam_question_id IS NOT NULL;

-- =====================================================================================
--  Ket qua mong doi:
--    3.1 -> parent_exam_question_id, int, YES
--    3.2 -> 1
--    3.3 -> 0 (lan chay dau tien)
-- =====================================================================================
