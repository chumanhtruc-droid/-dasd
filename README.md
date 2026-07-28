# Hệ Thống Nhắn Tin Thời Gian Thực & Chụp Màn Hình (RTM System)

Dự án này bao gồm 3 thành phần chính:
1. **Backend**: Node.js, Express, Socket.IO, Prisma, Cloudinary (Lưu ảnh), PostgreSQL
2. **Frontend (Web Dashboard)**: Next.js, TailwindCSS
3. **Windows Client**: C# WPF (.NET 8)

---

## Hướng dẫn Triển khai "Combo Quốc Dân" hoàn toàn miễn phí (Cloudinary + Neon + Render + Vercel)

Đây là hướng dẫn giúp bạn đẩy toàn bộ dự án lên Internet 24/7 mà không tốn 1 đồng chi phí nào.

### Bước 1: Lấy URL Database (Neon.tech)
1. Truy cập [Neon.tech](https://neon.tech/) và đăng nhập bằng Google/GitHub.
2. Tạo Project mới. Neon sẽ cung cấp cho bạn một chuỗi kết nối **Connection String** dạng:
   `postgresql://admin:password@ep-lucky-xxxxx.ap-southeast-1.aws.neon.tech/neondb?sslmode=require`
3. Lưu chuỗi này lại, đây chính là biến `DATABASE_URL`.

### Bước 2: Lấy API Key Lưu ảnh (Cloudinary)
1. Truy cập [Cloudinary.com](https://cloudinary.com/) và đăng ký tài khoản.
2. Ở trang Dashboard, bạn sẽ thấy 3 thông tin quan trọng ở mục **Product Environment Credentials**:
   - `Cloud Name` (vd: dzqbzq)
   - `API Key`
   - `API Secret`
3. Lưu lại 3 thông tin này để dùng cho Backend.

### Bước 3: Đưa Code lên GitHub
1. Mở tài khoản GitHub của bạn và tạo 1 kho lưu trữ (Repository) mới tên là `rtm-system` (để Private hoặc Public tùy ý).
2. Mở Terminal (CMD) tại thư mục `dự pohongf` và gõ:
   ```bash
   git init
   git add .
   git commit -m "Init project"
   git branch -M main
   git remote add origin https://github.com/Taikhoancuaban/rtm-system.git
   git push -u origin main
   ```

### Bước 4: Triển khai Backend lên Render.com
1. Truy cập [Render.com](https://render.com/), đăng nhập bằng GitHub.
2. Bấm **New+** > **Web Service**.
3. Chọn Repository `rtm-system` mà bạn vừa tạo trên GitHub.
4. Cấu hình như sau:
   - **Name:** `rtm-backend`
   - **Root Directory:** `backend`
   - **Build Command:** `npm install && npx prisma generate && npx prisma db push && npm run build`
   - **Start Command:** `npm start`
5. Cuộn xuống mục **Environment Variables** (Biến môi trường) và thêm các biến sau:
   - `DATABASE_URL` = (Chuỗi lấy từ Neon.tech ở Bước 1)
   - `JWT_SECRET` = `mot_chuoi_bi_mat_bat_ky_cua_ban`
   - `CLOUDINARY_CLOUD_NAME` = (Lấy ở Bước 2)
   - `CLOUDINARY_API_KEY` = (Lấy ở Bước 2)
   - `CLOUDINARY_API_SECRET` = (Lấy ở Bước 2)
6. Bấm **Create Web Service**. Chờ khoảng 2-3 phút, Render sẽ cung cấp cho bạn URL của backend, dạng `https://rtm-backend-xxxx.onrender.com`.

### Bước 5: Triển khai Frontend lên Vercel.com
1. Truy cập [Vercel.com](https://vercel.com/), đăng nhập bằng GitHub.
2. Bấm **Add New** > **Project** và import repository `rtm-system`.
3. Trong mục cấu hình, mở rộng phần **Root Directory** và chọn `frontend`.
4. (Tùy chọn) Thêm biến môi trường nếu cần thiết (Trong code Next.js hiện tại, URL đang được fix cứng là localhost:5000. Bạn cần sửa lại các dòng URL trong file `src/app/page.tsx`, `chat/page.tsx`, và `admin/page.tsx` thành URL của Render ở Bước 4 trước khi push lên GitHub).
5. Bấm **Deploy**. Vercel sẽ tự động build và cung cấp cho bạn URL truy cập.

### Bước 6: Cập nhật URL cho Windows Client
1. Mở file `windows-client/MainWindow.xaml.cs`.
2. Thay thế `http://localhost:5000` thành URL Backend của Render (ví dụ: `https://rtm-backend.onrender.com`).
3. Build file .exe bằng lệnh sau (yêu cầu cài .NET 8 SDK):
   ```bash
   cd windows-client
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
   ```
4. File `.exe` siêu nhẹ sẽ nằm ở: `bin\Release\net8.0-windows\win-x64\publish\WindowsClient.exe`. Bạn có thể gửi file này cho bất kỳ ai chạy trên Windows.

---

> 🎉 **Hoàn tất!** Giờ đây bạn đã có một hệ thống RTM Server hoàn toàn miễn phí, tự động cập nhật mỗi khi bạn push code lên GitHub và chạy ổn định 24/7!
