-- Run once against the shared MySQL database before deploying the class-roster feature.
-- Existing users/courses are left untouched; class and membership rows must be assigned
-- from real school data after these tables are created.

CREATE TABLE IF NOT EXISTS academic_classes (
    class_id INT NOT NULL AUTO_INCREMENT,
    class_code VARCHAR(50) NOT NULL,
    class_name VARCHAR(200) NOT NULL,
    course_id INT NOT NULL,
    lecturer_id INT NOT NULL,
    is_active TINYINT(1) NOT NULL DEFAULT 1,
    created_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at DATETIME NULL,
    PRIMARY KEY (class_id),
    UNIQUE KEY uq_academic_classes_code (class_code),
    KEY ix_academic_classes_course (course_id),
    KEY ix_academic_classes_lecturer (lecturer_id),
    CONSTRAINT fk_academic_classes_course
        FOREIGN KEY (course_id) REFERENCES courses (course_id),
    CONSTRAINT fk_academic_classes_lecturer
        FOREIGN KEY (lecturer_id) REFERENCES users (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

CREATE TABLE IF NOT EXISTS class_students (
    class_id INT NOT NULL,
    student_id INT NOT NULL,
    joined_at DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (class_id, student_id),
    KEY ix_class_students_student (student_id),
    CONSTRAINT fk_class_students_class
        FOREIGN KEY (class_id) REFERENCES academic_classes (class_id)
        ON DELETE CASCADE,
    CONSTRAINT fk_class_students_student
        FOREIGN KEY (student_id) REFERENCES users (user_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
