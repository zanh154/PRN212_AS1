# AssignmentPRN system architecture

Sơ đồ phản ánh kiến trúc ba tầng hiện tại: `Presentation → Business → DataAccess`.

## Sơ đồ tổng quan ba tầng

![Kiến trúc ba tầng](kien-truc-tong-quan.svg)

Nét liền là request đi xuống, nét đứt là response đi lên. Business và DataAccess được build thành class library (`.dll`) và được Presentation tham chiếu.

## Sơ đồ chi tiết

![AssignmentPRN system architecture](architecture.svg)

Source Mermaid có thể chỉnh sửa tại [architecture.mmd](architecture.mmd). GitHub hiển thị trực tiếp SVG ở trên và vẫn cho phép xem source của sơ đồ.

## Luồng request đến database

```mermaid
sequenceDiagram
    actor User as Admin / Lecturer / Student
    participant Browser
    participant MVC as Presentation Controller
    participant Service as Business Service
    participant Repo as DataAccess Repository
    participant EF as AivesDbContext / EF Core
    participant DB as MySQL 8.4

    User->>Browser: Thao tác trên giao diện
    Browser->>MVC: HTTP request + session cookie
    MVC->>Service: Gọi service interface với request DTO
    Service->>Service: Kiểm tra quyền và business rules
    Service->>Repo: Query hoặc command
    Repo->>EF: LINQ / transaction
    EF->>DB: SQL qua Pomelo MySQL provider + TLS
    DB-->>EF: Result rows / affected rows
    EF-->>Repo: Entity hoặc read model nội bộ
    Repo-->>Service: DataAccess read model
    Service-->>MVC: ServiceResponse / response DTO
    MVC-->>Browser: Razor HTML hoặc redirect
    Browser-->>User: Hiển thị kết quả
```

## Cách kết nối MySQL

1. `Program.cs` đọc `ConnectionStrings:DefaultConnection` từ cấu hình ứng dụng hoặc biến môi trường.
2. `AddBusiness(...)` gọi `AddDataAccess(...)` trong lúc đăng ký dependency injection.
3. DataAccess gọi `UseMySql(connectionString, MySqlServerVersion(8.4.8))` để cấu hình `AivesDbContext`.
4. Repository nhận `AivesDbContext` theo request scope và dùng EF Core để sinh SQL.
5. `DatabaseInitializerHostedService` chạy lúc khởi động, gọi `CanConnectAsync` và đọc số lượng dữ liệu cơ bản. Nó không tự tạo schema hay seed dữ liệu.
6. Chuỗi kết nối mẫu dùng MySQL trên Aiven với `SslMode=Required`; mật khẩu thật không được commit vào Git.

## Luồng file tài liệu

`CourseMaterialsController → ICourseMaterialService → CourseMaterialService → LocalMaterialFileStore → src/AssignmentPRN.DataAccess/Storage/materials`

Thư mục lưu trữ nằm **ngoài `wwwroot`** là có chủ đích: mọi thứ dưới `wwwroot` đều được
`UseStaticFiles()` phục vụ thẳng, không qua controller, nên ai biết URL cũng tải được dù
chưa đăng nhập. Đặt ra ngoài thì mọi lượt tải đều phải đi qua `CourseMaterialsController.Download`,
nơi `[SessionAuthorize(Admin, Lecturer)]` kiểm tra phiên trước. Đường dẫn lấy từ cấu hình
`MaterialStorage:RootPath`; giá trị tương đối được tính theo content root, còn khi publish thì
đặt đường dẫn tuyệt đối vì thư mục project chỉ tồn tại trong bản checkout.

Controller chỉ gọi service của Business. Business điều phối metadata và file; việc tạo đường dẫn, ghi, đọc và xóa file nằm trong DataAccess implementation.

## Quy tắc phụ thuộc

- Presentation chỉ tham chiếu Business; không truy cập DataAccess, Repository hay `AivesDbContext`.
- Business chứa service interface, request/response DTO, enum công khai và các quy tắc nghiệp vụ; Business gọi DataAccess.
- DataAccess chứa EF Core, entity, read model nội bộ, repository, MySQL provider và local file-store implementation.
- Chiều tham chiếu project là `Presentation → Business → DataAccess`; không có project `Domain` riêng.

## Cấu trúc thư mục theo sơ đồ

Mỗi hộp trong sơ đồ tương ứng một thư mục thật; thêm file mới phải đặt đúng thư mục của hộp đó.

| Tầng | Hộp trong sơ đồ | Thư mục | Namespace |
|---|---|---|---|
| Presentation | Controllers | `Controllers/` | `AssignmentPRN.Presentation.Controllers` |
| Presentation | Razor Views | `Views/` | *(không tự khai báo — xem ghi chú dưới bảng)* |
| Presentation | ViewModels | `ViewModels/` | `AssignmentPRN.Presentation.ViewModels` |
| Business | Services | `Services/` | `AssignmentPRN.Business.Services` |
| Business | Business rules | `BusinessRules/` | `AssignmentPRN.Business.BusinessRules` |
| Business | Interfaces | `Interfaces/` | `AssignmentPRN.Business.Interfaces` |
| DataAccess | Repositories | `Repositories/` | `AssignmentPRN.DataAccess.Repositories` |
| DataAccess | AivesDbContext | `Data/` | `AssignmentPRN.DataAccess.Data` |
| DataAccess | Entity Models | `Entities/` | `AssignmentPRN.DataAccess.Entities` |

File `.cshtml` không khai báo namespace như file `.cs`. Lúc build, Razor sinh ra một lớp
cho mỗi view và đặt tất cả vào namespace `AspNetCoreGeneratedDocument`, tên lớp ghép từ
đường dẫn — `Views/Account/Login.cshtml` thành `AspNetCoreGeneratedDocument.Views_Account_Login`.
Có thể đổi bằng chỉ thị `@namespace`, nhưng dự án không dùng. Các `@using` dùng chung cho
mọi view khai báo một lần ở `Views/_ViewImports.cshtml`.

Ngoài ba hộp chính, mỗi tầng còn một ô nét đứt liệt kê phần không thuộc nghiệp vụ:

- **Presentation — "Hạ tầng · khởi động"**: `Program.cs` (đăng ký DI, middleware) và `appsettings.json` (`ConnectionStrings:DefaultConnection`, `MaterialStorage:RootPath`) ở gốc project; các thư mục `Constants/`, `Filters/`, `Properties/`, `Styles/`, `wwwroot/`.
- **Business — "Nối tầng"**: không có thư mục hạ tầng, nhưng có hai file ở gốc project là `ServiceCollectionExtensions.cs` (đăng ký DI) và `DataAccessMappings.cs` (chuyển DTO giữa hai tầng).
- **DataAccess — "Hạ tầng"**: `Contracts/`, `Enums/`, `Extensions/`, `Services/`, `Common/`; riêng `Storage/materials` được vẽ thành hình trụ "File tài liệu" nằm trong tầng.

Sơ đồ tổng quan vẽ đủ các mục này và ghi số lượng từng hộp, nên đọc sơ đồ là thấy hết thư mục thật. Project `tests/AssignmentPRN.Tests` cố ý không vẽ vì không chạy lúc runtime.
