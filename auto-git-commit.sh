#!/bin/bash

# ==========================================
# Auto Git Commit & Merge Script (Linux/Mac)
# ==========================================

# Màu sắc
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Hàm hiển thị màu
log_success() {
	echo -e "${GREEN}✅ $1${NC}"
}

log_error() {
	echo -e "${RED}❌ $1${NC}"
}

log_info() {
	echo -e "${CYAN}ℹ️ $1${NC}"
}

log_warning() {
	echo -e "${YELLOW}⚠️ $1${NC}"
}

# Kiểm tra thay đổi
check_changes() {
	git diff-index --quiet HEAD --
	if [ $? -eq 1 ]; then
		return 0  # Có thay đổi
	else
		return 1  # Không có thay đổi
	fi
}

# Commit thay đổi
perform_commit() {
	local message="$1"

	log_info "Staging các file thay đổi..."
	git add -A

	log_info "Tạo commit: $message"
	if git commit -m "$message"; then
		log_success "Commit thành công!"
		return 0
	else
		log_error "Lỗi khi commit"
		return 1
	fi
}

# Merge branches
perform_merge() {
	local source="$1"
	local target="$2"

	log_info "Chuyển đến branch $target..."
	git checkout "$target"

	log_info "Merge từ $source vào $target..."
	if git merge "$source"; then
		log_success "Merge thành công từ $source vào $target!"
		return 0
	else
		log_error "Lỗi khi merge"
		log_warning "Có thể có conflict! Hãy giải quyết thủ công."
		return 1
	fi
}

# Push code
perform_push() {
	local current_branch=$(git branch --show-current)

	log_info "Push branch $current_branch lên remote..."
	if git push origin "$current_branch"; then
		log_success "Push thành công!"
		return 0
	else
		log_error "Lỗi khi push"
		return 1
	fi
}

# Hiển thị status
show_status() {
	log_info "Trạng thái Git hiện tại:"
	echo ""
	git status
	echo ""
}

# Chạy một lần
execute_once() {
	log_info "🚀 Khởi động Auto Git Commit & Merge..."
	log_info "Branch hiện tại: $(git branch --show-current)"
	echo ""

	show_status

	if check_changes; then
		log_success "✨ Phát hiện thay đổi code!"

		if perform_commit "$COMMIT_MSG"; then
			if [ "$SOURCE_BRANCH" != "$TARGET_BRANCH" ]; then
				echo ""
				if perform_merge "$SOURCE_BRANCH" "$TARGET_BRANCH"; then
					if [ "$DO_PUSH" = true ]; then
						echo ""
						perform_push
					fi
				fi
			elif [ "$DO_PUSH" = true ]; then
				echo ""
				perform_push
			fi
		fi
	else
		log_warning "Không có thay đổi nào để commit."
	fi

	echo ""
	log_success "✅ Hoàn tất!"
}

# Watch mode
execute_watch() {
	log_info "👁️ Chế độ giám sát được kích hoạt (kiểm tra mỗi $WATCH_INTERVAL giây)"
	log_info "Nhấn Ctrl+C để dừng..."
	echo ""

	while true; do
		if check_changes; then
			local timestamp=$(date +"%H:%M:%S")
			log_success "[$timestamp] 🔔 Phát hiện thay đổi! Đang commit..."
			execute_once
		fi

		sleep "$WATCH_INTERVAL"
	done
}

# Mặc định
COMMIT_MSG="Auto-commit: Code update"
SOURCE_BRANCH="master"
TARGET_BRANCH="main"
DO_PUSH=false
WATCH_MODE=false
WATCH_INTERVAL=5

# Parse arguments
while [[ $# -gt 0 ]]; do
	case $1 in
		--message|-m)
			COMMIT_MSG="$2"
			shift 2
			;;
		--source|-s)
			SOURCE_BRANCH="$2"
			shift 2
			;;
		--target|-t)
			TARGET_BRANCH="$2"
			shift 2
			;;
		--push|-p)
			DO_PUSH=true
			shift
			;;
		--watch|-w)
			WATCH_MODE=true
			shift
			;;
		--interval|-i)
			WATCH_INTERVAL="$2"
			shift 2
			;;
		*)
			log_error "Option không hợp lệ: $1"
			exit 1
			;;
	esac
done

# Chạy chương trình chính
if [ "$WATCH_MODE" = true ]; then
	execute_watch
else
	execute_once
fi
