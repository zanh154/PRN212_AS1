# Module 5 - Bộ câu hỏi (Question bank)

Trạng thái: **đã code xong, đã build và chạy thử trên database local.**
Ngày cập nhật: 2026-09-29. Nhánh: `TriNguyen`.

---

## 1. Cài đặt cho thành viên mới

```bash
git checkout TriNguyen
dotnet build src/AssignmentPRN.Presentation/AssignmentPRN.Presentation.csproj
```

Sau đó chạy đúng **1 script SQL**:

```bash
mysql -u root -p aives_db < database/20260929_add_question_options.sql
```

Script an toàn khi chạy lại nhiều lần, không xóa dữ liệu cũ.
Chi tiết từng thay đổi DB nằm trong comment ngay trong file script.

> Lưu ý: `AssignmentPRN.slnx` đang trỏ tới
> `tests/AssignmentPRN.Tests/AssignmentPRN.Tests.csproj` — file này **chưa có**.
> Vì vậy `dotnet build` trên toàn solution sẽ báo lỗi `MSB3202`.
> Hãy build từng project như lệnh trên.

---

## 2. Phạm vi đã làm

| Tầng | Nội dung |
|---|---|
| DataAccess | Entity `Question`, `QuestionOption`, `CourseMaterial`, `ExamQuestion`; enum; mapping `AivesDbContext`; repository + contract |
| Business | `QuestionService`, `CourseMaterialService`, `QuestionPicker`, `QuestionCsvReader` |
| Presentation | `QuestionsController`, `CourseMaterialsController`, viewmodel, 5 view, menu, SCSS |

Các controller mới chỉ hiện với vai trò `Admin` và `Lecturer`.

### Quy tắc đã chốt

- **Phân quyền:** `Admin` quản lý toàn bộ. `Lecturer` chỉ thao tác trên môn mình phụ
  trách; service chặn ở tầng Business, không phụ thuộc việc ẩn nút trên giao diện.
- **Chủ đề bắt buộc:** mỗi câu hỏi phải gắn đúng 1 tài liệu
  (`questions.source_material_id`). `CourseMaterial` chính là "chủ đề".
- **Không có bước duyệt:** câu hỏi trắc nghiệm tạo ra là `Approved` ngay.
  Dùng `Archived` để ngừng cấp phát, **không xóa** câu đã gán cho lượt thi.
- **Random:** mỗi sinh viên có bộ câu riêng, không trùng nhau trong cùng phiên.
  Kết quả được ghi snapshot vào `exam_questions` ngay khi tạo lượt thi.
- **Tài liệu:** chỉ PDF/DOCX/PPTX, tối đa 20 MB. CSV tối đa 2 MB.
  File lưu ở `wwwroot/materials`, `file_name` giữ tên gốc (để CSV trỏ tới được),
  `file_path` chứa tên GUID duy nhất trên đĩa.
- `course_materials.processing_status` luôn ghi `Completed` vì không có bước xử lý;
  cột được giữ lại cho tính năng đọc nội dung tài liệu bằng AI ở module sau.

---

## 3. Thay đổi database

Chỉ có **một** thay đổi schema, các phần còn lại là sửa dữ liệu:

| # | Loại | Nội dung |
|---|---|---|
| 1 | CREATE | Bảng `question_options` — lưu các đáp án A/B/C/D |
| 2 | INDEX | `idx_questions_bank (course_id, question_type, status)` cho truy vấn lấy ngân hàng câu hỏi |
| 3 | UPDATE | Câu `Main` còn `Draft`/`PendingReview` → `Approved` |
| 4 | UPDATE | Tài liệu còn `processing_status = 'Pending'` → `'Completed'` |

Lý do cần bảng `question_options`: cột `questions.expected_answer` chỉ chứa được
một chuỗi đoạn, không lưu được danh sách đáp án có đánh dấu đúng/sai và thứ tự.

Script có sẵn 4 câu `SELECT` kiểm tra ở cuối, chạy xong nhìn kết quả là biết đã đúng.

---

## 4. Luồng CSV import

Tải mẫu tại `/Questions/DownloadTemplate`. File `.csv` hoặc `.txt`, phân cách bằng `;`
hoặc `,`, có dòng tiêu đề:

```
question_text;difficulty;bloom;material;expected_answer;option_a;option_b;option_c;option_d;correct
```

- `difficulty`: `Easy` | `Medium` | `Hard`
- `bloom`: `Remember` | `Understand` | `Apply` | `Analyze`
- `material`: tên file tài liệu, ví dụ `demo.pdf` (bắt buộc)
- `correct`: `A`–`H` hoặc `1`–`8`, trỏ vào cột `option_...`
- Ô trống được bỏ qua; ô có dấu nháy kép hỗ trợ dấu `;` bên trong.

Mỗi dòng lỗi được báo riêng kèm số dòng, **dòng hợp lệ vẫn được nhập**. Không có câu hỏi
nào bị ghi nửa vời: toàn bộ thao tác của một dòng nằm trong một transaction.

---

## 5. API chính

| Method | Đường dẫn | Công dụng |
|---|---|---|
| GET | `/Questions?courseId=&keyword=&difficulty=&materialId=&page=` | Danh sách, lọc, phân trang |
| GET | `/Questions/Create?courseId=` | Tạo câu hỏi |
| POST | `/Questions/Save` | Lưu (dùng chung tạo và sửa) |
| POST | `/Questions/Archive/{id}` | Ngừng cấp phát |
| GET | `/Questions/Import?courseId=` | Nhập CSV |
| GET | `/Questions/DownloadTemplate` | Tải file mẫu |
| GET | `/CourseMaterials` | Danh sách tài liệu |
| GET/POST | `/CourseMaterials/Upload` | Tải tài liệu lên |
| GET | `/CourseMaterials/Download/{id}` | Mở tài liệu |
| POST | `/CourseMaterials/Delete/{id}` | Xóa (chặn nếu câu hỏi còn dùng) |

Random cho lượt thi nằm ở tầng Business:
`IQuestionService.AssignToExamAsync` → trả về `ExamQuestionInput` → repository ghi vào
`exam_questions`. Module tạo lượt thi sẽ gọi hàm này sau khi đã tạo session và danh sách
thi.

---

## 6. Kiểm thử đã thực hiện

| Hạng mục | Kết quả |
|---|---|
| Build DataAccess / Business / Presentation | 0 warning, 0 error |
| SQL chạy lần 1 và lần 2 | 0 lỗi, index không bị tạo trùng |
| GET trang List / Create / Import | HTTP 200 |
| Tạo tài liệu | `file_name=demo.pdf`, `file_path=/materials/{guid}.pdf`, `processing_status=Completed` |
| Tạo câu hỏi | 3 option, đúng 1 đáp án, `status=Approved`, đúng `source_material_id` |
| Từ chối 2 đáp án đúng / thiếu option / nội dung rỗng | Bị chặn, DB không thêm câu nào |
| Import CSV có dòng lỗi | Dòng hợp lệ vào DB, dòng lỗi báo kèm số dòng |

### Chưa kiểm thử

- Phân câu hỏi cho lượt thi thật (`AssignToExamAsync`) — mới chỉ có logic tầng Business.
- Nhập CSV với tên tài liệu tiếng Việt có dấu.
- Test tự động (`tests/AssignmentPRN.Tests`) — chưa tạo.
- Sửa / Archive / xóa tài liệu / tải tài liệu qua giao diện.

---

## 7. Ghi chú cho người tiếp tục

- Module 6 chưa động tới: DB có `questions.question_type = 'FollowUp'` nhưng chưa có
  `parent_question_id`; bảng `follow_up_questions` đang liên kết qua `answers`. Cần thống
  nhất lại khi làm phần câu hỏi nối tiếp.
- `course_materials.processing_status` và `questions.expected_answer` được giữ lại dù
  Module 5 chưa dùng tới, để khớp schema đã bàn giao.
- Cột `is_correct` không có ràng buộc "đúng 1 dòng" ở tầng database. Quy tắc này nằm ở
  service và viewmodel; nếu sau này có ai ghi thẳng bằng SQL thì phải tự giữ.
