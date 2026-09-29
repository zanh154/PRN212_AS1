# Thêm sinh viên vào phiên thi

Nhánh: `feature/add-student-to-exam-session`.

## Kiểm tra giao diện với dữ liệu MySQL

1. Đăng nhập Admin hoặc giảng viên phụ trách. Mở chi tiết phiên thi chưa bắt đầu.
2. Nhập email sinh viên đang hoạt động, chọn giờ trống trong ngày thi và bấm **Thêm sinh viên**.
3. Kiểm tra thông báo thành công, số sinh viên tăng một, ca mới ở trạng thái Chờ thi; khung giờ phiên thi mở rộng nếu cần.
4. Thêm lại cùng email: hiển thị lỗi sinh viên đã có, danh sách giữ nguyên.
5. Thử email không tồn tại, tài khoản giảng viên hoặc sinh viên không hoạt động: từ chối.
6. Thử khung giờ trùng sinh viên/giảng viên, ngày khác hoặc ca kéo dài qua ngày: từ chối.
7. Với phiên đã bắt đầu/hoàn thành/huỷ: không hiển thị form, POST trực tiếp cũng bị từ chối.
8. Giảng viên khác không thấy form và POST trực tiếp nhận 403. Student không được truy cập chức năng quản lý. POST thiếu antiforgery token bị từ chối.

## Kiểm tra tự động

`dotnet test tests/AssignmentPRN.Tests/AssignmentPRN.Tests.csproj`

Test dùng SQLite trong bộ nhớ, kiểm tra lưu dữ liệu, lịch sinh viên, mở rộng khung giờ, trùng sinh viên, trùng lịch, email không hợp lệ, tài khoản sai vai trò, phiên không tồn tại/đã đóng/quá khứ và ca vượt ngày. Chưa xác minh giao diện trong trình duyệt hoặc concurrency trên MySQL thực tế.
