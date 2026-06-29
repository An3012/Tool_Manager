# 🎨 Page Transitions & Animations Guide

## Tổng Quan

Ứng dụng AI Trading System hiện có hệ thống transitions toàn diện cho tất cả các trang view, cung cấp trải nghiệm người dùng mượt mà và chuyên nghiệp.

## 📁 Các File Transition

### 1. **CSS Files**
- `~/css/transitions.css` - Hiệu ứng transition chính
- `~/css/view-transitions.css` - Transitions cho tất cả views

### 2. **JavaScript File**
- `~/js/transitions.js` - Xử lý page navigation transitions

### 3. **HTML Integration**
- `~/Views/Shared/_Layout.cshtml` - Đã cập nhật để load transition files

## 🎯 Các Hiệu Ứng Chính

### 1. **Page Fade-In** (Khi tải trang mới)
```css
@keyframes pageIn {
	from: opacity 0%, translateY 10px
	to: opacity 100%, translateY 0
}
```
- Thời gian: 0.5 giây
- Easing: ease-out

### 2. **Slide-In-Up** (Cards, sections)
```css
@keyframes slideInUp {
	from: opacity 0%, translateY 20px
	to: opacity 100%, translateY 0
}
```
- Các cards xuất hiện lần lượt từ dưới lên
- Có stagger delay giữa các phần tử

### 3. **Slide-In-Left** (Table rows, list items)
```css
@keyframes slideInLeft {
	from: opacity 0%, translateX -20px
	to: opacity 100%, translateX 0
}
```
- Hàng trong bảng xuất hiện từ trái sang
- Delay tăng dần: 0.05s → 0.1s → 0.15s, vv...

### 4. **Button Hover Effect**
```
Scale: 1 → 1.02 (nhẹ)
Shadow: Tăng từ 4px → 16px
Duration: 0.3s
```

### 5. **Modal Fade-In**
```css
@keyframes fadeIn {
	from: opacity 0%
	to: opacity 100%
}
```
- Modal slide xuống từ trên: translateY -30px → 0
- Duration: 0.3 giây

## 📊 Hiệu Ứng Theo Trang

### Trang AiPlanGenerator (`/AiPlanGenerator`)
- ✅ Fade-in page
- ✅ Cards slide-in-up
- ✅ Modals fade-in
- ✅ Buttons hover effects
- ✅ Form inputs focus scale

### Trang PlanAnalytics (`/PlanAnalytics`)
- ✅ Fade-in page
- ✅ Metric cards slide-in-up với stagger
- ✅ Table rows slide-in-left
- ✅ Progress bars animate
- ✅ Summary cards pop-in

### Views Copilot (Index, Portfolio, Account)
- ✅ Cards fade-in
- ✅ Charts slide-in
- ✅ Badges pop-in
- ✅ Links underline on hover

## 🎮 Interactive Effects

### Form Elements
- **Focus**: Scale 1.01x + Glow shadow
- **Hover**: Border color change
- **Duration**: 0.3s

### Buttons
- **Hover**: Translate-Y -2px + Shadow
- **Active**: Reset to normal
- **Duration**: 0.3s

### Links
- **Hover**: Underline appears
- **Decoration**: Smooth transition

## 🔊 Respects User Preferences

### prefers-reduced-motion
Nếu user bật "Reduce motion" trong hệ thống:
- Tất cả animations bị disable
- Chỉ còn transitions 0.01ms (basically instant)
- Scroll behavior: auto (không smooth)

```css
@media (prefers-reduced-motion: reduce) {
	* {
		animation: none !important;
		transition: none !important;
	}
}
```

## 📱 JavaScript Helpers

### showLoading(message)
```javascript
window.showLoading('Đang xử lý...');
// Hiển thị overlay loading với icon quay
```

### showSuccess(message, duration)
```javascript
window.showSuccess('Hoàn thành!', 3000);
// Thông báo xanh từ phải sang trái, mất sau 3 giây
```

### showError(message, duration)
```javascript
window.showError('Có lỗi!', 5000);
// Thông báo đỏ từ phải sang trái, mất sau 5 giây
```

### showWarning(message, duration)
```javascript
window.showWarning('Cảnh báo!', 4000);
// Thông báo cam từ phải sang trái, mất sau 4 giây
```

## 📈 Performance Considerations

1. **Hardware Acceleration**
   - Sử dụng `transform` và `opacity` (GPU-accelerated)
   - Tránh animate: width, height, position

2. **Animation Duration**
   - Short: 0.3s (hover effects, micro-interactions)
   - Medium: 0.5s (page fade-in)
   - Long: 0.6-0.7s (complex animations)

3. **Stagger Delays**
   - Cards: 0.1s - 0.5s
   - Rows: 0.05s - 0.3s
   - Elements: 0.1s increments

## 🛠️ Sử Dụng CSS Classes

### Data Attributes (HTML)
```html
<!-- Fade-in on scroll -->
<div data-reveal>Content</div>

<!-- Delayed fade-in -->
<div data-delay="100">Item 1</div>
<div data-delay="200">Item 2</div>

<!-- Skip transition on submit -->
<form data-no-transition>
</form>
```

### CSS Classes
```html
<!-- Manual reveal -->
<div class="reveal">Content</div>

<!-- Fade in with delay -->
<div class="fade-in-delayed">Item</div>

<!-- Skeleton loading -->
<div class="skeleton"></div>
```

## 🎨 Tùy Chỉnh Transitions

### Để thêm transition riêng:

**CSS:**
```css
@keyframes customAnimation {
	from {
		opacity: 0;
		transform: translateY(30px);
	}
	to {
		opacity: 1;
		transform: translateY(0);
	}
}

.custom-element {
	animation: customAnimation 0.6s ease-out;
}
```

**JavaScript:**
```javascript
document.querySelectorAll('.my-element').forEach((el, index) => {
	el.style.animationDelay = (index * 100) + 'ms';
	el.classList.add('custom-element');
});
```

## 🐛 Troubleshooting

### Animations không chạy
- Kiểm tra browser support (tất cả modern browsers hỗ trợ)
- Kiểm tra `prefers-reduced-motion` setting
- Xem DevTools Console cho errors

### Performance issues
- Giảm số lượng animations
- Sử dụng `will-change` sparingly
- Kiểm tra FPS với DevTools Performance tab

### Scroll animations không trigger
- Đảm bảo element có `data-reveal` attribute
- Kiểm tra Intersection Observer support

## 📚 Browser Support

| Browser | Support |
|---------|---------|
| Chrome  | ✅ Full |
| Firefox | ✅ Full |
| Safari  | ✅ Full |
| Edge    | ✅ Full |
| IE 11   | ⚠️ Partial |

## 🎓 Best Practices

1. ✅ **DO**: Sử dụng transitions cho hover/focus states
2. ✅ **DO**: Respect user's motion preferences
3. ✅ **DO**: Giữ animations <= 1 giây
4. ✅ **DO**: Sử dụng stagger để tạo visual hierarchy
5. ❌ **DON'T**: Animate quá nhiều phần tử cùng lúc
6. ❌ **DON'T**: Sử dụng animations không cần thiết
7. ❌ **DON'T**: Animate position/width/height (dùng transform)

## 📞 Support

Để thêm transitions vào new elements:

1. Thêm `data-reveal` attribute
2. Hoặc thêm class từ `transitions.css` hoặc `view-transitions.css`
3. Hoặc viết custom CSS animation
