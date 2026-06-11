/* ============================================================================
   WHITE STITCHES ADMIN — Rich-text editor (Phase 1E‑2)
   A dependency-free WYSIWYG that upgrades any
       <textarea data-editor="rich" data-dir="ltr|rtl">
   into a contenteditable surface with a small formatting toolbar. The HTML is
   mirrored back into the original <textarea> on every change and right before
   the owning form submits, so model binding is unchanged. The server still
   sanitizes the HTML on save (HtmlSanitizer) — this editor is convenience only.
   ============================================================================ */
(function () {
  'use strict';

  var COMMANDS = [
    { cmd: 'bold', label: 'B', title: 'Bold', css: 'font-weight:700' },
    { cmd: 'italic', label: 'I', title: 'Italic', css: 'font-style:italic' },
    { cmd: 'underline', label: 'U', title: 'Underline', css: 'text-decoration:underline' },
    { sep: true },
    { block: 'H2', label: 'H2', title: 'Heading' },
    { block: 'H3', label: 'H3', title: 'Subheading' },
    { block: 'P', label: '¶', title: 'Paragraph' },
    { sep: true },
    { cmd: 'insertUnorderedList', label: '• List', title: 'Bulleted list' },
    { cmd: 'insertOrderedList', label: '1. List', title: 'Numbered list' },
    { block: 'BLOCKQUOTE', label: '❝', title: 'Quote' },
    { sep: true },
    { action: 'link', label: 'Link', title: 'Insert link' },
    { cmd: 'unlink', label: 'Unlink', title: 'Remove link' },
    { sep: true },
    { cmd: 'removeFormat', label: 'Clear', title: 'Clear formatting' }
  ];

  function initEditor(textarea) {
    if (textarea.dataset.editorReady === '1') return;
    textarea.dataset.editorReady = '1';

    var dir = textarea.getAttribute('data-dir') === 'rtl' ? 'rtl' : 'ltr';

    var wrap = document.createElement('div');
    wrap.className = 'rte';

    var toolbar = document.createElement('div');
    toolbar.className = 'rte__toolbar';

    var area = document.createElement('div');
    area.className = 'rte__area';
    area.setAttribute('contenteditable', 'true');
    area.setAttribute('dir', dir);
    area.setAttribute('role', 'textbox');
    area.setAttribute('aria-multiline', 'true');
    area.innerHTML = textarea.value || '';

    function sync() { textarea.value = area.innerHTML.trim(); }

    function exec(command, value) {
      area.focus();
      try { document.execCommand(command, false, value || null); } catch (e) { /* no-op */ }
      sync();
    }

    COMMANDS.forEach(function (c) {
      if (c.sep) {
        var s = document.createElement('span');
        s.className = 'rte__sep';
        toolbar.appendChild(s);
        return;
      }
      var b = document.createElement('button');
      b.type = 'button';
      b.className = 'rte__btn';
      b.title = c.title;
      b.textContent = c.label;
      if (c.css) b.setAttribute('style', c.css);
      // Keep the editor selection while clicking the toolbar.
      b.addEventListener('mousedown', function (e) { e.preventDefault(); });
      b.addEventListener('click', function () {
        if (c.cmd) { exec(c.cmd); }
        else if (c.block) { exec('formatBlock', c.block); }
        else if (c.action === 'link') {
          var url = window.prompt('Link URL (https://…)', 'https://');
          if (url) { exec('createLink', url); }
        }
      });
      toolbar.appendChild(b);
    });

    area.addEventListener('input', sync);
    area.addEventListener('blur', sync);
    if (textarea.form) {
      textarea.form.addEventListener('submit', sync);
    }

    textarea.classList.add('rte__source');
    textarea.parentNode.insertBefore(wrap, textarea);
    wrap.appendChild(toolbar);
    wrap.appendChild(area);
    wrap.appendChild(textarea); // stays in the form for binding, hidden by CSS
  }

  function initAll(root) {
    (root || document).querySelectorAll('textarea[data-editor="rich"]').forEach(initEditor);
  }

  document.addEventListener('DOMContentLoaded', function () { initAll(document); });
  window.WSRichEditor = { initAll: initAll, initEditor: initEditor };
})();
