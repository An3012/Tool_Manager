/**
 * Currency Formatter for Vietnamese Dong (VND)
 * Định dạng input số tiền theo chuẩn Việt Nam
 */

document.addEventListener('DOMContentLoaded', function () {
    // Các selector cho input tiền tệ
    const currencyInputs = document.querySelectorAll(
        'input[type="number"][asp-for*="Capital"],' +
        'input[type="number"][asp-for*="TargetProfit"],' +
        'input[type="number"][asp-for*="Profit"],' +
        'input[type="number"][asp-for*="Amount"],' +
        'input[type="number"][placeholder*="VND"],' +
        'input[type="number"][placeholder*="đ"]'
    );

    currencyInputs.forEach(input => {
        // Format display value
        input.addEventListener('blur', function () {
            if (this.value) {
                const value = parseInt(this.value);
                if (!isNaN(value)) {
                    this.value = value.toLocaleString('vi-VN');
                }
            }
        });

        // Clean value on focus for editing
        input.addEventListener('focus', function () {
            if (this.value) {
                const cleaned = this.value.replace(/\./g, '');
                this.value = cleaned;
            }
        });

        // Prevent non-numeric input
        input.addEventListener('keypress', function (e) {
            const char = String.fromCharCode(e.which);
            if (!/[0-9]/.test(char)) {
                e.preventDefault();
            }
        });

        // Clean on paste
        input.addEventListener('paste', function (e) {
            e.preventDefault();
            const text = (e.clipboardData || window.clipboardData).getData('text');
            const cleaned = text.replace(/[^\d]/g, '');
            this.value = cleaned;
            this.dispatchEvent(new Event('input'));
        });

        // Initial formatting if value exists
        if (input.value) {
            const value = parseInt(input.value.replace(/\./g, ''));
            if (!isNaN(value)) {
                input.value = value.toLocaleString('vi-VN');
            }
        }
    });

    // Setup form submit to clean values
    const forms = document.querySelectorAll('form');
    forms.forEach(form => {
        form.addEventListener('submit', function (e) {
            const numberInputs = this.querySelectorAll('input[type="number"]');
            numberInputs.forEach(input => {
                if (input.value) {
                    // Remove formatting for submission
                    const cleaned = input.value.replace(/\./g, '');
                    input.value = cleaned;
                }
            });
        });
    });

    console.log('✅ Currency formatter initialized');
});

// Export function for manual formatting
window.formatCurrency = function (value) {
    if (!value) return '';
    const num = parseInt(value.toString().replace(/\D/g, ''));
    return isNaN(num) ? '' : num.toLocaleString('vi-VN');
};

window.cleanCurrency = function (value) {
    if (!value) return '';
    return value.toString().replace(/\D/g, '');
};
