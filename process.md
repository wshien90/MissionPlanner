# Tiến độ phát triển FSEMI Mission Planner

Ghi lại việc đã làm, đang làm và cần làm, để tiếp tục ở session mới. Cập nhật file này
mỗi khi xong một PR.

Cập nhật lần cuối: 2026-10-30.

## Sản phẩm

- **FSEMI Mission Planner**: trạm điều khiển mặt đất của FSEMI, dựa trên Mission Planner 1.3.83,
  phiên bản riêng bắt đầu từ 1.0.0.
- Repo `wshien90/MissionPlanner`:
  - `master` theo Mission Planner gốc;
  - `fsemi` là nhánh của sản phẩm, tách từ `master` ngày 2026-10-30;
  - nhánh làm việc là `claude/bigwing-uav-continue-26o9hv`. Mỗi tính năng một PR vào `fsemi`.
    Sau khi merge, đặt lại nhánh làm việc về `origin/fsemi`.
- Sản phẩm anh em: **bigWing Command** (repo `wshien90/bigWing-command`, nhánh `main`). Hai chức
  năng của FSEMI được lấy từ bigWing rồi điều chỉnh: giao hàng, và chụp ảnh để dựng mô hình 3D.
  Khi chép code từ bigWing:
  - đổi `bigWing`/`BigWing` thành `Fsemi`;
  - bỏ phần riêng của bigW (logo, theme, vùng bay Hàn Quốc, tiếng Hàn).
- Ngôn ngữ của các màn hình mới: tiếng Anh và tiếng Việt. Không làm tiếng Hàn.
- Thương hiệu: FSEMI = **FPT Semiconductor**, website https://fpt-semiconductor.com/.
  - Logo, biểu tượng và màu lấy từ website; nguồn và bảng màu ghi trong
    `fsemi/branding/README.md`.
  - Màu chính: navy `#010837`, xanh nhấn `#6388F7`. Màu logo: xanh `#2E65A5`, cam `#DB773A`,
    lục `#50A657`.
  - Chưa có server cập nhật.

## Cách làm việc đã thống nhất

- Trả lời người dùng bằng tiếng Việt.
- Mỗi tính năng một PR. Trước khi báo xong:
  - build trên Linux;
  - chạy unit test;
  - chạy thử trong app với SITL và chụp màn hình;
  - chạy CI **DotNet Build** (Actions → Run workflow, file `main.yml`, trên nhánh làm việc).
  
  Người dùng thử trên Windows rồi tự merge.
- Bản build thử: pre-release **test-build**
  (https://github.com/wshien90/MissionPlanner/releases/tag/test-build). Mỗi lần chạy CI sẽ thay
  bản cũ.
- Bảo mật: không dán key vào chat hay vào code trong repo.
- File `.cs`: CRLF và UTF-8 có BOM. File `.resx`: giữ nguyên kiểu xuống dòng có sẵn.

## Kỹ thuật (môi trường cloud Linux)

- Thử với SITL: kết nối qua giao diện (bấm TCP rồi Kết nối). Chạy app với `-port TCP` thì app bị
  treo ở hộp thoại lấy tham số trên Mono.

- Build:
  `dotnet build MissionPlanner.csproj --configuration Release -p:MissionPlannerLinuxBuild=true -p:EnableWindowsTargeting=true "-p:DirectoryBuildTargetsPath=$PWD/Linux/Build.targets" -p:MonoPosixPath=/usr/lib/mono/4.5/Mono.Posix.dll -v:q`
  Kết quả ra `bin/Release/net461/FSEMIMissionPlanner.exe`.
- Lần build đầu có thể báo lỗi chép `plugins/*.cs`: tạo thư mục `bin/Release/net461/plugins`
  rồi build lại.
- Chạy trên Mono cần `System.Speech.dll` trong thư mục build. File này không có trong repo; bản
  Windows có sẵn. Khi thử trên Linux, chép từ một bản build khác.
- Dữ liệu người dùng trên Linux nằm ở `~/.local/share/FSEMI Mission Planner/` (file
  `config.xml`; đặt `<language>vi-VN</language>` để có giao diện tiếng Việt).

## Đã làm

| PR | Nội dung |
|---|---|
| #1 | Đổi tên thành FSEMI Mission Planner (exe `FSEMIMissionPlanner.exe`, thư mục dữ liệu riêng, bộ cài riêng với UpgradeCode mới), tắt cập nhật, báo lỗi và Google Analytics của Mission Planner, phiên bản 1.0.0 (build N), dịch toàn bộ giao diện sang tiếng Việt (chọn vi-VN trong Config → Planner → UI Language), không hỏi Altitude Angel và Drone ID lúc mở lần đầu, logo/biểu tượng/màn hình khởi động FPT Semiconductor và theme FSEMI (navy #010837, xanh #6388F7) lấy từ website, bấm logo mở fpt-semiconductor.com, CI (chạy tay và với mỗi PR vào `fsemi`) kèm mở thử app và đăng bản test-build, README/PRIVACY/process.md |
| #2 | Giao hàng: tab Giao hàng (KHÓA/THẢ, gripper hoặc servo, trạng thái từ servo, danh sách lần thả), "Thả hàng tại đây" trên bản đồ Kế hoạch, đánh dấu điểm thả; chữ giao diện riêng `FsemiStrings.resx` (en, vi) |

## Đang làm

- PR **Chụp ảnh 3D cho công trình** vào `fsemi`: chờ người dùng thử và merge.
  - Bay vòng quanh công trình hoặc dọc mặt tường; camera và độ chồng ảnh; chia chuyến theo pin
    (thời gian bay một pin nhập trong hộp thoại); lưu từng chuyến thành file `.waypoints`.
  - `PhotoRecorder` ghi vị trí từng ảnh (CAMERA_FEEDBACK) từ lúc arm đến lúc disarm vào
    `photos/flight_<ngày>_<giờ>_photos.csv`.
  - Cửa sổ "Ảnh kiểm tra công trình" ở màn Dữ liệu: đặt tên ảnh theo tầng và hướng, ghi GPS vào
    ảnh, xuất `photos.csv`, `geo.txt` và `photos.kml`.
  - Code lấy từ bigWing: `Utilities/InspectionPattern.cs`, `CameraCoverage.cs`,
    `InspectionPhotos.cs`, `InspectionPhotoFiles.cs`, `InspectionFlightFiles.cs`,
    `Controls/InspectionForm.cs`, `InspectionPhotosForm.cs`, `GCSViews/FlightPlannerInspection.cs`.
  - Test: `InspectionPatternTests`, `CameraCoverageTests`, `InspectionPhotosTests`.

## Cần làm

1. **Chụp ảnh 3D cho đồi núi, dãy núi** (mới, chưa có bên bigWing):
   - bay theo lưới trên một vùng vẽ trên bản đồ;
   - giữ độ cao đều so với mặt đất, theo dữ liệu SRTM hoặc địa hình;
   - thêm các tuyến chụp nghiêng để dựng 3D;
   - chia theo pin và lưu file như phần công trình.

