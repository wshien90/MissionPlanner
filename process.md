# Tiến độ phát triển FSEMI Mission Planner

Ghi lại việc đã làm, đang làm và cần làm, để tiếp tục ở session mới. Cập nhật file này
mỗi khi xong một PR.

Cập nhật lần cuối: 2026-11-11 (sau PR #9; ảnh hướng dẫn mới, deck trình bày, ý tưởng mô phỏng gió).

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
  - CI **DotNet Build** (Windows) và **Linux Build** tự chạy với mỗi PR vào `fsemi`. DotNet
    Build cũng chạy tay được (Actions → DotNet Build → Run workflow).
  - **Android Build** và **OSX Build** chỉ chạy khi bấm tay (người dùng đồng ý, để tiết kiệm phút
    chạy Actions; máy macOS tính gấp 10).
  - Có lần Actions bị tắt trên repo ("Actions has been disabled for this repository"): người dùng
    bật lại trong Settings → Actions, rồi chạy tay DotNet Build và Linux Build.

  Người dùng thử trên Windows rồi tự merge.
- Bản build thử: pre-release **test-build**
  (https://github.com/wshien90/MissionPlanner/releases/tag/test-build), file tải trực tiếp
  https://github.com/wshien90/MissionPlanner/releases/download/test-build/FSEMIMissionPlannerBeta.zip.
  Mỗi lần chạy CI sẽ thay bản cũ; tên bản ghi số build và nhánh, ví dụ "Test build 14 (7/merge)"
  (build 14 = code của `fsemi` sau PR #7).
- Bảo mật: không dán key vào chat hay vào code trong repo.
- File `.cs`: CRLF và UTF-8 có BOM. File `.resx`: giữ nguyên kiểu xuống dòng có sẵn.

## Kỹ thuật (môi trường cloud Linux)

- `docs/fsemi-sitl.parm` đã thử trên SITL ArduCopter (2026-11-02): gripper giả lập khoá ở PWM
  1900; camera servo 10 gửi CAMERA_FEEDBACK; servo gimbal 11 xoay theo DO_MOUNT_CONTROL
  (-90° → 1100, 0° → 1500); TERRAIN_ENABLE 1. MP gốc truyền `--defaults <copter.parm>`; FSEMI
  nối thêm `,<fsemi-sitl.parm>` (danh sách cách nhau bởi dấu phẩy, file sau thắng).

- Thử với SITL: kết nối qua giao diện (bấm TCP rồi Kết nối). Chạy app với `-port TCP` thì app bị
  treo ở hộp thoại lấy tham số trên Mono.
- Bay trong mô phỏng (đã thử, ghi trong mục 8.2 của hướng dẫn):
  - Copter mặc định không arm được ở AUTO (`AUTO_OPTIONS`). Chọn GUIDED → Arm, rồi trong khoảng
    10 giây bấm chuột phải trên bản đồ → Cất cánh; sau đó chuyển Auto để bay nhiệm vụ, RTL để về.
  - Không có nút "Start Mission" riêng; `Mission_Start` nằm trong danh sách lệnh ở tab Thao tác.
  - "Bay tới đây" lần đầu hỏi độ cao ("Enter Guided Mode Alt"); phải nhập thì máy bay mới bay.
  - Độ cao 0 m khi thả hàng nghĩa là máy bay chưa cất cánh.
  - Ba kiểu độ cao: Relative (so với điểm cất cánh, frame 3), Absolute (so với mực nước biển,
    frame 0), Terrain (so với mặt đất bên dưới, frame 10).

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
| #3 | Chụp ảnh 3D: công trình (bay vòng quanh, dọc mặt tường, camera và độ chồng ảnh, chia chuyến theo pin với thời gian bay một pin nhập trong hộp thoại, lưu từng chuyến thành file), `PhotoRecorder` ghi vị trí ảnh, "Ảnh kiểm tra công trình" (đặt tên, ghi GPS, CSV/geo.txt/KML); đồi núi, dãy núi (tuyến song song trên vùng đa giác, giữ độ cao theo địa hình của máy bay hoặc SRTM, lượt chụp nghiêng, chia chuyến); Android/OSX chỉ chạy tay |
| #4 | README cập nhật, hướng dẫn sử dụng `docs/huong-dan-su-dung.md` (kèm ảnh màn hình trong `docs/images`) |
| #5 | Mô phỏng: tab MÔ PHỎNG có ô "Thông số thử FSEMI (giao hàng, camera, gimbal)", bật sẵn, nạp thêm `docs/fsemi-sitl.parm` (chép cạnh exe) vào `--defaults` của SITL; mục 8 "Mô phỏng (SITL)" trong hướng dẫn |
| #6 | Hướng dẫn: sửa mục 8.2 (GUIDED → Arm → Cất cánh → Auto, không có nút Start Mission), ghi chú hộp thoại "Enter Guided Mode Alt" của "Bay tới đây", mục mới 1.4 "Ba kiểu độ cao: Relative, Absolute, Terrain", câu hỏi thường gặp về độ cao 0 m |
| #7 | `process.md`: PR #5, #6 và cách bay trong mô phỏng |
| #8 | README: link tải bản build thử, sửa mô tả DotNet Build (chạy cả với PR vào `fsemi`), link tới mục 1.4; hướng dẫn mục 1.1: cảnh báo SmartScreen, Unblock file zip. Đã rà lại hướng dẫn: tên nút/menu khớp giao diện tiếng Việt, link nội bộ và ảnh đều đúng |
| #9 | Hướng dẫn: thay ảnh màn hình bằng ảnh người dùng chụp trên Windows (build 15, bản đồ vệ tinh), thêm ảnh đang bay vòng quanh (`13`) và bay địa hình (`14`); ảnh build 10 lưu ở `docs/images/archive/` |

## Đang làm

Không có. PR #9 đã merge. Người dùng chuẩn bị trình bày với đồng nghiệp bằng deck "FSEMI Mission
Planner" (Artifact https://claude.ai/artifact/XLDsCnqBjyA6u2xyvAi34s, 45 slide, cùng giao diện deck
FC737; slide 44 còn ô `[__]` chờ điền). Chờ người dùng chọn việc tiếp theo trong danh sách dưới.

## Cần làm / ý tưởng tiếp theo

1. **Tải trước dữ liệu SRTM cho vùng bay**: một nút trong hộp thoại chụp ảnh địa hình (hoặc menu
   Đa giác) tải đủ ô SRTM của vùng khi còn mạng, để ngoài hiện trường không cần Internet.
   - SRTM là dữ liệu độ cao mặt đất toàn cầu, ô khoảng 30–90 m, sai số khoảng ±5–10 m, đo theo
     ngọn cây và mái nhà. Phần mềm tải từ máy chủ ArduPilot, lưu ở thư mục `srtm` trong
     dữ liệu người dùng.
   - Dùng cho: giữ độ cao theo SRTM, gửi dữ liệu địa hình cho máy bay (TERRAIN_REQUEST), hiện
     độ cao thấp nhất và cao nhất của vùng.
2. **Thử bay thật phần địa hình**: kiểm tra `TERRAIN_ENABLE` và dữ liệu địa hình trên máy bay; ở
   vùng có rừng, bay từ 60 m trở lên; nếu có rangefinder hoặc lidar, cân nhắc thêm tuỳ chọn giữ
   độ cao theo cảm biến.
3. **Gửi ảnh lên WebODM** để tự dựng mô hình 3D, từ cửa sổ "Ảnh kiểm tra công trình" (dùng
   `geo.txt`). Cần máy chủ WebODM (URL và tài khoản).
4. **Danh sách điểm giao hàng** hay dùng: lưu sẵn tên, toạ độ, độ cao thả; chọn khi lập nhiệm vụ.
5. Tự học thời gian bay của pin sau mỗi chuyến (như bigWing, cần bộ ghi chuyến bay), thay cho ô
   nhập tay.
6. Giọng nói tiếng Việt cho cảnh báo thả hàng (hiện đang nói tiếng Anh).
7. **Mô phỏng có gió**, để thử chia chuyến theo pin (30% dự phòng có đủ khi ngược gió không) và
   thả hàng khi có gió. SITL ArduPilot đã có sẵn tham số: `SIM_WIND_SPD` (m/s), `SIM_WIND_DIR`
   (độ, hướng bắc thật), `SIM_WIND_TURB` (gió giật, m/s), `SIM_WIND_T` / `SIM_WIND_T_ALT` (gió
   thay đổi theo độ cao). Hiện chỉnh tay được ở CẤU HÌNH → Tham số đầy đủ, có hiệu lực ngay. Hai
   cách làm, người dùng chưa chọn:
   - thêm sẵn một mức gió (ví dụ 5 m/s) vào `docs/fsemi-sitl.parm`;
   - thêm ô "Gió" (tốc độ, hướng, gió giật) trên tab MÔ PHỎNG, cạnh ô "Thông số thử FSEMI",
     ghi các tham số trên khi khởi động hoặc khi đang bay.
