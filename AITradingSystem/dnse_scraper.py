import sys
import json
import time
import re
from pathlib import Path
from playwright.sync_api import sync_playwright

BASE_DIR = Path(__file__).resolve().parent
OUTPUT_JSON = BASE_DIR / "dnse_deals.json"

# Ensure UTF-8 stdout for Windows console / C# redirection
try:
    sys.stdout.reconfigure(encoding="utf-8")
except AttributeError:
    pass


def log(message):
    print(message, flush=True)


def save_debug_screenshot(page, name="debug"):
    """Save a debug screenshot to help diagnose issues."""
    try:
        path = str(BASE_DIR / f"{name}.png")
        page.screenshot(path=path)
        log(f"[DEBUG] Screenshot saved: {path}")
    except Exception as e:
        log(f"[WARN] Failed to save screenshot {name}: {e}")


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

    # Fallback to check all buttons
    try:
        buttons = page.query_selector_all("button")
        for btn in buttons:
            text = (btn.inner_text() or "").strip().lower()
            if "đăng nhập" in text or "dang nhap" in text or "login" in text:
                return btn, f"button with text '{text}'"
    except Exception:
        pass

    return None, None


def parse_number(text):
    """Parse a number from Vietnamese-formatted text. Handles commas, dots, signs."""
    if not text:
        return 0
    text = text.strip()
    # Remove currency symbols and whitespace
    text = re.sub(r'[đĐ₫VND\s]', '', text, flags=re.IGNORECASE)
    is_negative = '-' in text
    text = text.replace('+', '').replace('-', '').replace(',', '').replace('.', '')
    try:
        val = int(text) if text else 0
        return -abs(val) if is_negative else val
    except ValueError:
        return 0


def parse_decimal(text):
    """Parse a decimal number from text (e.g., '13,554.70' or '13.554,70')."""
    if not text:
        return 0.0
    text = text.strip()
    text = re.sub(r'[đĐ₫VND\s]', '', text, flags=re.IGNORECASE)
    is_negative = '-' in text
    text = text.replace('+', '').replace('-', '')
    
    # Handle Vietnamese number format: 13.554,70 (dot as thousand sep, comma as decimal)
    if ',' in text and '.' in text:
        if text.rfind(',') > text.rfind('.'):
            # Format: 13.554,70 → comma is decimal separator
            text = text.replace('.', '').replace(',', '.')
        else:
            # Format: 13,554.70 → dot is decimal separator
            text = text.replace(',', '')
    elif ',' in text:
        # Could be thousand separator or decimal, check position
        parts = text.split(',')
        if len(parts) == 2 and len(parts[1]) <= 2:
            text = text.replace(',', '.')  # Decimal separator
        else:
            text = text.replace(',', '')   # Thousand separator
    
    try:
        val = float(text) if text else 0.0
        return -abs(val) if is_negative else val
    except ValueError:
        return 0.0


# =============================================================================
# PHASE 1: Portfolio / Assets Page Scraping (Primary Approach)
# =============================================================================

def navigate_to_portfolio(page):
    """Navigate to the portfolio/assets page to get current holdings."""
    log("[INFO] Attempting to navigate to portfolio/assets page...")
    
    # Strategy 1: Try direct URL navigation to common portfolio paths
    portfolio_urls = [
        "https://entradex.dnse.com.vn/tai-san",
        "https://entradex.dnse.com.vn/danh-muc",
        "https://entradex.dnse.com.vn/portfolio",
    ]
    
    for url in portfolio_urls:
        try:
            log(f"[INFO] Trying direct navigation to {url}...")
            page.goto(url, wait_until="domcontentloaded", timeout=15000)
            time.sleep(3)
            
            # Check if we landed on a valid portfolio page (not redirected to login)
            current_url = page.url.lower()
            if "dang-nhap" in current_url or "login" in current_url:
                log(f"[WARN] Redirected to login page from {url}")
                continue
            
            # Check if portfolio content is visible
            if has_portfolio_content(page):
                log(f"[INFO] Portfolio content found at {url}!")
                return True
                
        except Exception as ex:
            log(f"[WARN] Failed to navigate to {url}: {ex}")
    
    # Strategy 2: Click menu items from the main page
    log("[INFO] Trying menu navigation...")
    try:
        page.goto("https://entradex.dnse.com.vn/", wait_until="domcontentloaded", timeout=15000)
        time.sleep(3)
    except Exception:
        pass
    
    menu_texts = ["Tài sản", "Danh mục", "Portfolio", "Vị thế"]
    for menu_text in menu_texts:
        try:
            link = page.locator(f"a:has-text('{menu_text}'), button:has-text('{menu_text}'), [role='menuitem']:has-text('{menu_text}'), nav *:has-text('{menu_text}')").first
            if link.is_visible(timeout=2000):
                link.click()
                log(f"[INFO] Clicked menu item: '{menu_text}'")
                time.sleep(3)
                
                if has_portfolio_content(page):
                    log(f"[INFO] Portfolio content found after clicking '{menu_text}'!")
                    return True
        except Exception:
            continue
    
    # Strategy 3: Check if current page already shows portfolio data
    if has_portfolio_content(page):
        log("[INFO] Portfolio content found on current page!")
        return True
    
    log("[WARN] Could not navigate to portfolio page via any strategy.")
    return False


def has_portfolio_content(page):
    """Check if the current page has portfolio/held stocks content."""
    indicators = [
        "Cổ phiếu nắm giữ",
        "Danh mục đầu tư",
        "Cổ phiếu đang giữ",
        "KL khả dụng",
        "Giá vốn",
        "Giá TB",
        "Giá thị trường",
        "Tổng giá trị",
    ]
    try:
        page_text = page.inner_text("body") or ""
        for indicator in indicators:
            if indicator.lower() in page_text.lower():
                log(f"[DEBUG] Portfolio indicator found: '{indicator}'")
                return True
    except Exception:
        pass
    return False


def scrape_portfolio_holdings(page):
    """
    Scrape ACTUAL current holdings from the portfolio/assets page.
    This shows the real quantities after all buys and sells.
    """
    holdings = []
    
    log("[INFO] Scraping portfolio holdings...")
    save_debug_screenshot(page, "debug_portfolio")
    
    # Wait for dynamic content to load
    try:
        page.wait_for_load_state("networkidle", timeout=10000)
    except Exception:
        pass
    time.sleep(2)
    
    # --- Strategy 1: Table-based layout ---
    holdings = try_scrape_portfolio_table(page)
    if holdings:
        return holdings
    
    # --- Strategy 2: Card/List-based layout ---
    holdings = try_scrape_portfolio_cards(page)
    if holdings:
        return holdings
    
    # --- Strategy 3: Generic text extraction ---
    holdings = try_scrape_portfolio_generic(page)
    if holdings:
        return holdings
    
    log("[WARN] No portfolio holdings found with any scraping strategy.")
    return []


def try_scrape_portfolio_table(page):
    """Try to scrape holdings from a table layout."""
    holdings = []
    
    try:
        tables = page.query_selector_all("table")
        log(f"[DEBUG] Found {len(tables)} tables on portfolio page")
        
        for table_idx, table in enumerate(tables):
            rows = table.query_selector_all("tbody tr")
            if not rows:
                continue
            
            # Try to identify column headers
            headers = []
            try:
                header_cells = table.query_selector_all("thead th, thead td")
                headers = [(h.inner_text() or "").strip().lower() for h in header_cells]
                log(f"[DEBUG] Table {table_idx} headers: {headers}")
            except Exception:
                pass
            
            for row in rows:
                cells = row.query_selector_all("td")
                if len(cells) < 3:
                    continue
                
                row_text = (row.inner_text() or "").strip()
                if "Chưa có" in row_text or "No data" in row_text or not row_text:
                    continue
                
                holding = extract_holding_from_cells(cells, headers)
                if holding and holding.get("DealText"):
                    holdings.append(holding)
                    log(f"[INFO] Scraped holding: {holding['DealText']} qty={holding['QtyText']}")
            
            if holdings:
                log(f"[INFO] Found {len(holdings)} holdings from table {table_idx}")
                return holdings
                
    except Exception as ex:
        log(f"[WARN] Table scraping failed: {ex}")
    
    return holdings


def extract_holding_from_cells(cells, headers):
    """Extract holding data from table cells, using headers to identify columns."""
    holding = {
        "DealText": "",
        "QtyText": "0",
        "OpenTimeText": "",
        "StatusText": "OPEN",
        "PnlText": "0",
        "AvgPrice": "0",
        "MarketPrice": "0",
        "InvestedValue": "0",
    }
    
    cell_texts = [(c.inner_text() or "").strip() for c in cells]
    
    if headers:
        # Map columns by header keywords
        for i, header in enumerate(headers):
            if i >= len(cell_texts):
                break
            text = cell_texts[i]
            
            if any(kw in header for kw in ["mã", "symbol", "cp", "cổ phiếu", "mã ck"]):
                # Extract stock symbol (3-letter uppercase code)
                match = re.search(r'\b([A-Z]{2,5})\b', text.upper())
                if match:
                    holding["DealText"] = match.group(1)
                elif text.strip():
                    holding["DealText"] = text.split()[0].upper()
                    
            elif any(kw in header for kw in ["kl khả dụng", "sl nắm giữ", "số lượng", "kl", "qty", "khối lượng"]):
                holding["QtyText"] = str(parse_number(text))
                
            elif any(kw in header for kw in ["giá tb", "giá vốn", "giá mua tb", "avg", "entry"]):
                holding["AvgPrice"] = str(parse_decimal(text))
                
            elif any(kw in header for kw in ["giá tt", "giá thị trường", "giá hiện tại", "market", "current"]):
                holding["MarketPrice"] = str(parse_decimal(text))
                
            elif any(kw in header for kw in ["lãi/lỗ", "lãi lỗ", "pnl", "p&l", "profit", "l/l"]):
                val = parse_number(text)
                holding["PnlText"] = str(val)
                
            elif any(kw in header for kw in ["giá trị", "value", "tổng"]):
                holding["InvestedValue"] = str(parse_number(text))
    else:
        # No headers — use positional heuristics
        # Common layout: [Symbol] [Qty] [AvgPrice] [MarketPrice] [PnL] ...
        if len(cell_texts) >= 1:
            match = re.search(r'\b([A-Z]{2,5})\b', cell_texts[0].upper())
            if match:
                holding["DealText"] = match.group(1)
            else:
                holding["DealText"] = cell_texts[0].split()[0].upper() if cell_texts[0] else ""
        if len(cell_texts) >= 2:
            holding["QtyText"] = str(parse_number(cell_texts[1]))
        if len(cell_texts) >= 3:
            holding["AvgPrice"] = str(parse_decimal(cell_texts[2]))
        if len(cell_texts) >= 4:
            holding["MarketPrice"] = str(parse_decimal(cell_texts[3]))
        if len(cell_texts) >= 5:
            holding["PnlText"] = str(parse_number(cell_texts[4]))
    
    # Validate: must have symbol and positive quantity
    if not holding["DealText"] or not re.match(r'^[A-Z]{2,5}$', holding["DealText"]):
        return None
    
    qty = parse_number(holding["QtyText"])
    if qty <= 0:
        return None
    
    return holding


def try_scrape_portfolio_cards(page):
    """Try to scrape holdings from a card/list-based layout."""
    holdings = []
    
    try:
        # Look for card containers that might represent stock holdings
        card_selectors = [
            "div[class*='stock-item']",
            "div[class*='holding']",
            "div[class*='position']",
            "div[class*='MuiCard']",
            "div[class*='portfolio-item']",
            "li[class*='stock']",
        ]
        
        for selector in card_selectors:
            cards = page.query_selector_all(selector)
            if not cards:
                continue
            
            log(f"[DEBUG] Found {len(cards)} cards with selector: {selector}")
            
            for card in cards:
                text = (card.inner_text() or "").strip()
                if not text:
                    continue
                
                holding = extract_holding_from_text(text)
                if holding:
                    holdings.append(holding)
                    log(f"[INFO] Scraped card holding: {holding['DealText']} qty={holding['QtyText']}")
            
            if holdings:
                return holdings
                
    except Exception as ex:
        log(f"[WARN] Card scraping failed: {ex}")
    
    return holdings


def try_scrape_portfolio_generic(page):
    """Try generic text-based extraction from the page body."""
    holdings = []
    
    try:
        # Get full page text and look for stock symbol + quantity patterns
        body_text = page.inner_text("body") or ""
        
        # Look for patterns like: "POW ... 27 ... 13,500"
        # Vietnamese stock symbols are 2-5 uppercase letters
        stock_pattern = re.compile(
            r'\b([A-Z]{2,5})\b'
            r'[^\n]*?'
            r'(\d[\d.,]*)\s*(?:cp|cổ|cổ phiếu|KL)?',
            re.IGNORECASE
        )
        
        matches = stock_pattern.findall(body_text)
        seen = set()
        for symbol, qty_str in matches:
            symbol = symbol.upper()
            if symbol in seen:
                continue
            if symbol in ("OPEN", "CLOSED", "BUY", "SELL", "HOLD", "VND", "USD"):
                continue
            
            qty = parse_number(qty_str)
            if qty > 0:
                seen.add(symbol)
                holdings.append({
                    "DealText": symbol,
                    "QtyText": str(qty),
                    "OpenTimeText": "",
                    "StatusText": "OPEN",
                    "PnlText": "0",
                    "AvgPrice": "0",
                    "MarketPrice": "0",
                    "InvestedValue": "0",
                })
                log(f"[INFO] Generic extraction: {symbol} qty={qty}")
        
    except Exception as ex:
        log(f"[WARN] Generic extraction failed: {ex}")
    
    return holdings


# =============================================================================
# PHASE 2: Deal Report Aggregation (Fallback Approach)
# =============================================================================

def scrape_current_table(page):
    """Scrape rows from the current deal report table page."""
    try:
        try:
            page.wait_for_selector("table tbody tr", timeout=5000)
        except Exception:
            pass

        rows = page.query_selector_all("table tbody tr")
        log(f"[INFO] Scraped {len(rows)} rows from current table")
        deals = []

        for row in rows:
            cells = row.query_selector_all("td")
            if len(cells) < 5:
                continue

            row_text = (row.inner_text() or "").strip()
            if "Chưa có giao dịch" in row_text or "No data" in row_text:
                continue

            deal_text_raw = (cells[0].inner_text() or "").strip()
            qty_text_raw = (cells[1].inner_text() or "").strip()
            open_time_raw = (cells[2].inner_text() or "").strip()
            status_text_raw = (cells[3].inner_text() or "").strip()
            pnl_text_raw = (cells[4].inner_text() or "").strip()

            deal_text = deal_text_raw.split()[0].upper() if deal_text_raw else ""
            qty = parse_number(qty_text_raw)
            open_time_text = " ".join(open_time_raw.split())
            status_text = status_text_raw
            pnl_text = pnl_text_raw.split('\n')[0].strip() if pnl_text_raw else "0"

            if not deal_text or qty <= 0:
                continue

            # Detect BUY vs SELL from deal text or other columns
            order_type = "BUY"
            deal_lower = deal_text_raw.lower()
            if "bán" in deal_lower or "sell" in deal_lower or "short" in deal_lower:
                order_type = "SELL"
            # Also check status column for sell indicators
            status_lower = status_text_raw.lower()
            if "bán" in status_lower or "đóng" in status_lower:
                order_type = "SELL"

            deals.append({
                "Symbol": deal_text,
                "Qty": qty,
                "OrderType": order_type,
                "OpenTime": open_time_text,
                "Status": status_text,
                "PnlText": pnl_text,
            })
        return deals
    except Exception as ex:
        log(f"[WARN] Failed to scrape current table: {ex}")
        return []


def click_next_page(page):
    """Click the next page button in the pagination."""
    selectors = [
        "button[aria-label='Go to next page']",
        "button[aria-label='Next page']",
        "button[aria-label='Trang sau']",
        "button[aria-label='Trang tiếp theo']",
        "button.MuiTablePagination-actions button:last-child",
        "div.MuiTablePagination-actions button:nth-child(2)",
        "button:has-text('>')",
    ]
    for sel in selectors:
        try:
            btn = page.query_selector(sel)
            if btn and btn.is_visible() and not btn.is_disabled():
                btn.click()
                time.sleep(2)
                return True
        except Exception:
            continue
    
    try:
        buttons = page.query_selector_all("div[class*='MuiTablePagination'] button")
        if len(buttons) >= 2:
            btn = buttons[-1]
            if btn.is_visible() and not btn.is_disabled():
                btn.click()
                time.sleep(2)
                return True
    except Exception:
        pass
        
    return False


def scrape_and_aggregate_deals(page, context):
    """
    Fallback: Scrape deal report and AGGREGATE into net positions.
    BUY deals increase quantity, SELL deals decrease quantity.
    Only output symbols with remaining quantity > 0.
    """
    from datetime import datetime, timedelta
    
    all_deals = []
    seen_deals = set()

    # Generate date range slices (DNSE limits to 365 days per query)
    end_dt = datetime.now()
    slices = []
    for years_back in range(5):
        t = end_dt - timedelta(days=365 * years_back)
        f = end_dt - timedelta(days=365 * (years_back + 1))
        slices.append((f.strftime("%d/%m/%Y"), t.strftime("%d/%m/%Y")))

    log(f"[INFO] Prepared date range queries: {slices}")

    for idx, (fromDate, toDate) in enumerate(slices):
        log(f"[INFO] Querying range slice {idx+1}/5: {fromDate} to {toDate}")
        try:
            to_date_input = page.locator("#deal-report-filter-toDate")
            if to_date_input.is_visible():
                to_date_input.click()
                to_date_input.press("Control+A")
                to_date_input.press("Delete")
                to_date_input.fill(toDate)
                to_date_input.press("Enter")
                time.sleep(0.5)

            from_date_input = page.locator("#deal-report-filter-fromDate")
            if from_date_input.is_visible():
                from_date_input.click()
                from_date_input.press("Control+A")
                from_date_input.press("Delete")
                from_date_input.fill(fromDate)
                from_date_input.press("Enter")
                time.sleep(3)
            
            has_next = True
            page_num = 1
            while has_next:
                log(f"[INFO] Scrape page {page_num} for range {fromDate} - {toDate}")
                slice_deals = scrape_current_table(page)
                for d in slice_deals:
                    k = (d["Symbol"], d["OpenTime"], str(d["Qty"]))
                    if k not in seen_deals:
                        seen_deals.add(k)
                        all_deals.append(d)
                
                if click_next_page(page):
                    page_num += 1
                else:
                    has_next = False

        except Exception as ex:
            log(f"[WARN] Error scraping date slice {fromDate} to {toDate}: {ex}")

    # === AGGREGATE deals into net positions per symbol ===
    log(f"[INFO] Total raw deals scraped: {len(all_deals)}. Aggregating into net positions...")
    
    positions = {}  # symbol → {qty, total_cost, pnl, first_date}
    
    for deal in all_deals:
        symbol = deal["Symbol"]
        qty = deal["Qty"]
        
        if symbol not in positions:
            positions[symbol] = {
                "net_qty": 0,
                "total_cost": 0,  # For weighted avg price calculation
                "pnl": 0,
                "first_date": deal["OpenTime"],
            }
        
        pos = positions[symbol]
        pnl_val = parse_number(deal["PnlText"])
        
        if deal["OrderType"] == "BUY":
            pos["net_qty"] += qty
            pos["total_cost"] += qty  # We'll use PnL to derive entry price later
        elif deal["OrderType"] == "SELL":
            pos["net_qty"] -= qty
        
        pos["pnl"] += pnl_val
        
        if not pos["first_date"] and deal["OpenTime"]:
            pos["first_date"] = deal["OpenTime"]
    
    # Build output: only include symbols with positive net quantity
    holdings = []
    for symbol, pos in positions.items():
        if pos["net_qty"] <= 0:
            log(f"[INFO] {symbol}: net qty = {pos['net_qty']} (fully sold or short), skipping.")
            continue
        
        holdings.append({
            "DealText": symbol,
            "QtyText": str(pos["net_qty"]),
            "OpenTimeText": pos["first_date"],
            "StatusText": "OPEN",
            "PnlText": str(pos["pnl"]),
            "AvgPrice": "0",
            "MarketPrice": "0",
            "InvestedValue": "0",
        })
        log(f"[INFO] Aggregated: {symbol} net_qty={pos['net_qty']}, pnl={pos['pnl']}")
    
    return holdings


# =============================================================================
# Login & Main Flow
# =============================================================================

def wait_for_login(page, context):
    """Wait for successful login by monitoring URL and page content."""
    log("[INFO] Please complete CAPTCHA/OTP manually in the browser if needed.")
    log("[INFO] Waiting for login success...")
    
    save_debug_screenshot(page, "debug_login")
    
    for i in range(180):  # 3 minutes max wait
        time.sleep(1)
        try:
            pages = context.pages
            active_page = page

            for p_tab in pages:
                try:
                    p_url = p_tab.url.strip("/")
                    if p_url == "https://entradex.dnse.com.vn":
                        active_page = p_tab
                    # Check if any tab shows portfolio or dashboard
                    if any(kw in p_url for kw in ["tai-san", "danh-muc", "portfolio"]):
                        active_page = p_tab
                        break
                except Exception:
                    continue

            if i % 10 == 0:
                save_debug_screenshot(active_page, "debug_login")

            current_url = active_page.url.strip("/")
            
            if i % 5 == 0:
                log(f"[DEBUG] Wait {i + 1}/180, url={active_page.url}")

            # Check for dashboard indicators
            is_logged_in = False
            
            # URL-based check
            if current_url in ["https://entradex.dnse.com.vn", "https://entradex.dnse.com.vn/"]:
                is_logged_in = True
            
            # Content-based check
            if not is_logged_in:
                try:
                    menu_keywords = ["Cổ phiếu", "Tài sản", "Danh mục", "Tài khoản", "Báo cáo"]
                    for kw in menu_keywords:
                        if active_page.locator(f"text={kw}").first.is_visible(timeout=300):
                            is_logged_in = True
                            log(f"[INFO] Dashboard element '{kw}' detected.")
                            break
                except Exception:
                    pass
            
            if is_logged_in:
                log(f"[INFO] Login successful! URL: {current_url}")
                return active_page
                
        except Exception as ex:
            if "Target page, context or browser has been closed" in str(ex):
                log("[ERROR] Browser was closed during login wait.")
                return None
    
    log("[ERROR] Timeout waiting for login (3 minutes).")
    return None


def main():
    log(f"[INFO] Script dir: {BASE_DIR}")
    log(f"[INFO] Output JSON: {OUTPUT_JSON}")

    if len(sys.argv) < 3:
        log("[ERROR] Usage: python dnse_scraper.py <username> <password>")
        sys.exit(1)

    username = sys.argv[1]
    password = sys.argv[2]

    if not username or not password:
        log("[ERROR] Username/password is empty")
        sys.exit(1)

    log("[INFO] Starting browser for DNSE login...")

    with sync_playwright() as p:
        browser = None
        launch_errors = []

        for channel in ["chrome", "msedge"]:
            try:
                browser = p.chromium.launch(headless=False, channel=channel, args=["--start-maximized"])
                log(f"[INFO] Browser launched via channel: {channel}")
                break
            except Exception as ex:
                launch_errors.append(f"{channel}: {ex}")
                log(f"[WARN] Launch failed for {channel}: {ex}")

        if browser is None:
            try:
                browser = p.chromium.launch(headless=False, args=["--start-maximized"])
                log("[INFO] Browser launched via bundled Chromium")
            except Exception as ex:
                log(f"[ERROR] Browser launch failed: {ex}")
                log("[ERROR] Install Playwright browsers with: playwright install")
                if launch_errors:
                    log("[ERROR] Channel errors: " + " | ".join(launch_errors))
                sys.exit(1)

        try:
            context = browser.new_context(viewport=None)
            page = context.new_page()

            # --- Login Flow ---
            log("[INFO] Opening login page...")
            page.goto("https://entradex.dnse.com.vn/dang-nhap", wait_until="domcontentloaded", timeout=30000)
            log(f"[INFO] Current URL: {page.url}")

            try:
                page.wait_for_load_state("networkidle", timeout=10000)
            except Exception:
                pass

            # Auto-fill credentials
            try:
                selectors = [
                    "input[type='text']",
                    "input[placeholder*='Số điện thoại']",
                    "input[placeholder*='Tên đăng nhập']",
                    "input[placeholder*='Mã KH']",
                ]
                username_input, used_selector = find_first_input(page, selectors)
                if username_input:
                    username_input.click()
                    username_input.fill(username)
                    log(f"[INFO] Filled username using selector: {used_selector}")
                else:
                    log("[WARN] Username input not found automatically")

                password_input = page.locator("input[type='password']").first
                password_input.fill(password)
                log("[INFO] Filled password field")
            except Exception as ex:
                log(f"[WARN] Auto-fill failed: {ex}")

            # Click login button
            try:
                login_btn, btn_selector = find_login_button(page)
                if login_btn:
                    login_btn.click()
                    log(f"[INFO] Clicked login button: {btn_selector}")
                else:
                    log("[WARN] Login button not found. Please click manually.")
            except Exception as ex:
                log(f"[WARN] Failed to click login: {ex}")

            # Wait for login to complete
            active_page = wait_for_login(page, context)
            if active_page is None:
                log("[ERROR] Login failed or timed out.")
                browser.close()
                sys.exit(1)
            
            page = active_page
            
            # === PHASE 1: Try Portfolio Page (Primary — gets ACTUAL current holdings) ===
            holdings = []
            
            log("[INFO] ===== PHASE 1: Portfolio/Assets Page Scraping =====")
            if navigate_to_portfolio(page):
                save_debug_screenshot(page, "debug_portfolio_page")
                holdings = scrape_portfolio_holdings(page)
                
                if holdings:
                    log(f"[INFO] Phase 1 SUCCESS: Found {len(holdings)} holdings from portfolio page.")
                else:
                    log("[WARN] Phase 1: Portfolio page found but no holdings scraped.")
            
            # === PHASE 2: Fallback to Deal Report Aggregation ===
            if not holdings:
                log("[INFO] ===== PHASE 2: Deal Report Aggregation (Fallback) =====")
                try:
                    log("[INFO] Navigating to /bao-cao/deal...")
                    page.goto("https://entradex.dnse.com.vn/bao-cao/deal", wait_until="domcontentloaded", timeout=20000)
                    time.sleep(3)
                    
                    # Check if we actually reached the deal page
                    if "bao-cao/deal" in page.url or "deal" in page.url.lower():
                        save_debug_screenshot(page, "debug_deal_page")
                        holdings = scrape_and_aggregate_deals(page, context)
                        
                        if holdings:
                            log(f"[INFO] Phase 2 SUCCESS: Aggregated {len(holdings)} net positions from deal history.")
                        else:
                            log("[WARN] Phase 2: No deals found to aggregate.")
                    else:
                        log(f"[WARN] Could not reach deal page. Current URL: {page.url}")
                        
                except Exception as ex:
                    log(f"[ERROR] Phase 2 failed: {ex}")
            
            # === Output Results ===
            if holdings:
                log(f"[INFO] Final result: {len(holdings)} positions to sync:")
                for h in holdings:
                    log(f"  - {h['DealText']}: qty={h['QtyText']}, pnl={h['PnlText']}, avg_price={h.get('AvgPrice', 'N/A')}")
                
                OUTPUT_JSON.write_text(json.dumps(holdings, ensure_ascii=False, indent=4), encoding="utf-8")
                log(f"[INFO] Saved JSON to {OUTPUT_JSON}")
            else:
                log("[ERROR] No holdings found from any scraping method.")
                browser.close()
                sys.exit(1)

        finally:
            try:
                browser.close()
            except Exception:
                pass


if __name__ == "__main__":
    main()
