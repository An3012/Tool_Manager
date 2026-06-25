# 🚀 Auto Git Commit & Merge CLI Tool

## 📋 Giới Thiệu

Công cụ tự động commit và merge code mỗi khi có thay đổi. Hỗ trợ:
- ✅ **Commit tự động** khi phát hiện thay đổi
- ✅ **Merge tự động** giữa các branch
- ✅ **Push tự động** lên remote
- ✅ **Watch mode** - giám sát liên tục
- ✅ **Cross-platform** - Windows, Linux, Mac

---

## 🔧 Cài Đặt

### Windows
```bash
# Chạy batch file
auto-git-commit.bat

# Hoặc PowerShell trực tiếp
powershell -NoProfile -ExecutionPolicy Bypass -File auto-git-commit.ps1
```

### Linux / Mac
```bash
# Cấp quyền thực thi
chmod +x auto-git-commit.sh

# Chạy script
./auto-git-commit.sh
```

---

## 📖 Hướng Dẫn Sử Dụng

### 1️⃣ **Commit & Push (Một Lần)**

**Windows (Batch):**
```bash
auto-git-commit.bat
# Chọn option 1
```

**Windows (PowerShell):**
```powershell
.\auto-git-commit.ps1 -CommitMessage "Fix bug" -Push
```

**Linux/Mac:**
```bash
./auto-git-commit.sh -m "Fix bug" -p
```

---

### 2️⃣ **Commit & Merge & Push**

**Windows (Batch):**
```bash
auto-git-commit.bat
# Chọn option 2
# Nhập source branch: master
# Nhập target branch: main
# Nhập commit message: Fix critical bug
```

**Windows (PowerShell):**
```powershell
.\auto-git-commit.ps1 `
  -CommitMessage "Fix critical bug" `
  -SourceBranch "master" `
  -TargetBranch "main" `
  -Push
```

**Linux/Mac:**
```bash
./auto-git-commit.sh -m "Fix critical bug" -s master -t main -p
```

---

### 3️⃣ **Watch Mode - Giám Sát Liên Tục**

Tự động commit mỗi 5 giây nếu có thay đổi:

**Windows (Batch):**
```bash
auto-git-commit.bat
# Chọn option 3
# Nhập khoảng thời gian kiểm tra (mặc định: 5)
```

**Windows (PowerShell):**
```powershell
.\auto-git-commit.ps1 -Watch -WatchInterval 5 -Push
```

**Linux/Mac:**
```bash
./auto-git-commit.sh -w -i 5 -p
```

**Nhấn Ctrl+C để dừng watch mode**

---

### 4️⃣ **Watch Mode Với Auto-Merge**

Giám sát + commit + merge + push tự động:

**Windows (PowerShell):**
```powershell
.\auto-git-commit.ps1 `
  -CommitMessage "Auto-update" `
  -SourceBranch "master" `
  -TargetBranch "main" `
  -Watch -WatchInterval 5 `
  -Push
```

**Linux/Mac:**
```bash
./auto-git-commit.sh -m "Auto-update" -s master -t main -w -i 5 -p
```

---

## 📝 Tham Số (Parameters)

### PowerShell (.ps1)
```powershell
-CommitMessage "message"      # Tin nhắn commit (mặc định: Auto-commit: Code update)
-SourceBranch "branch"        # Source branch (mặc định: master)
-TargetBranch "branch"        # Target branch (mặc định: main)
-Push                         # Flag để push lên remote
-Watch                        # Bật chế độ giám sát
-WatchInterval 5              # Khoảng thời gian kiểm tra (giây)
```

### Bash (.sh)
```bash
-m, --message "message"       # Tin nhắn commit
-s, --source "branch"         # Source branch
-t, --target "branch"         # Target branch
-p, --push                    # Push lên remote
-w, --watch                   # Bật chế độ giám sát
-i, --interval 5              # Khoảng thời gian kiểm tra
```

---

## 🎯 Ví Dụ Thực Tế

### Ví Dụ 1: Sửa lỗi và commit
**Windows:**
```powershell
.\auto-git-commit.ps1 -CommitMessage "Fix navbar bug" -Push
```

**Linux/Mac:**
```bash
./auto-git-commit.sh -m "Fix navbar bug" -p
```

### Ví Dụ 2: Phát triển feature trên dev, merge vào main
**Windows:**
```powershell
.\auto-git-commit.ps1 `
  -CommitMessage "Add new dashboard" `
  -SourceBranch "dev" `
  -TargetBranch "main" `
  -Push
```

### Ví Dụ 3: Giám sát liên tục khi làm việc
**Windows (PowerShell):**
```powershell
.\auto-git-commit.ps1 -Watch -WatchInterval 10 -Push
```

**Linux/Mac:**
```bash
./auto-git-commit.sh -w -i 10 -p
```

---

## ⚠️ Lưu Ý Quan Trọng

1. **Commit Message**: Luôn viết commit message rõ ràng và chi tiết
2. **Merge Conflicts**: Nếu có conflict, script sẽ dừng và yêu cầu xử lý thủ công
3. **Watch Mode**: Kiểm tra mỗi N giây - điều chỉnh interval tùy theo nhu cầu
4. **Push**: Luôn sử dụng flag `-Push` nếu muốn tự động push lên remote
5. **Branches**: Đảm bảo branches đã tồn tại trước khi chạy merge

---

## 🐛 Xử Lý Sự Cố

### Lỗi: "Command not found" (Linux/Mac)
```bash
# Giải pháp: Cấp quyền thực thi
chmod +x auto-git-commit.sh
```

### Lỗi: PowerShell execution policy (Windows)
```powershell
# Giải pháp: Chạy với bypass
powershell -ExecutionPolicy Bypass -File auto-git-commit.ps1
```

### Lỗi: Merge conflict
```bash
# Script sẽ dừng lại
# 1. Mở file có conflict
# 2. Giải quyết conflict
# 3. Commit thủ công
# 4. Chạy script lại
```

---

## 📞 Hỗ Trợ

Nếu gặp vấn đề, hãy:
1. Kiểm tra git status: `git status`
2. Xem git log: `git log --oneline -5`
3. Kiểm tra branches: `git branch -a`

---

## 📜 License

MIT License - Tự do sử dụng và sửa đổi

---

**Made with ❤️ for developers**
