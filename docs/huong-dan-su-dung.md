# Hướng dẫn sử dụng FSEMI Mission Planner

Tài liệu này hướng dẫn dùng các chức năng riêng của **FSEMI Mission Planner** (phiên bản 1.0.0):

- giao hàng;
- chụp ảnh để dựng mô hình 3D cho công trình;
- chụp ảnh để dựng mô hình 3D cho đồi núi, dãy núi;
- chia chuyến theo pin;
- xử lý ảnh sau chuyến bay;
- thử tất cả các chức năng trên bằng máy bay giả lập, không cần drone thật.

Các chức năng còn lại giống Mission Planner, xem tài liệu tại
[ardupilot.org/planner](https://ardupilot.org/planner/).

Ảnh minh hoạ chụp từ giao diện tiếng Việt, khi chạy với máy bay giả lập (SITL), nên bản đồ nền
không có ảnh vệ tinh.

## Mục lục

1. [Bắt đầu](#1-bắt-đầu)
2. [Giao hàng](#2-giao-hàng)
3. [Chụp ảnh 3D cho công trình](#3-chụp-ảnh-3d-cho-công-trình)
4. [Chụp ảnh 3D cho đồi núi, dãy núi](#4-chụp-ảnh-3d-cho-đồi-núi-dãy-núi)
5. [Chia chuyến theo pin](#5-chia-chuyến-theo-pin)
6. [Xử lý ảnh sau chuyến bay](#6-xử-lý-ảnh-sau-chuyến-bay)
7. [Dựng mô hình 3D](#7-dựng-mô-hình-3d)
8. [Mô phỏng (SITL): thử khi chưa có drone](#8-mô-phỏng-sitl-thử-khi-chưa-có-drone)
9. [Câu hỏi thường gặp](#9-câu-hỏi-thường-gặp)

---

## 1. Bắt đầu

### 1.1. Cài đặt và mở phần mềm

1. Tải `FSEMIMissionPlannerBeta.zip` (bản build thử ở mục
   [Releases → test-build](https://github.com/wshien90/MissionPlanner/releases/tag/test-build)).
2. Giải nén vào một thư mục, ví dụ `C:\FSEMI Mission Planner`.
3. Chạy `FSEMIMissionPlanner.exe`.

FSEMI Mission Planner có thư mục dữ liệu riêng (`Documents\FSEMI Mission Planner`), nên cài chung
máy với Mission Planner hay bigWing Command được mà không lẫn cài đặt.

![Màn hình khởi động](images/01-man-hinh-khoi-dong.png)

### 1.2. Chọn tiếng Việt

1. Bấm **CONFIG** (CẤU HÌNH) → **Planner**.
2. Ở **UI Language**, chọn **Tiếng Việt (vi-VN)**.
3. Đóng rồi mở lại phần mềm.

Ở cùng màn hình này:

- **Giao diện (Theme)**: mặc định là `FSEMI.mpsystheme` (màu FPT Semiconductor). Theme xanh lá
  của Mission Planner là `BurntKermit.mpsystheme`.
- **Giọng nói**: bật **Bật giọng nói** để phần mềm báo khi thả hàng.

![Màn hình Cấu hình](images/03-cau-hinh-theme.png)

### 1.3. Màn hình chính

- **DỮ LIỆU**: theo dõi chuyến bay (HUD, bản đồ, các tab bên dưới HUD).
- **KẾ HOẠCH**: lập nhiệm vụ trên bản đồ.

Các chức năng FSEMI nằm ở:

- tab **Giao hàng** dưới HUD;
- menu chuột phải trên bản đồ **KẾ HOẠCH** (Thả hàng tại đây, Kiểm tra công trình);
- menu chuột phải trên bản đồ **DỮ LIỆU** (Ảnh kiểm tra công trình...).

Bấm logo FPT Semiconductor ở góc trên bên phải sẽ mở trang
[fpt-semiconductor.com](https://fpt-semiconductor.com/).

![Màn hình Dữ liệu](images/02-man-hinh-du-lieu.png)

---

## 2. Giao hàng

### 2.1. Tab Giao hàng: khoá và thả hàng

Mở **DỮ LIỆU** → tab **Giao hàng** (ngay sau tab Nhanh).

![Tab Giao hàng](images/04-tab-giao-hang.png)

- **Trạng thái** (dải màu trên cùng): **ĐÃ KHÓA** (xanh lá), **ĐÃ THẢ** (cam) hoặc **KHÔNG RÕ**
  (xám). Phần mềm đọc trạng thái từ đầu ra servo của móc hàng.
- **KHÓA**: gửi lệnh khoá móc hàng.
- **THẢ**: phần mềm hỏi lại kèm độ cao và tốc độ hiện tại, rồi mới gửi lệnh thả. Nếu máy bay
  chưa arm, phần mềm cũng nhắc.
- **Cách thả**:
  - **Auto**: nếu máy bay bật `GRIP_ENABLE` thì dùng gripper của ArduPilot, nếu không thì dùng
    servo;
  - **Gripper**: lệnh `DO_GRIPPER`, dùng `GRIP_GRAB` / `GRIP_RELEASE` và servo có
    `SERVOn_FUNCTION = 28`;
  - **Servo**: lệnh `DO_SET_SERVO` trên kênh **Servo** (mặc định 9), với **PWM khóa** (mặc định
    1100) và **PWM thả** (mặc định 1900).
- **Các lần thả**: danh sách thời gian, toạ độ, độ cao của mỗi lần thả trong phiên làm việc.
  - Lần thả do máy bay tự làm (theo nhiệm vụ hoặc công tắc RC) cũng được ghi, kèm chữ
    "(mission/RC)".
  - Mỗi lần thả có một điểm màu cam trên bản đồ **DỮ LIỆU**.

> **An toàn:** thử KHÓA/THẢ khi máy bay còn dưới đất và chưa gắn hàng, để chắc PWM khoá/thả
> đúng với móc của bạn.

### 2.2. Lập nhiệm vụ thả hàng

1. Mở **KẾ HOẠCH**. Bảo đảm ô loại nhiệm vụ đang là **MISSION** (không phải Fence hay Rally).
2. Chuột phải vào điểm cần giao trên bản đồ → **Thả hàng tại đây**.
3. Điền hộp thoại:
   - **Độ cao hành trình**: độ cao bay tới và rời điểm thả.
   - **Độ cao thả**: độ cao lúc thả. Phải thấp hơn độ cao hành trình.
   - **Lơ lửng trước khi thả** / **sau khi thả** (giây): cho máy bay đứng yên trước và sau khi
     mở móc.
   - **Thả bằng**: Gripper (`DO_GRIPPER`) hoặc Servo (`DO_SET_SERVO`).
   - **Khóa móc lại sau khi bay lên** và **Quay về điểm cất cánh sau khi thả**: tuỳ chọn.
4. Bấm **Thêm vào mission**.

![Hộp thoại Thả hàng](images/05-tha-hang-tai-day.png)

Phần mềm thêm vào cuối nhiệm vụ lần lượt:

1. bay tới điểm ở độ cao hành trình;
2. hạ xuống độ cao thả và lơ lửng;
3. thả;
4. lơ lửng;
5. bay lên độ cao hành trình;
6. (tuỳ chọn) khoá móc;
7. (tuỳ chọn) RTL.

Điểm thả được đánh dấu màu cam trên bản đồ. Muốn giao nhiều điểm, lặp lại cho từng điểm và bỏ
chọn RTL ở các điểm trước điểm cuối.

![Nhiệm vụ thả hàng](images/06-nhiem-vu-tha-hang.png)

Cuối cùng bấm **Ghi** để nạp nhiệm vụ vào máy bay.

---

## 3. Chụp ảnh 3D cho công trình

Dùng cho tháp, cột điện, trụ cầu, toà nhà: máy bay bay từng tầng, từ trên xuống, camera luôn
hướng vào công trình, chụp một ảnh ở mỗi điểm.

**Chuẩn bị:**

- Camera được điều khiển chụp bằng `DO_DIGICAM_CONTROL`, ví dụ `CAM1_TYPE` và servo hoặc relay
  kích chụp.
- Máy bay có gimbal, hoặc là multicopter tự xoay mũi theo ROI.

### 3.1. Bay vòng quanh công trình (tháp, cột, trụ)

1. **KẾ HOẠCH** → chuột phải vào **tâm công trình** → **Kiểm tra công trình** → **Bay vòng
   quanh công trình tại đây...**
2. Chọn **Camera**, ví dụ *Sony A6400 16mm (A6100, A6600)*, đây là mặc định. Chọn **Tự nhập**
   nếu muốn nhập số tay.
3. Điền:
   - **Độ chồng giữa các ảnh** (mặc định 80%) và **Độ chồng giữa các tầng bay** (mặc định 70%);
   - **Bán kính công trình**: bán kính của công trình, khoảng từ tâm ra mặt ngoài;
   - **Bán kính**: bán kính vòng bay. Phải lớn hơn bán kính công trình, an toàn ít nhất 5 m;
   - **Độ cao trên cùng** / **dưới cùng**: tầng bay cao nhất và thấp nhất (so với điểm cất cánh);
   - **Chiều bay**: theo hoặc ngược chiều kim đồng hồ.
4. Phần mềm tự tính **Số ảnh mỗi vòng** và **Khoảng cách tầng**. Dòng chữ dưới các ô cho biết
   mỗi ảnh phủ bao nhiêu mét và bao nhiêu mm mỗi pixel. Muốn thấy vết nứt nhỏ thì nên dưới 2 mm
   mỗi pixel.
5. Xem phần tóm tắt (số lệnh, số ảnh, quãng đường, thời gian) và các cảnh báo màu đỏ.
6. Bấm **Thêm vào mission**.

![Bay vòng quanh công trình](images/07-bay-vong-cong-trinh.png)

### 3.2. Bay dọc mặt tường (mặt đứng toà nhà)

1. Chuột phải vào **một đầu tường** → **Kiểm tra công trình** → **Mặt đứng: đầu tường tại
   đây**.
2. Chuột phải vào **đầu kia của tường** → **Kiểm tra công trình** → **Mặt đứng: cuối tường tại
   đây...**
3. Chọn **Bay ở phía** (bên máy bay tới, hoặc bên kia tường) và **Khoảng cách tới tường**.
4. Các ô còn lại giống phần 3.1. Phần mềm tự tính **Khoảng cách chụp dọc tường**.

### 3.3. Gợi ý

- Độ cao dưới cùng ít nhất 3 m. RTL_ALT phải cao hơn công trình, vì RTL bay thẳng về.
- Dưới 5 m tới công trình là nguy hiểm, phần mềm sẽ cảnh báo.
- Nhiệm vụ quá dài thì bật **Chia thành nhiều chuyến theo pin** (xem [phần 5](#5-chia-chuyến-theo-pin)).

---

## 4. Chụp ảnh 3D cho đồi núi, dãy núi

Máy bay bay đi bay về theo các tuyến song song trên một vùng, ở **độ cao cố định so với mặt
đất**. Camera chụp theo quãng đường. Có thể thêm **lượt chụp nghiêng** vuông góc để thấy rõ sườn
dốc và vách đá.

### 4.1. Vẽ vùng cần chụp

1. **KẾ HOẠCH** → chuột phải trên bản đồ → **Đa giác** → **Vẽ đa giác**.
2. Bấm lần lượt các điểm bao quanh đồi hoặc dãy núi, ít nhất 3 điểm. Đường đỏ là ranh giới vùng.

### 4.2. Lập nhiệm vụ chụp địa hình

1. Chuột phải trong vùng → **Kiểm tra công trình** → **Chụp ảnh địa hình vùng đa giác (3D)...**
2. Điền hộp thoại:
   - **Camera**, **Độ cao so với mặt đất** (mặc định 60 m), **Độ chồng giữa các ảnh** (80%),
     **Độ chồng giữa các tuyến bay** (70%). Phần mềm báo mm mỗi pixel, khoảng cách giữa các
     tuyến và khoảng cách giữa các lần chụp.
   - **Hướng các tuyến bay (°)**: mặc định theo cạnh dài nhất của vùng, để ít phải quay đầu.
   - **Thêm lượt chụp nghiêng vuông góc** (bật sẵn) và **Góc nghiêng camera** (mặc định 45° dưới
     phương ngang). Nên bật cho vùng dốc, vách đá, thung lũng hẹp.
   - **Giữ độ cao theo** (xem 4.3).
   - **Chia thành nhiều chuyến theo pin** và **Thời gian bay một pin (phút)**.
3. Xem phần tóm tắt:
   - số tuyến, số lệnh, số ảnh ước tính, quãng đường, thời gian bay;
   - độ cao thấp nhất và cao nhất của mặt đất (khi đã có dữ liệu SRTM);
   - danh sách các chuyến.
4. Bấm **Thêm vào mission**.

![Chụp ảnh địa hình](images/10-chup-anh-dia-hinh.png)

![Nhiệm vụ chụp địa hình](images/11-nhiem-vu-dia-hinh.png)

### 4.3. Giữ độ cao so với mặt đất

| Lựa chọn | Cách hoạt động | Cần gì |
|---|---|---|
| **Dữ liệu địa hình của máy bay (TERRAIN_ENABLE)** (khuyên dùng) | Waypoint dùng khung độ cao theo địa hình (Frame 10). Máy bay tự giữ độ cao so với mặt đất, kể cả giữa hai waypoint. | Đặt `TERRAIN_ENABLE = 1`. Máy bay cần dữ liệu địa hình, có sẵn trên thẻ nhớ hoặc do phần mềm gửi khi đang kết nối. Phần mềm cảnh báo nếu `TERRAIN_ENABLE = 0`. |
| **Dữ liệu độ cao (SRTM) của phần mềm** | Phần mềm tra độ cao mặt đất dọc mỗi tuyến, cứ 30 m thêm một waypoint, ở độ cao = mặt đất + độ cao đã chọn (so với mực nước biển). | Dữ liệu SRTM của vùng đã được tải về máy. |

**SRTM là gì?** SRTM là dữ liệu độ cao mặt đất của gần như toàn Trái Đất, do NASA đo bằng radar
năm 2000.

- **Độ chi tiết:** ô khoảng 30–90 m.
- **Sai số:** khoảng ±5–10 m.
- **Đo mặt trên cùng:** ngọn cây, mái nhà, không phải đất trần.
- **Nguồn:** phần mềm tự tải từ Internet và lưu ở thư mục `srtm`.

> **Ngoài hiện trường không có mạng:** trước khi đi, mở vùng bay trên bản đồ và mở hộp thoại
> chụp ảnh địa hình một lần khi còn mạng, để phần mềm tải sẵn dữ liệu SRTM.
>
> **An toàn:** vì sai số và cây cối, ở vùng có rừng nên bay từ 60 m trở lên so với mặt đất. Bay
> cao hơn 120 m so với mặt đất cần giấy phép bay, phần mềm sẽ cảnh báo.

---

## 5. Chia chuyến theo pin

Áp dụng cho cả chụp ảnh công trình và chụp ảnh địa hình.

1. Bật **Chia thành nhiều chuyến theo pin (mỗi chuyến kết thúc bằng RTL)** và nhập **Thời gian
   bay một pin (phút)**.
2. Phần mềm luôn chừa **30% dự phòng**.
   - Ví dụ pin bay 20 phút thì mỗi chuyến tối đa 14 phút.
   - Thời gian tính cả cất cánh, hạ cánh và quãng đường bay từ home tới và về.
3. Cách chia:
   - công trình: chia theo **tầng**, từ trên xuống;
   - địa hình: chia theo **tuyến**, theo thứ tự.

   Danh sách các chuyến hiện ngay trong hộp thoại.
4. Khi bấm **Thêm vào mission**:
   - **chuyến 1** được đưa vào nhiệm vụ;
   - **mỗi chuyến** được lưu thành một file nhiệm vụ riêng, trong
     `Documents\FSEMI Mission Planner\missions\inspection_<ngày>_<giờ>\flight_<n>_of_<tổng>.waypoints`.

![Đã lưu file các chuyến](images/08-luu-file-cac-chuyen.png)

**Bay các chuyến tiếp theo:**

1. Bay xong chuyến 1, hạ cánh, thay pin.
2. **KẾ HOẠCH** → chuột phải → **Kiểm tra công trình** → **Các chuyến kiểm tra** → chọn chuyến
   tiếp theo. Phần mềm hỏi lại, rồi thay các dòng của chuyến cũ bằng chuyến mới.
3. Bấm **Ghi** để nạp vào máy bay.

Ở máy khác, hoặc khi đã đóng phần mềm, dùng **Nạp file** để mở file `.waypoints` của chuyến cần
bay. Mục **Mở thư mục chứa file các chuyến** mở nhanh thư mục này.

---

## 6. Xử lý ảnh sau chuyến bay

Trong mỗi chuyến bay, từ lúc arm đến lúc disarm, phần mềm ghi lại **vị trí chụp từng ảnh** (từ
CAMERA_FEEDBACK của máy bay). Các vị trí được lưu ở
`Documents\FSEMI Mission Planner\photos\flight_<ngày>_<giờ>_photos.csv`. Phần mềm cần đang kết
nối với máy bay trong suốt chuyến bay.

Sau chuyến bay:

1. Chép ảnh từ thẻ nhớ camera vào một thư mục trên máy tính.
2. **DỮ LIỆU** → chuột phải trên bản đồ → **Ảnh kiểm tra công trình...**
3. Điền:
   - **Thư mục ảnh**: thư mục vừa chép ảnh vào;
   - **Chuyến bay**: chuyến đã chụp, chuyến mới nhất ở trên cùng;
   - **Tên công trình**: dùng để đặt tên ảnh;
   - **Chiều cao mỗi tầng** và **Tầng 1 ở độ cao so với điểm cất cánh**: để tính ảnh thuộc tầng
     nào;
   - **Ghi toạ độ GPS vào từng ảnh (EXIF)**: nên bật.
4. Bấm **Đặt tên và gắn toạ độ**.

![Ảnh kiểm tra công trình](images/09-anh-kiem-tra-cong-trinh.png)

**Kết quả:**

- Phần mềm tạo thư mục `FSEMI_<tên công trình>` bên cạnh thư mục ảnh.
- Mỗi ảnh được đặt tên theo tầng và hướng, ví dụ `ToaA_F03_NE_0012.jpg` là tầng 3, hướng Đông
  Bắc, ảnh số 12. Ảnh chụp từ trên đỉnh có hướng `TOP`.
- Toạ độ GPS được ghi vào từng ảnh. Ảnh gốc không bị thay đổi.
- Kèm theo các danh sách:
  - `photos.csv`: dùng cho Metashape, Pix4D, RealityScan;
  - `geo.txt`: dùng cho WebODM / OpenDroneMap;
  - `photos.kml`: mở bằng Google Earth, xem vị trí ảnh theo tầng.

Phần mềm ghép ảnh với vị trí theo thứ tự nếu số ảnh bằng số lần chụp, nếu không thì theo thời
gian chụp. Ảnh không ghép được với vị trí nào được chép vào thư mục con `unplaced`.

---

## 7. Dựng mô hình 3D

Đưa thư mục `FSEMI_<tên>` (ảnh đã có toạ độ) vào một phần mềm dựng 3D:

| Phần mềm | Ghi chú |
|---|---|
| **RealityScan** (Epic Games) | Miễn phí cho doanh nghiệp doanh thu dưới 1 triệu USD/năm, nhanh, đẹp. |
| **Agisoft Metashape** | Bản Standard khoảng 179 USD, bản Professional khoảng 3.499 USD (có toạ độ, đo đạc). |
| **WebODM / OpenDroneMap** | Mã nguồn mở, miễn phí, chạy trên máy chủ riêng. Dùng `geo.txt`. |
| **Pix4D** | Chuyên nghiệp, chi phí cao. |

**Mẹo để mô hình đẹp:**

- Độ chồng ảnh 80% giữa các ảnh và 70% giữa các tầng hoặc tuyến.
- Giữ khoảng cách đều tới bề mặt.
- Với địa hình dốc, bật lượt chụp nghiêng.
- Với công trình, chụp thêm một vòng từ trên xuống.
- Chụp lúc trời nhiều mây mỏng, ít bóng gắt.

---

## 8. Mô phỏng (SITL): thử khi chưa có drone

Tab **MÔ PHỎNG** chạy một máy bay ArduPilot giả lập (SITL) ngay trên máy tính. Với nó, bạn thử được
toàn bộ các chức năng ở phần 2–6 mà không cần drone thật.

### 8.1. Khởi động máy bay giả lập

1. Bấm **MÔ PHỎNG**.
2. **Bấm lên bản đồ** của tab này để chọn chỗ cất cánh. Ví dụ chọn Hà Nội, cạnh một toà nhà, hoặc
   vùng đồi núi như Ba Vì, Tam Đảo để thấy rõ chức năng bay theo địa hình. Ô **Hướng** là hướng
   mũi máy bay lúc đầu.
3. Để nguyên ô **Thông số thử FSEMI (giao hàng, camera, gimbal)** (bật sẵn). Phần mềm sẽ tự đặt
   cho máy bay giả lập:
   - **gripper giả lập** ở servo 9, để thử giao hàng;
   - **camera** ở servo 10, gửi vị trí từng ảnh chụp;
   - **gimbal** ở servo 11, để thấy lệnh xoay camera;
   - **TERRAIN_ENABLE = 1**, để bay theo địa hình.
4. **Lần đầu dùng** (hoặc khi muốn làm lại từ đầu), đánh dấu thêm **Xoá sạch**, để máy bay giả
   lập bỏ hết các thông số cũ đã lưu và nhận bộ thông số mới.
5. Bấm biểu tượng **Đa cánh quạt**.
   - Lần đầu, phần mềm tải chương trình mô phỏng từ `firmware.ardupilot.org`, nên **cần Internet**.
   - Chạy xong, phần mềm tự kết nối và chuyển sang màn **DỮ LIỆU**.
6. Chờ HUD báo **Ready to Arm** và GPS **3D Fix**, mất khoảng 30–60 giây.

![Tab Mô phỏng](images/12-mo-phong.png)

Bộ thông số nằm trong file `fsemi-sitl.parm`, cạnh `FSEMIMissionPlanner.exe` (bản gốc ở
`docs/fsemi-sitl.parm`). Nếu muốn tự nạp cho một máy bay giả lập đã chạy:

1. **CẤU HÌNH → Tham số đầy đủ → Nạp từ file**, chọn `fsemi-sitl.parm`.
2. Bấm **Ghi tham số**.
3. Khởi động lại máy bay giả lập.

> Chỉ dùng file này cho **mô phỏng**. Các thông số `SIM_...` không có trên máy bay thật, còn các
> servo 9, 10, 11 phải đặt theo cách bạn đấu dây thật.

### 8.2. Cho máy bay giả lập bay lên và chạy nhiệm vụ

Khi máy bay còn đứng dưới đất, độ cao là **0 m**, kể cả khi bấm THẢ trong tab Giao hàng. Muốn
thấy máy bay bay thì phải **arm** rồi cho **cất cánh**.

**Chuẩn bị:** chờ HUD báo **Ready to Arm** và **GPS: 3D Fix**, khoảng 30–60 giây sau khi bấm
**Đa cánh quạt**. Nếu HUD báo *Not Ready to Arm*, xem lý do ở tab **Tin nhắn** dưới HUD.

**Cất cánh** (làm liền các bước, vì máy bay đã arm mà không cất cánh trong khoảng 10 giây sẽ tự
disarm):

1. **DỮ LIỆU → tab Thao tác**: trong ô chọn chế độ (cạnh nút **Đặt chế độ**), chọn **GUIDED**, rồi
   bấm **Đặt chế độ**.
2. Bấm **Arm/ Disarm**. HUD chuyển từ *DISARMED* sang **ARMED**.
3. **Chuột phải lên bản đồ** màn DỮ LIỆU → **Cất cánh**, nhập độ cao (ví dụ 20), rồi OK. Độ cao
   trên HUD và ô **Altitude** ở tab **Nhanh** tăng dần.
4. Muốn cho máy bay bay tới chỗ khác: chuột phải lên bản đồ, ở điểm cách máy bay khoảng 100–200 m
   cho dễ thấy → **Bay tới đây** hoặc **Bay tới đây (nhập độ cao)**.
   - Lần đầu, phần mềm hỏi độ cao bằng hộp thoại tiếng Anh **"Enter Guided Mode Alt"** (ô
     *Relative*, số 10): nhập độ cao, ví dụ 20, rồi bấm **OK**. Bấm Cancel hoặc để hộp thoại bị
     che thì máy bay đứng yên. Từ lần sau phần mềm dùng lại độ cao này.
   - Lệnh chỉ có tác dụng khi HUD báo **ARMED** và máy bay đang bay. Nếu máy bay đã tự disarm, cho
     cất cánh lại.

**Chạy nhiệm vụ** (thả hàng, chụp ảnh công trình, chụp ảnh địa hình):

1. **KẾ HOẠCH**: lập nhiệm vụ, lệnh đầu tiên là **Cất cánh** (chuột phải → **Cất cánh**, khoảng 20–30
   m), rồi thêm phần thả hàng hoặc chụp ảnh. Bấm **Ghi** để nạp vào máy bay.
2. **DỮ LIỆU**: cho máy bay cất cánh như các bước 1–3 ở trên.
3. Khi máy bay đã ở trên cao, bấm nút **Auto** trong tab **Thao tác**. Máy bay tự bay theo nhiệm vụ.
4. Theo dõi máy bay trên bản đồ (đánh dấu **Tự cuộn bản đồ** để bản đồ chạy theo máy bay) và trên
   HUD. Muốn dừng, bấm **RTL** để máy bay quay về và tự hạ cánh.

> Không chọn chế độ **Auto** rồi mới arm: ArduCopter mặc định không cho arm ở chế độ Auto (tham số
> `AUTO_OPTIONS`). Lệnh bắt đầu nhiệm vụ cũng có trong ô danh sách hành động của tab Thao tác
> (**Mission_Start**, bấm **Thực hiện**), nhưng cách trên đơn giản hơn.

### 8.3. Thử từng chức năng

| Chức năng | Cách thử trên máy bay giả lập | Kết quả mong đợi |
|---|---|---|
| Khoá / thả hàng | Cho máy bay cất cánh (phần 8.2), rồi **DỮ LIỆU → Giao hàng**: bấm **KHÓA**, rồi **THẢ** và xác nhận. | Trạng thái chuyển **ĐÃ KHÓA** rồi **ĐÃ THẢ**; danh sách có một lần thả, bản đồ có điểm cam. |
| Thả hàng theo nhiệm vụ | Cất cánh, rồi **Thả hàng tại đây** (phần 2.2), ghi và chạy. | Máy bay tới điểm, hạ xuống, thả; tab Giao hàng ghi một lần thả "(mission/RC)". |
| Chụp ảnh công trình | Cất cánh, rồi **Bay vòng quanh công trình** với ít tầng (ví dụ 20 m xuống 10 m, cách 5 m). | Máy bay bay vòng, chụp ở mỗi điểm; hạ cánh xong có file `photos\flight_..._photos.csv`. |
| Chụp ảnh địa hình | Vẽ vùng nhỏ (khoảng 200 × 200 m), cất cánh, rồi **Chụp ảnh địa hình vùng đa giác (3D)** với **Dữ liệu địa hình của máy bay**. | Máy bay bay đi bay về theo tuyến; độ cao so với mặt đất giữ đều. Cần Internet để phần mềm gửi dữ liệu địa hình. |
| Chia chuyến theo pin | Đặt **Thời gian bay một pin** nhỏ (ví dụ 5 phút). Bay xong chuyến 1 thì chọn **Các chuyến kiểm tra → Chuyến 2**, ghi và chạy lại. | Mỗi chuyến kết thúc bằng RTL; các file `flight_<n>_of_<tổng>.waypoints` được lưu. |
| Xử lý ảnh | Chép vào một thư mục **đúng số ảnh JPEG** bằng số lần chụp của chuyến (ảnh bất kỳ, ví dụ ảnh điện thoại), rồi làm theo phần 6. | Ảnh được đổi tên theo tầng và hướng, có toạ độ GPS, kèm `photos.csv`, `geo.txt`, `photos.kml`. |

Máy bay giả lập chỉ giả lập **lệnh chụp và vị trí ảnh**, không tạo ra file ảnh thật. Vì vậy phải
dùng ảnh bất kỳ với đúng số lượng: khi số ảnh bằng số lần chụp, phần mềm ghép theo thứ tự.

### 8.4. Giới hạn của mô phỏng

- Không thử được camera và móc hàng thật, gió, nhiễu GPS, sai số của dữ liệu địa hình.
- Lần bay thật đầu tiên nên chọn chỗ trống, bay thấp, thử từng chức năng một.

---

## 9. Câu hỏi thường gặp

**Bấm "Thả hàng tại đây" hoặc "Kiểm tra công trình" nhưng phần mềm báo phải chuyển sang
mission?** Ô loại nhiệm vụ (góc trên bản đồ Kế hoạch) đang là Fence hoặc Rally. Chọn **MISSION**.

**Hộp thoại chụp địa hình báo phải vẽ vùng trước?** Vẽ đa giác như ở [phần 4.1](#41-vẽ-vùng-cần-chụp),
ít nhất 3 điểm.

**Tóm tắt báo "Chưa có dữ liệu độ cao cho vùng này"?** Phần mềm chưa tải được dữ liệu SRTM, ví
dụ vì không có mạng. Có hai cách:

- chọn **Dữ liệu địa hình của máy bay**, khi máy bay có dữ liệu trên thẻ nhớ;
- kết nối Internet, mở lại hộp thoại để tải dữ liệu.

**Danh sách "Chuyến bay" trong cửa sổ ảnh trống?** Chỉ những chuyến có chụp ảnh (máy bay gửi
CAMERA_FEEDBACK) và phần mềm kết nối suốt chuyến mới có file vị trí. Kiểm tra cấu hình camera
(`CAMn_TYPE`, chân kích chụp) trên máy bay.

**Trong mô phỏng, độ cao luôn là 0 m?** Máy bay đang đứng dưới đất. Làm theo
[phần 8.2](#82-cho-máy-bay-giả-lập-bay-lên-và-chạy-nhiệm-vụ): chọn GUIDED, arm, rồi chuột phải bản
đồ → **Cất cánh**.

**Máy bay giả lập không nhận thông số thử FSEMI (tab Giao hàng báo "Servo 9" thay vì "Gripper ở
servo 9")?** Máy bay giả lập đang giữ thông số cũ. Ở tab **MÔ PHỎNG**, đánh dấu **Xoá sạch** và
**Thông số thử FSEMI**, rồi chạy lại.

**Muốn quay lại màu xanh lá của Mission Planner?** CẤU HÌNH → Planner → Giao diện →
`BurntKermit.mpsystheme`.
