# Copilot Instructions

## Project Guidelines
- User prefers responses in Vietnamese (tiếng Việt) for all software development tasks and assistance

## Auto Git Tools
Dự án này bao gồm các script CLI tự động commit và merge:

### Windows
- `auto-git-commit.bat` - Menu interactif cho Windows
- `auto-git-commit.ps1` - PowerShell script

### Linux/Mac
- `auto-git-commit.sh` - Bash script

### Cách sử dụng
```bash
# Commit & Push (một lần)
./auto-git-commit.sh -m "Fix bug" -p

# Watch Mode (giám sát liên tục)
./auto-git-commit.sh -w -i 5 -p

# Commit & Merge & Push
./auto-git-commit.sh -m "Auto-update" -s master -t main -p
```

Xem chi tiết tại `AUTO_GIT_GUIDE.md`