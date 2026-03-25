// RufistoWard Admin UI — ward-admin.js (<5KB)
// CSP-compliant: no inline scripts, no eval

(function() {
  'use strict';

  var prefersReducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  // Command palette (Ctrl+K)
  document.addEventListener('keydown', function(e) {
    if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
      e.preventDefault();
      // TODO: Open command palette modal
    }
  });

  // Toast auto-dismiss
  function initToasts() {
    document.querySelectorAll('[data-testid="toast"]').forEach(function(toast) {
      if (toast.dataset.wardInit) return;
      toast.dataset.wardInit = '1';

      var duration = parseInt(toast.dataset.toastDuration || '5000', 10);
      var timer;

      function startDismiss() {
        timer = setTimeout(function() {
          if (prefersReducedMotion) {
            toast.remove();
          } else {
            toast.style.transition = 'opacity 0.3s, transform 0.3s';
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(1rem)';
            setTimeout(function() { toast.remove(); }, 300);
          }
        }, duration);
      }

      toast.addEventListener('mouseenter', function() { clearTimeout(timer); });
      toast.addEventListener('mouseleave', startDismiss);
      startDismiss();
    });
  }

  // Listen for HTMX toast triggers
  document.addEventListener('htmx:afterSettle', initToasts);
  document.addEventListener('DOMContentLoaded', initToasts);

  // Keyboard navigation shortcuts
  var shortcuts = {
    'g d': '/admin',
    'g a': '/admin/apps',
    'g s': '/admin/scopes',
    'g u': '/admin/users',
    'g r': '/admin/roles'
  };

  var keyBuffer = '';
  var keyTimer;

  document.addEventListener('keydown', function(e) {
    if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.tagName === 'SELECT') return;
    if (e.ctrlKey || e.metaKey || e.altKey) return;
    clearTimeout(keyTimer);
    keyBuffer += e.key + ' ';
    keyTimer = setTimeout(function() { keyBuffer = ''; }, 500);

    var trimmed = keyBuffer.trim();
    if (shortcuts[trimmed]) {
      window.location.href = shortcuts[trimmed];
      keyBuffer = '';
    }
  });
})();
