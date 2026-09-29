-- =====================================================================================
--  Module 5 - Sinh vien chon dap an trac nghiem | Script them cot
-- =====================================================================================
--  Muc dich
--    Bang `answers` duoc thiet ke cho thi van dap: transcript va audio_path. Khi sinh
--    vien tu vao thi va chon dap an trac nghiem thi khong co cho nao luu lua chon do.
--    Script nay them mot cot tro toi dong trong `question_options`.
--
--  Chay khi nao
--    Sau khi da chay 20260929_add_question_options.sql (can co bang question_options).
--    Chay 1 lan tren database dung chung cua nhom.
--
--  Cach chay
--    mysql -u root -p <ten_db> < 20260930_add_answer_selected_option.sql
--    (Workbench: File > Open > chon file nay > Execute)
--
--  An toan
--    - Idempotent: chay bao nhieu lan cung ra ket qua, khong loi.
--    - Chi THEM cot, khong xoa va khong sua dong du lieu nao.
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- 0. Kiem tra truoc: hai bang can co san
-- -------------------------------------------------------------------------------------
SELECT 'Prerequisite check' AS step, TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('answers', 'question_options')
ORDER BY TABLE_NAME;

-- -------------------------------------------------------------------------------------
-- 1. Them cot answers.selected_option_id
--
--    - NULL nghia la cau do sinh vien chua tra loi, hoac la cau van dap khong co
--      phuong an de chon. Vi vay cot phai cho phep NULL.
--    - ON DELETE SET NULL: xoa mot phuong an thi bai lam khong bi xoa theo, chi mat
--      lien ket. Khong dung CASCADE vi mat bai lam la mat du lieu thi that.
-- -------------------------------------------------------------------------------------
SET @column_exists = (
    SELECT COUNT(*) FROM information_schema.COLUMNS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'answers'
      AND COLUMN_NAME = 'selected_option_id'
);

SET @add_column = IF(
    @column_exists = 0,
    'ALTER TABLE answers ADD COLUMN selected_option_id INT NULL AFTER candidate_id',
    'SELECT ''answers.selected_option_id da ton tai, bo qua'' AS step'
);

PREPARE stmt FROM @add_column;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- -------------------------------------------------------------------------------------
-- 2. Khoa ngoai toi question_options
-- -------------------------------------------------------------------------------------
SET @fk_exists = (
    SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'answers'
      AND CONSTRAINT_NAME = 'fk_answers_selected_option'
);

SET @add_fk = IF(
    @fk_exists = 0,
    'ALTER TABLE answers ADD CONSTRAINT fk_answers_selected_option
        FOREIGN KEY (selected_option_id) REFERENCES question_options (option_id)
        ON DELETE SET NULL',
    'SELECT ''fk_answers_selected_option da ton tai, bo qua'' AS step'
);

PREPARE stmt FROM @add_fk;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- -------------------------------------------------------------------------------------
-- 3. Moi luot thi chi co mot bai lam cho moi cau
--
--    Sinh vien sua lai dap an thi phai ghi de dong cu chu khong them dong moi. Rang
--    buoc duy nhat nay chan viec mot cau co hai bai lam neu co hai request cung luc.
-- -------------------------------------------------------------------------------------
SET @index_exists = (
    SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'answers'
      AND INDEX_NAME = 'ux_answers_exam_question'
);

SET @add_index = IF(
    @index_exists = 0,
    'ALTER TABLE answers ADD UNIQUE INDEX ux_answers_exam_question (exam_question_id)',
    'SELECT ''ux_answers_exam_question da ton tai, bo qua'' AS step'
);

PREPARE stmt FROM @add_index;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- -------------------------------------------------------------------------------------
-- 4. Kiem tra ket qua - chay xong phai thay dung nhu phan "Ket qua mong doi"
-- -------------------------------------------------------------------------------------
SELECT '4.1 cot selected_option_id' AS check_name, COLUMN_NAME, COLUMN_TYPE, IS_NULLABLE
FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'answers'
  AND COLUMN_NAME = 'selected_option_id';

SELECT '4.2 khoa ngoai' AS check_name, COUNT(*) AS found
FROM information_schema.TABLE_CONSTRAINTS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'answers'
  AND CONSTRAINT_NAME = 'fk_answers_selected_option';

SELECT '4.3 rang buoc duy nhat' AS check_name, COUNT(DISTINCT INDEX_NAME) AS found
FROM information_schema.STATISTICS
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'answers'
  AND INDEX_NAME = 'ux_answers_exam_question';

-- =====================================================================================
--  Ket qua mong doi:
--    4.1 -> selected_option_id, int, YES
--    4.2 -> 1
--    4.3 -> 1
-- =====================================================================================
