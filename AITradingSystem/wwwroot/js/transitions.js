/**
 * Page Transitions Script
 * Xử lý hiệu ứng chuyển tiếp giữa các trang
 */

document.addEventListener('DOMContentLoaded', function () {
    // Add page transition class to body on load
    document.body.classList.add('page-transition');

    // Handle link clicks for smooth transitions
    const links = document.querySelectorAll('a:not([target="_blank"]):not([href^="#"]):not([href^="javascript"]):not([download])');

    links.forEach(link => {
        link.addEventListener('click', function (e) {
            const href = this.href;
            if (!href || href === '#' || href.startsWith('javascript:')) {
                return;
            }

            const isExternalLink = !href.includes(window.location.hostname);

            // Skip external links and special links
            if (isExternalLink) {
                return;
            }

            // Only apply transition for same-domain navigation
            e.preventDefault();

            // Add fade out animation
            document.body.classList.remove('page-transition');
            document.body.classList.add('page-transition-out');

            // Navigate after animation
            setTimeout(() => {
                window.location.href = href;
            }, 300);
        });
    });

    // Reveal elements on scroll
    const observerOptions = {
        threshold: 0.1,
        rootMargin: '0px 0px -100px 0px'
    };

    const observer = new IntersectionObserver(function (entries) {
        entries.forEach(entry => {
            if (entry.isIntersecting) {
                entry.target.classList.add('reveal');
                observer.unobserve(entry.target);
            }
        });
    }, observerOptions);

    // Observe all elements with reveal class
    const revealElements = document.querySelectorAll('[data-reveal]');
    revealElements.forEach(el => {
        observer.observe(el);
    });

    // Smooth scroll for anchor links
    document.querySelectorAll('a[href^="#"]').forEach(anchor => {
        anchor.addEventListener('click', function (e) {
            const href = this.getAttribute('href');
            if (href !== '#' && document.querySelector(href)) {
                e.preventDefault();
                document.querySelector(href).scrollIntoView({
                    behavior: 'smooth',
                    block: 'start'
                });
            }
        });
    });

    // Add fade-in class to delayed elements
    const delayedElements = document.querySelectorAll('[data-delay]');
    delayedElements.forEach((el, index) => {
        const delay = el.getAttribute('data-delay') || (index * 100);
        el.style.animationDelay = delay + 'ms';
        el.classList.add('fade-in-delayed');
    });

    // Handle form submit transitions
    const forms = document.querySelectorAll('form:not([data-no-transition])');
    forms.forEach(form => {
        form.addEventListener('submit', function () {
            document.body.classList.remove('page-transition');
            document.body.classList.add('page-transition-out');
        });
    });

    // Loading state animations
    window.showLoading = function (message = 'Đang xử lý...') {
        const loadingDiv = document.createElement('div');
        loadingDiv.id = 'loading-overlay';
        loadingDiv.style.cssText = `
            position: fixed;
            top: 0;
            left: 0;
            right: 0;
            bottom: 0;
            background: rgba(0, 0, 0, 0.7);
            display: flex;
            justify-content: center;
            align-items: center;
            z-index: 9999;
            animation: fadeIn 0.3s ease-out;
        `;

        loadingDiv.innerHTML = `
            <div style="text-align: center;">
                <div style="font-size: 40px; margin-bottom: 16px; animation: spin 1s linear infinite;">⚙️</div>
                <p style="color: white; font-size: 16px; margin: 0;">${message}</p>
            </div>
        `;

        document.body.appendChild(loadingDiv);
        return loadingDiv;
    };

    window.hideLoading = function () {
        const loadingDiv = document.getElementById('loading-overlay');
        if (loadingDiv) {
            loadingDiv.remove();
        }
    };

    // Success notification
    window.showSuccess = function (message, duration = 3000) {
        const notification = document.createElement('div');
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: rgba(16, 185, 129, 0.95);
            color: white;
            padding: 16px 24px;
            border-radius: 8px;
            z-index: 10000;
            animation: slideInRight 0.5s ease-out;
            max-width: 400px;
            box-shadow: 0 8px 16px rgba(0, 0, 0, 0.2);
        `;
        notification.innerHTML = `✅ ${message}`;

        document.body.appendChild(notification);

        setTimeout(() => {
            notification.style.animation = 'slideOutRight 0.3s ease-in';
            setTimeout(() => notification.remove(), 300);
        }, duration);
    };

    // Error notification
    window.showError = function (message, duration = 5000) {
        const notification = document.createElement('div');
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: rgba(239, 68, 68, 0.95);
            color: white;
            padding: 16px 24px;
            border-radius: 8px;
            z-index: 10000;
            animation: slideInRight 0.5s ease-out;
            max-width: 400px;
            box-shadow: 0 8px 16px rgba(0, 0, 0, 0.2);
        `;
        notification.innerHTML = `❌ ${message}`;

        document.body.appendChild(notification);

        setTimeout(() => {
            notification.style.animation = 'slideOutRight 0.3s ease-in';
            setTimeout(() => notification.remove(), 300);
        }, duration);
    };

    // Warning notification
    window.showWarning = function (message, duration = 4000) {
        const notification = document.createElement('div');
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            right: 20px;
            background: rgba(245, 158, 11, 0.95);
            color: white;
            padding: 16px 24px;
            border-radius: 8px;
            z-index: 10000;
            animation: slideInRight 0.5s ease-out;
            max-width: 400px;
            box-shadow: 0 8px 16px rgba(0, 0, 0, 0.2);
        `;
        notification.innerHTML = `⚠️ ${message}`;

        document.body.appendChild(notification);

        setTimeout(() => {
            notification.style.animation = 'slideOutRight 0.3s ease-in';
            setTimeout(() => notification.remove(), 300);
        }, duration);
    };

    // Define slide animations in CSS if not already defined
    if (!document.querySelector('style[data-transitions]')) {
        const style = document.createElement('style');
        style.setAttribute('data-transitions', 'true');
        style.textContent = `
            @keyframes slideInRight {
                from {
                    opacity: 0;
                    transform: translateX(30px);
                }
                to {
                    opacity: 1;
                    transform: translateX(0);
                }
            }

            @keyframes slideOutRight {
                from {
                    opacity: 1;
                    transform: translateX(0);
                }
                to {
                    opacity: 0;
                    transform: translateX(30px);
                }
            }
        `;
        document.head.appendChild(style);
    }

    console.log('✅ Page transitions initialized');
});

// Detect page visibility changes
document.addEventListener('visibilitychange', function () {
    if (document.hidden) {
        // Page is hidden
    } else {
        // Page is visible - refresh animations if needed
        document.body.classList.remove('page-transition-out');
        document.body.classList.add('page-transition');
    }
});
