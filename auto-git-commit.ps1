# ==========================================
# Auto Git Commit & Merge Script
# Tự động commit và merge khi có thay đổi code
# ==========================================

param(
	[string]$CommitMessage = "Auto-commit: Code update",
	[string]$SourceBranch = "master",
	[string]$TargetBranch = "main",
	[switch]$Push = $false,
	[switch]$Watch = $false,
	[int]$WatchInterval = 5  # Kiểm tra mỗi 5 giây
)

# Màu sắc cho output
function Write-Success {
	Write-Host "$args" -ForegroundColor Green
}

function Write-Error-Custom {
	Write-Host "$args" -ForegroundColor Red
}

function Write-Info {
	Write-Host "$args" -ForegroundColor Cyan
}

function Write-Warning-Custom {
	Write-Host "$args" -ForegroundColor Yellow
}

# Kiểm tra xem có thay đổi chưa
function Check-Changes {
	$status = git status --porcelain
	return $status.Length -gt 0
}

# Commit thay đổi
function Perform-Commit {
	param(
		[string]$Message
	)

	try {
		Write-Info "📝 Staging các file thay đổi..."
		git add -A

		Write-Info "💾 Tạo commit: $Message"
		git commit -m $Message

		Write-Success "✅ Commit thành công!"
		return $true
	}
	catch {
		Write-Error-Custom "❌ Lỗi khi commit: $_"
		return $false
	}
}

# Merge branches
function Perform-Merge {
	param(
		[string]$Source,
		[string]$Target
	)

	try {
		Write-Info "🔀 Chuyển đến branch $Target..."
		git checkout $Target

		Write-Info "🔀 Merge từ $Source vào $Target..."
		git merge $Source

		Write-Success "✅ Merge thành công từ $Source vào $Target!"
		return $true
	}
	catch {
		Write-Error-Custom "❌ Lỗi khi merge: $_"
		Write-Warning-Custom "⚠️ Có thể có conflict! Hãy giải quyết thủ công."
		return $false
	}
}

# Push code lên remote
function Perform-Push {
	try {
		$currentBranch = git branch --show-current
		Write-Info "📤 Push branch $currentBranch lên remote..."
		git push origin $currentBranch

		Write-Success "✅ Push thành công!"
		return $true
	}
	catch {
		Write-Error-Custom "❌ Lỗi khi push: $_"
		return $false
	}
}

# Hiển thị status hiện tại
function Show-Status {
	Write-Info "📊 Trạng thái Git hiện tại:"
	Write-Host ""
	git status
	Write-Host ""
}

# Main logic - một lần chạy
function Execute-Once {
	Write-Info "🚀 Khởi động Auto Git Commit & Merge..."
	Write-Info "Branch hiện tại: $(git branch --show-current)"
	Write-Info ""

	Show-Status

	if (Check-Changes) {
		Write-Success "✨ Phát hiện thay đổi code!"

		# Commit
		if (Perform-Commit -Message $CommitMessage) {
			# Merge nếu có target branch khác
			if ($SourceBranch -ne $TargetBranch) {
				Write-Info ""
				if (Perform-Merge -Source $SourceBranch -Target $TargetBranch) {
					# Push nếu có flag
					if ($Push) {
						Write-Info ""
						Perform-Push
					}
				}
			}
			elseif ($Push) {
				Write-Info ""
				Perform-Push
			}
		}
	}
	else {
		Write-Warning-Custom "⚠️ Không có thay đổi nào để commit."
	}

	Write-Info ""
	Write-Info "✅ Hoàn tất!"
}

# Watch mode - giám sát liên tục
function Execute-Watch {
	Write-Info "👁️ Chế độ giám sát được kích hoạt (kiểm tra mỗi $WatchInterval giây)"
	Write-Info "Nhấn Ctrl+C để dừng..."
	Write-Info ""

	$lastCheck = Get-Date

	while ($true) {
		try {
			if (Check-Changes) {
				$timestamp = Get-Date -Format "HH:mm:ss"
				Write-Success "[$timestamp] 🔔 Phát hiện thay đổi! Đang commit..."
				Execute-Once
			}

			Start-Sleep -Seconds $WatchInterval
		}
		catch {
			Write-Error-Custom "❌ Lỗi trong watch mode: $_"
			Start-Sleep -Seconds $WatchInterval
		}
	}
}

# Main entry point
if ($Watch) {
	Execute-Watch
}
else {
	Execute-Once
}
