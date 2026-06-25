#!/bin/bash
# Quick start - Chạy script auto-git-commit tùy theo OS

OS_TYPE=$(uname -s)

case "$OS_TYPE" in
	MINGW*|MSYS*|CYGWIN*)
		# Windows
		echo "🪟 Windows detected - Chạy PowerShell script..."
		powershell -NoProfile -ExecutionPolicy Bypass -File "auto-git-commit.ps1" -Push "$@"
		;;
	Darwin)
		# macOS
		echo "🍎 macOS detected - Chạy bash script..."
		chmod +x auto-git-commit.sh
		./auto-git-commit.sh -p "$@"
		;;
	Linux)
		# Linux
		echo "🐧 Linux detected - Chạy bash script..."
		chmod +x auto-git-commit.sh
		./auto-git-commit.sh -p "$@"
		;;
	*)
		echo "❌ OS không được hỗ trợ: $OS_TYPE"
		exit 1
		;;
esac
