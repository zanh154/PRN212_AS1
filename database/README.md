# Database — dump và dữ liệu thử

```
database/
  dump/aives_20261002.sql    <- Dump đầy đủ: schema + dữ liệu. Dùng để tạo DB mới.
  seed_test_flow.sql         <- Dữ liệu thử để chạy demo / kiểm thử.
```

---

## 1. Dựng database: import dump

```bash
# Tạo database rỗng
mysql -u <user> -p -e "CREATE DATABASE aives CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

# Import dump (schema + dữ liệu)
mysql -u <user> -p aives < database/dump/aives_20261002.sql
```

Dump xuất ngày 02/10/2026 từ database chung của nhóm, nên **đã có sẵn mọi thay đổi schema
từ trước tới nay**: bảng lớp học, bảng phương án trả lời, cột đáp án sinh viên chọn, cột
cấu hình chủ đề/độ khó của đề, cột câu hỏi đào sâu. Import xong là chạy được ngay, không
cần chạy thêm script nào.

Dump không chứa lệnh `USE` hay `CREATE DATABASE` nên import vào database đặt tên gì cũng được.

Trỏ ứng dụng vào DB vừa tạo bằng `ConnectionStrings:DefaultConnection` trong
`src/AssignmentPRN.Presentation/appsettings.json` (xem mẫu ở `appsettings.json.example`).

### Cách xuất dump mới

Khi schema đổi, xuất lại dump rồi thay file trong `dump/`:

```bash
mysqldump -u <user> -p --single-transaction --routines --default-character-set=utf8mb4 \
  <ten_db> > database/dump/aives_<yyyyMMdd>.sql
```

Đặt tên theo ngày để biết dump thuộc thời điểm nào. Giữ **một** file dump mới nhất trong
thư mục; xoá bản cũ để không ai import nhầm.

---

## 2. Dữ liệu thử để demo và kiểm thử

`seed_test_flow.sql` tạo một bộ dữ liệu đầy đủ cho cả luồng: môn học, lớp, câu hỏi, phiên
thi, thí sinh. Chạy **sau** khi đã import dump.

```bash
mysql -u <user> -p <ten_db> < database/seed_test_flow.sql
```

- Chạy lại bao nhiêu lần cũng được: đầu script tự xoá dữ liệu thử cũ rồi tạo lại với mốc
  giờ mới. Nó **chỉ đụng tới dữ liệu gắn mã `TFLOW` / email `tflow.*@test.local`**, không
  ảnh hưởng dữ liệu thật.
- Mật khẩu chung cho mọi tài khoản thử: `Test@1234`. Danh sách tài khoản và dữ liệu sinh ra
  nằm ở phần chú thích đầu file.
- Hai tài liệu PDF mà script tham chiếu không nằm trong Git. Chép từ `docs/samples/` vào
  thư mục lưu trữ trước khi test phần tài liệu:

```bash
cp docs/samples/bai-giang-01.pdf src/AssignmentPRN.DataAccess/Storage/materials/tflow-chu-de-1.pdf
cp docs/samples/bai-giang-01.pdf src/AssignmentPRN.DataAccess/Storage/materials/tflow-chu-de-2.pdf
```

---

## Script migration cũ

Trước đây thư mục này còn 6 script `2026*.sql` ghi lại từng lần đổi schema. Chúng đã được
xoá vì dump bao trùm toàn bộ kết quả của chúng — giữ lại chỉ gây nhầm, vì có script chạy
lần hai sẽ lỗi.

Nếu cần xem lại lý do một thay đổi schema nào đó, lấy từ lịch sử Git:

```bash
git log --diff-filter=D --name-only -- "database/2026*.sql"   # tìm commit đã xoá
git show <commit>^:database/20260929_add_question_options.sql  # xem nội dung file cũ
```

---

## Lưu ý

- **Không commit mật khẩu thật.** `appsettings.json` đã nằm trong `.gitignore`; chỉ commit
  `appsettings.json.example`.
- Dump chứa cả bảng `users`. Mật khẩu trong đó là chuỗi băm BCrypt, không phải mật khẩu
  gốc — nhưng email và họ tên thì là dữ liệu thật, cân nhắc trước khi chia sẻ ra ngoài nhóm.
