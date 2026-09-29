# Chức năng 2 — Môn học và phiên thi

## Chạy dự án

Yêu cầu .NET 10 SDK. Sao chép `src/AssignmentPRN.Presentation/appsettings.json.example` thành `appsettings.json`, điền thông tin MySQL của nhóm. File cấu hình thật được Git bỏ qua.

```powershell
dotnet run --project src/AssignmentPRN.Presentation
dotnet test tests/AssignmentPRN.Tests/AssignmentPRN.Tests.csproj
```

Không cần migration cho thay đổi này. Ứng dụng sử dụng các bảng hiện có; kiểm thử dùng SQLite trong bộ nhớ, không ghi vào Aiven.

## Môn học

- Vào **Môn học** từ menu hoặc bảng điều khiển.
- Admin quản lý toàn bộ môn; giảng viên quản lý môn do mình phụ trách.
- Tạo/sửa mã môn, tên môn, mô tả, giảng viên, trạng thái hoạt động.
- Mã môn được trim, chuyển thành chữ hoa, kiểm tra trùng; giới hạn mã 50, tên 200, mô tả 1000 ký tự.
- Chỉ chọn giảng viên đang hoạt động.
- Môn có lớp, phiên thi hoặc dữ liệu liên quan không được xoá; dùng **Ngừng hoạt động** để giữ dữ liệu cũ.
- Môn ngừng hoạt động không được chọn để tạo phiên mới. Phiên cũ vẫn được giữ nguyên môn đó khi chỉnh sửa.

## Phiên thi

- Vào **Lịch thi → Tạo lịch thi**. Chọn môn, giảng viên, lớp và thời gian; danh sách sinh viên lấy từ lớp.
- Ngày bắt đầu không trong quá khứ, thời lượng 1–1440 phút, toàn bộ lượt thi phải kết thúc trong cùng ngày.
- Vào chi tiết → **Sửa phiên thi** để sửa tên, môn, mô tả, ngày giờ, thời lượng và số câu hỏi.
- Sửa không thay danh sách sinh viên hoặc giảng viên. Đổi môn vẫn giữ nguyên danh sách sinh viên hiện có.
- Nếu ngày giờ/thời lượng thay đổi, xếp lại các ca liên tiếp theo thứ tự hiện tại, giữ nguyên mã lượt thi; kiểm tra trùng lịch trước khi ghi. Nếu chỉ sửa thông tin, giữ các khung giờ đã điều chỉnh riêng.
- Chỉ sửa/xoá phiên Nháp hoặc Đã xếp lịch khi tất cả sinh viên còn Chờ thi. Giảng viên chỉ thao tác phiên mình phụ trách; admin thao tác toàn bộ.
- Các thao tác ghi dùng POST và chống giả mạo yêu cầu.

## Trạng thái

Phiên mới ở trạng thái **Đã xếp lịch**. Quy tắc chuyển:

| Hiện tại | Có thể chuyển sang |
|---|---|
| Nháp | Đã xếp lịch, Đã huỷ |
| Đã xếp lịch | Đang diễn ra, Đã huỷ |
| Đang diễn ra | Đã hoàn thành, Đã huỷ |
| Đã hoàn thành / Đã huỷ | Không chuyển tiếp |

Không bắt đầu trước giờ thi. Chỉ hoàn thành khi không còn sinh viên Chờ thi/Đang thi; việc cập nhật kết quả từng sinh viên thuộc module kết quả. Phiên đã huỷ không xuất hiện trong lịch sinh viên và không chiếm khung giờ. Không mở lại hoặc xoá phiên đã huỷ/hoàn thành.

## Kiểm tra thủ công

1. Đăng nhập admin, thêm môn có mã chưa dùng; sửa thông tin rồi xoá môn chưa được sử dụng.
2. Thử mã môn trùng và xoá môn đã có lớp: phải có thông báo, dữ liệu không mất.
3. Tạo phiên cho lớp có sinh viên; mở chi tiết và đối chiếu ngày giờ/thời lượng.
4. Sửa ngày giờ/thời lượng: kiểm tra danh sách và mã lượt thi được giữ nguyên. Sửa sang giờ trùng phiên khác: bị từ chối, lịch cũ không đổi.
5. Chuyển trạng thái theo bảng; thử bắt đầu sớm và hoàn thành khi còn sinh viên chờ thi: bị từ chối.
6. Dùng giảng viên khác mở URL sửa/xoá phiên hoặc môn không thuộc mình: bị từ chối. Sinh viên không được vào màn hình quản lý.

Các thao tác làm thay đổi dữ liệu nên thử trên dữ liệu thử nghiệm của nhóm, không dùng phiên thi thật.
