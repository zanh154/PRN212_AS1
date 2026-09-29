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

## Gợi ý tài khoản và xóa khỏi phiên thi

1. Bấm vào ô Sinh viên: hiển thị tối đa 25 tài khoản sinh viên đang hoạt động. Gõ `stu` hoặc tên/email để lọc; chọn bằng chuột hoặc phím mũi tên và Enter.
2. Sau khi chọn, sửa nội dung ô tìm kiếm: phải chọn lại một gợi ý trước khi thêm. Kiểm tra trạng thái không tìm thấy và lỗi mạng.
3. Bấm **Xóa khỏi phiên thi** ở sinh viên chờ thi. Hủy xác nhận: danh sách giữ nguyên. Đồng ý: sinh viên biến mất khỏi phiên, số lượng cập nhật.
4. Tài khoản, các phiên thi khác và khung giờ đã dành cho phiên thi giữ nguyên. Có thể thêm lại sinh viên vừa xóa.
5. Xóa sinh viên cuối cùng: phiên thi vẫn tồn tại, hiển thị danh sách rỗng.
6. Giảng viên khác, yêu cầu thiếu antiforgery token, candidate thuộc phiên khác, phiên đã bắt đầu hoặc sinh viên có dữ liệu bài thi đều không được phép xóa.

## Kiểm tra tự động

`dotnet test tests/AssignmentPRN.Tests/AssignmentPRN.Tests.csproj`

Test dùng SQLite trong bộ nhớ, kiểm tra lưu dữ liệu, lịch sinh viên, mở rộng khung giờ, trùng sinh viên, trùng lịch, email không hợp lệ, tài khoản sai vai trò, phiên không tồn tại/đã đóng/quá khứ và ca vượt ngày. Chưa xác minh giao diện trong trình duyệt hoặc concurrency trên MySQL thực tế.
