# Module 5 - Bộ câu hỏi (Question bank)

Trạng thái: **đã code xong, đã nối vào luồng tạo lịch thi, đã có unit test.**
Ngày cập nhật: 2026-09-29. Đã merge `TriNguyen` vào `QuocAnh_Dev`.

---

## 1. Cài đặt cho thành viên mới

```bash
git checkout QuocAnh_Dev
dotnet build AssignmentPRN.slnx
dotnet test AssignmentPRN.slnx
```

Sau đó chạy đúng **1 script SQL**:

```bash
mysql -u root -p aives_db < database/20260929_add_question_options.sql
```

Script an toàn khi chạy lại nhiều lần, không xóa dữ liệu cũ.
Chi tiết từng thay đổi DB nằm trong comment ngay trong file script.

> **Chưa chạy script thì thêm câu hỏi sẽ báo "Không thể lưu câu hỏi."**
> Đó là thông báo dự phòng của `ServiceExecutor` khi có lỗi ngoài dự kiến; lỗi thật
> là MySQL không tìm thấy bảng `question_options`. Nhập từ CSV cũng hỏng y như vậy.
> Chạy xong script, 4 câu `SELECT` ở cuối file phải trả về đúng như phần "Kết quả
> mong đợi" ghi trong đó.

---

## 2. Phạm vi đã làm

| Tầng | Nội dung |
|---|---|
| DataAccess | Entity `Question`, `QuestionOption`, `CourseMaterial`, `ExamQuestion`; enum; mapping `AivesDbContext`; repository + contract |
| Business | `QuestionService`, `CourseMaterialService`, `QuestionPicker`, `QuestionCsvReader` |
| Presentation | `QuestionsController`, `CourseMaterialsController`, màn hình phát đề của `ExamSessionsController`, viewmodel, 6 view, menu, SCSS |
| Tests | `tests/AssignmentPRN.Tests`: `QuestionPickerTests`, `QuestionCsvReaderTests` |

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

Tải mẫu tại `/Questions/Template`. File `.csv` hoặc `.txt`, phân cách bằng `;`
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
| GET | `/Questions/Template` | Tải file mẫu |
| GET | `/CourseMaterials` | Danh sách tài liệu |
| GET/POST | `/CourseMaterials/Upload` | Tải tài liệu lên |
| GET | `/CourseMaterials/Download/{id}` | Mở tài liệu |
| GET/POST | `/CourseMaterials/Edit/{id}` | Sửa môn học và tên tệp |
| POST | `/CourseMaterials/Delete/{id}` | Xóa (chặn nếu câu hỏi còn dùng) |

Luật khi sửa tài liệu:

- **Đổi môn** chỉ được khi tài liệu chưa là chủ đề của câu hỏi nào. Còn câu hỏi thì
  chặn, vì câu hỏi và chủ đề của nó phải nằm cùng một môn, mà chuyển cả câu hỏi theo
  có thể làm hỏng đề đã phát.
- **Đổi tên** thì lúc nào cũng được, kể cả khi đang có câu hỏi — nhưng phải giữ nguyên
  phần mở rộng, vì `file_type` được ghi từ đuôi tệp lúc tải lên và tệp trên đĩa không
  đổi. Tên này cũng là giá trị cột `material` của file CSV, đổi tên thì phải sửa CSV.
- Tên tệp không được trùng với tài liệu khác trong cùng môn.
- Tệp trên đĩa không bị đụng tới: `file_path` là GUID, `file_name` chỉ là nhãn.

Phát đề cho lượt thi nằm trong `ExamSessionsController`:

| Method | Đường dẫn | Công dụng |
|---|---|---|
| GET | `/ExamSessions/Questions/{id}` | Giao diện cấu hình + xem đề từng sinh viên |
| POST | `/ExamSessions/AssignQuestions` | Phát đề ngẫu nhiên theo cấu hình |
| POST | `/ExamSessions/ClearQuestions/{id}` | Huỷ đề để phát lại |

---

## 5b. Luồng phát đề (đã nối)

1. **Tạo lịch thi** (`POST /ExamSessions/Create`) → sau khi session và danh sách thi đã
   lưu, controller gọi ngay `IQuestionService.AssignToExamAsync` với
   `MainQuestionCount` của phiên thi và **toàn bộ** ngân hàng của môn.
2. Phát đề **thành công** → về `Details`, báo đã phát đề cho N sinh viên.
3. Ngân hàng **không đủ câu** → lịch thi vẫn được giữ (không rollback), người dùng được
   đưa thẳng sang `/ExamSessions/Questions/{id}` kèm lý do, để chọn lại phạm vi.
4. Ở màn hình đó có thể đổi **số câu mỗi sinh viên**, lọc theo **chủ đề** và **độ khó**;
   số câu khả dụng được tính lại theo đúng bộ lọc đang chọn trước khi bấm phát.
5. Chạy lại `AssignToExamAsync` chỉ bù cho sinh viên **chưa có đề**; sinh viên đã có đề
   được bỏ qua nên không ai bị phát chồng hai bộ câu.
6. `ClearExamAssignmentAsync` xoá đề để phát lại, và **từ chối** khi đã có câu được hỏi
   hoặc trả lời (`exam_questions.asked_at` / `is_completed`).

Vào màn hình phát đề từ: `Lịch thi → chi tiết phiên thi → thẻ "Ngân hàng câu hỏi"`.

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
| Build toàn solution + `dotnet test` | 0 warning, 0 error, 23/23 test pass |

### Unit test tự động (`dotnet test AssignmentPRN.slnx`)

| File | Kiểm tra |
|---|---|
| `QuestionPickerTests` | Đúng số câu; không trùng trong một đề; loại câu đã phát; pool cạn thì trả về phần còn lại thay vì ném lỗi; có xáo thật; **phát cả phiên thi 8 sinh viên × 5 câu không ai trùng ai** |
| `QuestionCsvReaderTests` | Đọc đúng dòng hợp lệ; `correct` dạng chữ và dạng số; giá trị mặc định `Medium`/`Understand`; dòng lỗi báo kèm số dòng mà dòng tốt vẫn vào; ô có dấu nháy chứa `;`; file phân cách bằng `,`; tài liệu không khớp thì để trống chủ đề; file rỗng / thiếu cột |

### Chưa kiểm thử

- Nhập CSV với tên tài liệu tiếng Việt có dấu.
- Sửa / Archive / xóa tài liệu / tải tài liệu qua giao diện.
- Phát đề trên database thật với phiên thi nhiều sinh viên (mới test ở tầng logic).

---

## 7. Ghi chú cho người tiếp tục

- Module 6 chưa động tới: DB có `questions.question_type = 'FollowUp'` nhưng chưa có
  `parent_question_id`; bảng `follow_up_questions` đang liên kết qua `answers`. Cần thống
  nhất lại khi làm phần câu hỏi nối tiếp.
- `course_materials.processing_status` và `questions.expected_answer` được giữ lại dù
  Module 5 chưa dùng tới, để khớp schema đã bàn giao.
- Cột `is_correct` không có ràng buộc "đúng 1 dòng" ở tầng database. Quy tắc này nằm ở
  service và viewmodel; nếu sau này có ai ghi thẳng bằng SQL thì phải tự giữ.
