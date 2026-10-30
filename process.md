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
- Thương hiệu:
  - đã đổi tên và tắt cập nhật, báo lỗi, thống kê gửi về server của Mission Planner;
  - logo, màu và website của FSEMI sẽ làm khi người dùng gửi;
  - chưa có server cập nhật.

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
| (PR đầu tiên vào `fsemi`) | Đổi tên thành FSEMI Mission Planner (exe `FSEMIMissionPlanner.exe`, thư mục dữ liệu riêng, bộ cài riêng với UpgradeCode mới), tắt cập nhật, báo lỗi và Google Analytics của Mission Planner, phiên bản 1.0.0 (build N), dịch toàn bộ giao diện sang tiếng Việt (chọn vi-VN trong Config → Planner → UI Language), không hỏi Altitude Angel và Drone ID lúc mở lần đầu, CI chạy tay kèm mở thử app và đăng bản test-build, README/PRIVACY/process.md |

## Đang làm

- PR đầu tiên (đổi tên, tắt server Mission Planner): chờ người dùng thử và merge vào `fsemi`.

## Cần làm

1. **Giao hàng**, lấy từ bigWing Command:
   - tab Giao hàng dưới HUD: nút KHOÁ / THẢ lớn, có hỏi xác nhận trước khi thả; dùng gripper
     ArduPilot hoặc một kênh servo;
   - menu chuột phải "Thả hàng tại đây" trên bản đồ Kế hoạch: bay tới điểm, hạ độ cao, thả, bay
     lên, quay về;
   - đánh dấu các lần thả trên bản đồ;
   - lớp chữ giao diện riêng (`FsemiStrings.resx` + `.vi-VN.resx`, `FsemiText`).

   Code bên bigWing: `Controls/Delivery*.cs`, `GCSViews/FlightDataDelivery.cs`,
   `GCSViews/FlightPlannerDelivery.cs`, `Utilities/BigWingText.cs`.
2. **Chụp ảnh và dựng mô hình 3D** cho công trình, đồi núi, dãy núi:
   - Công trình (lấy từ bigWing):
     - bay vòng quanh công trình hoặc dọc mặt tường, camera hướng vào công trình;
     - tính số ảnh và khoảng cách tầng theo camera và độ chồng ảnh, có sẵn danh sách camera phổ
       biến;
     - chia thành nhiều chuyến theo pin, lưu mỗi chuyến thành file;
     - sau chuyến bay: đặt tên ảnh, ghi GPS vào ảnh, xuất CSV, `geo.txt` và KML cho phần mềm
       dựng 3D.
   - Đồi núi, dãy núi (mới): bay theo lưới, giữ độ cao đều so với mặt đất (theo dữ liệu
     SRTM/địa hình), kèm các tuyến chụp nghiêng để dựng 3D, chia theo pin.

   Nên tách thành 2 PR: công trình, rồi địa hình.
3. Khi người dùng gửi: logo, biểu tượng, màu và website của FSEMI.
