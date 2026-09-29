-- =====================================================================================
--  Module 5 - Bo cau hoi (Question bank) | Script them / sua database
-- =====================================================================================
--  Muc dich
--    Bo cau hoi can mot bang luu cac dap an A/B/C/D cua moi cau. Schema cu khong co
--    bang do, va cot questions.expected_answer chi chua mot chuoi doan, khong the
--    luu danh sach dap an. Day la thay doi duy nhat ve schema.
--
--  Chay khi nao
--    1 database da co bang `questions` va `course_materials` (module truoc da tao).
--    2 Chay 1 lan duy nhat tren database dung chung cua nhom.
--
--  Cach chay
--    mysql -u root -p aives_db < 20260929_add_question_options.sql
--    (Workbench: File > Open > chon file nay > Execute)
--
--  An toan
--    - Idempotent: chay bao nhieu lan cung ra ket qua, khong loi.
--    - Khong xoa, khong reset du lieu cu.
--    - Chi sua nhung dong duoc ghi chu "duoc sua o Module 5".
-- =====================================================================================

-- -------------------------------------------------------------------------------------
-- 0. Kiem tra truoc: cau truc can co san
--    Script nay chi tao bang moi, khong tao lai schema module truoc. Neu bao loi
--    "Table doesn't exist" o buoc 1, database chua dung, hay chay script module truoc.
-- -------------------------------------------------------------------------------------
SELECT 'Prerequisite check' AS step, TABLE_NAME
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME IN ('questions', 'course_materials', 'question_options')
ORDER BY TABLE_NAME;

-- -------------------------------------------------------------------------------------
-- 1. Bang moi: question_options - cac dap an cua mot cau hoi
--
--    - is_correct: 1 = dap an dung. Moi cau co dung 1 dong is_correct = 1
--      (rang buoc do 2 luong kiem tra, khong phai rang buoc cua database).
--    - display_order: thu tu A/B/C/D hien thi, tinh tu 1.
--    - ON DELETE CASCADE: xoa cau hoi thi xoa luon dap an, khong de lai dong mo coi.
--    - Collation giong het voi cac bang con lai de tranh loi "Illegal mix of
--      collations" khi JOIN.
-- -------------------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS question_options (
    option_id     INT         NOT NULL AUTO_INCREMENT,
    question_id   INT         NOT NULL,
    option_text   TEXT        NOT NULL,
    is_correct    TINYINT(1)  NOT NULL DEFAULT 0,
    display_order INT         NOT NULL DEFAULT 1,
    PRIMARY KEY (option_id),
    KEY ix_question_options_question (question_id),
    CONSTRAINT fk_question_options_question
        FOREIGN KEY (question_id) REFERENCES questions (question_id)
        ON DELETE CASCADE
) ENGINE = InnoDB
  DEFAULT CHARSET = utf8mb4
  COLLATE = utf8mb4_unicode_ci;

-- -------------------------------------------------------------------------------------
-- 2. Index cho truy van lay ngan hang cau hoi
--
--    Cau hoi duoc chon bang dieu kien: thuoc mon + la Main + dang Approved.
--    Co san idx_questions_course va idx_questions_status rieng le, nhung khong dung
--    cho truy van 3 dieu kien. Index gop giup truy van nay.
-- -------------------------------------------------------------------------------------
SET @index_exists = (
    SELECT COUNT(*) FROM information_schema.STATISTICS
    WHERE TABLE_SCHEMA = DATABASE()
      AND TABLE_NAME = 'questions'
      AND INDEX_NAME = 'idx_questions_bank'
);

SET @add_index = IF(
    @index_exists = 0,
    'ALTER TABLE questions ADD INDEX idx_questions_bank (course_id, question_type, status)',
    'SELECT ''idx_questions_bank da ton tai, bo qua'' AS step'
);

PREPARE stmt FROM @add_index;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

-- -------------------------------------------------------------------------------------
-- 3. Sua du lieu: cau hoi Main dua vao Approved
--
--    Module 5 khong co buoc duyet cau hoi, dung luc tao. Database cu default la
--    'Draft' nen cau hoi tao tu truoc se khong bao gio duoc chon vao de thi.
--    Chi sua dong Main, giu nguyen FollowUp va giu nguyen Archived.
-- -------------------------------------------------------------------------------------
UPDATE questions
SET status = 'Approved'
WHERE question_type = 'Main'
  AND status IN ('Draft', 'PendingReview');

-- -------------------------------------------------------------------------------------
-- 4. Sua du lieu: tai lieu trong module 5 co san de dung ngay
--
--    Module 5 tai len file PDF/DOCX/PPTX roi dung lam chu de ngay, khong co buoc
--    xu ly gi them. Nhung cot course_materials.processing_status co san trong schema
--    mac dinh la 'Pending', dan den du lieu ghi sai so voi thuc te. Chuyen sang
--    'Completed' cho nhat quan.
--    Cot nay giu lai cho tinh nang AI doc noi dung tai lieu o cac module sau.
-- -------------------------------------------------------------------------------------
UPDATE course_materials
SET processing_status = 'Completed'
WHERE processing_status = 'Pending';

-- -------------------------------------------------------------------------------------
-- 5. Kiem tra ket qua - chay xong phai thay tat ca cac dong deu dung
--
--    5.1 question_options da ton tai va dung collation:
--    5.2 cau hoi Main khong con Draft/PendingReview:
--    5.3 tai lieu khong con Pending:
--    5.4 moi cau hoi co it nhat 2 dap an:
-- -------------------------------------------------------------------------------------
SELECT '5.1 question_options' AS check_name, TABLE_NAME, TABLE_COLLATION
FROM information_schema.TABLES
WHERE TABLE_SCHEMA = DATABASE()
  AND TABLE_NAME = 'question_options';

SELECT '5.2 questions still not Approved' AS check_name, COUNT(*) AS remaining
FROM questions
WHERE question_type = 'Main'
  AND status IN ('Draft', 'PendingReview');

SELECT '5.3 materials still Pending' AS check_name, COUNT(*) AS remaining
FROM course_materials
WHERE processing_status = 'Pending';

SELECT '5.4 questions with fewer than 2 options' AS check_name, COUNT(*) AS remaining
FROM questions q
WHERE (SELECT COUNT(*) FROM question_options o WHERE o.question_id = q.question_id) < 2;

-- =====================================================================================
--  Ket qua mong doi sau khi chay tren database moi (chua co du lieu cau hoi):
--    5.1 -> question_options, utf8mb4_unicode_ci
--    5.2 -> 0
--    5.3 -> 0
--    5.4 -> 0
-- =====================================================================================
