# Quy tắc phiên thi dùng chung

Quy tắc trong `src/AssignmentPRN.Domain/ExamSessionRules.cs` là nguồn duy nhất cho điều kiện sửa phiên, chuyển trạng thái thủ công, hủy phiên, vào thi và thời gian nộp bù.

- Domain không tham chiếu project khác, EF Core hoặc ASP.NET Core.
- Business và DataAccess đều tham chiếu Domain. DataAccess không tham chiếu ngược Business.
- Hai lớp `ExamSessionRules` cũ là lớp chuyển tiếp để giữ nguyên API và kiểu enum hiện tại. Chúng chuyển kiểu và gọi Domain; không còn tự định nghĩa điều kiện hay thời gian nộp bù.
- Enum ở các tầng vẫn giữ để tránh đổi hợp đồng dữ liệu hiện có. Test kiểm tra tên/giá trị enum tương ứng, toàn bộ ma trận chuyển trạng thái, quy tắc hủy và biên thời gian.
- Mục 2 đã chuyển nghiệp vụ lưu tạm sang `Business/ExamDraftValidation`; repository gọi bộ kiểm tra trong transaction trước khi ghi.
- Mục 3 đặt quy tắc tự đồng bộ phiên và chốt ca quá hạn ở `Domain/ExamLifecycleRules`. Business và DataAccess cùng dùng quy tắc này, không định nghĩa lại điều kiện trong repository.

Kiểm chứng: build thành công, 121/121 test đạt (113 test hồi quy và 8 test mới), SQLite riêng. Không thay đổi schema hoặc dữ liệu thật. Các test cũ chưa commit không tương thích API vẫn được giữ nguyên và không thuộc bộ chạy này.

Sau mục 2 và 3: 139/139 test đạt; bao gồm 14 trường hợp mới cho quy tắc tự chuyển trạng thái và biên thời gian quá hạn. Chưa kiểm thử tải đồng thời trên MySQL.
