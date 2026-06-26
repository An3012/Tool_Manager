import sys
import os
import time
import json
from pathlib import Path
from playwright.sync_api import sync_playwright

BASE_DIR = Path(__file__).resolve().parent.parent

def log(message):
    print(message, flush=True)

def find_first_input(page, selectors):
    for selector in selectors:
        try:
            element = page.query_selector(selector)
            if element:
                return element, selector
        except Exception:
            continue
    return None, None

def find_login_button(page):
    selectors = [
        "button[type='submit']",
        "button:has-text('Đăng nhập')",
        "button:has-text('ĐĂNG NHẬP')",
    ]
    for selector in selectors:
        try:
            btn = page.query_selector(selector)
            if btn and btn.is_visible():
                return btn, selector
        except Exception:
            continue
    return None, None

def wait_for_login(page):
    log("[INFO] Waiting for login success...")
    for i in range(120):  # 2 minutes max wait
        url = page.url.lower()
        if "dang-nhap" not in url and "login" not in url and ("portfolio" in url or "danh-muc" in url or "co-phieu" in url or "entradex" in url):
            if "dang-nhap" not in url:
                log(f"[INFO] Login detected! Current URL: {page.url}")
                return page
        time.sleep(1)
    return None

def main():
    if len(sys.argv) < 3:
        log("[ERROR] Missing arguments: username password")
        return

    username = sys.argv[1]
    password = sys.argv[2]
    log(f"[INFO] Using credentials for user: {username}")

    with sync_playwright() as p:
        browser = p.chromium.launch(headless=True)
        context = browser.new_context(viewport={"width": 1280, "height": 800})
        page = context.new_page()

        log("[INFO] Navigating to login page...")
        page.goto("https://entradex.dnse.com.vn/dang-nhap", wait_until="domcontentloaded", timeout=30000)

        # Autofill
        username_input, _ = find_first_input(page, [
            "input[type='text']",
            "input[placeholder*='Số điện thoại']",
            "input[placeholder*='Tên đăng nhập']",
            "input[placeholder*='Mã KH']"
        ])
        if username_input:
            username_input.click()
            username_input.fill(username)

        password_input = page.locator("input[type='password']").first
        password_input.fill(password)

        login_btn, _ = find_login_button(page)
        if login_btn:
            login_btn.click()

        active_page = wait_for_login(page)
        if not active_page:
            log("[ERROR] Login failed or timed out.")
            page.screenshot(path=str(BASE_DIR / "login_failed.png"))
            browser.close()
            return

        page = active_page
        time.sleep(5)

        log("[INFO] Navigating to realized PnL page...")
        page.goto("https://entradex.dnse.com.vn/bao-cao/lai-lo-thuc-hien", wait_until="domcontentloaded", timeout=30000)
        time.sleep(5)

        screenshot_path = str(BASE_DIR / "realized_pnl_page.png")
        page.screenshot(path=screenshot_path)
        log(f"[INFO] Screenshot saved to: {screenshot_path}")

        # Dump table headers and content
        table = page.query_selector("table")
        if table:
            log("[INFO] Table found!")
            headers = [h.inner_text().strip().replace('\n', ' ') for h in table.query_selector_all("thead th")]
            log(f"[INFO] Headers: {headers}")
            rows = table.query_selector_all("tbody tr")
            log(f"[INFO] Number of rows: {len(rows)}")
            for idx, r in enumerate(rows):
                cells = [c.inner_text().strip().replace('\n', ' ') for c in r.query_selector_all("td")]
                log(f"[INFO] Row {idx+1}: {cells}")
        else:
            log("[WARN] No table found. Printing page structure.")
            body_text = page.query_selector("body").inner_text()
            log(body_text[:1000])

        browser.close()

if __name__ == "__main__":
    main()
