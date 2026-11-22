// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Telefon alanlarında rakam ve + ( ) - boşluk dışındaki karakterleri anında engeller.
document.addEventListener('input', function (e) {
    var el = e.target;
    if (el.matches && el.matches('input[type="tel"]')) {
        var cleaned = el.value.replace(/[^0-9+()\s-]/g, '');
        if (cleaned !== el.value) el.value = cleaned;
    }
});

// data-confirm taşıyan formlar gönderilmeden önce onay ister (metin HTML attribute'ünde, inline JS yok).
document.addEventListener('submit', function (e) {
    var btn = e.submitter && e.submitter.getAttribute('data-confirm');
    var msg = btn || (e.target.getAttribute && e.target.getAttribute('data-confirm'));
    if (msg && !window.confirm(msg)) e.preventDefault();
});

// Galeri: küçük resme tıklayınca ana görseli değiştirir, ana görsele tıklayınca büyütür.
document.addEventListener('click', function (e) {
    var thumb = e.target.closest && e.target.closest('[data-gallery-thumb]');
    if (thumb) {
        e.preventDefault();
        var main = document.getElementById('gallery-main');
        if (main) { main.src = thumb.getAttribute('data-gallery-thumb'); }
        document.querySelectorAll('[data-gallery-thumb]').forEach(function (t) { t.classList.remove('active'); });
        thumb.classList.add('active');
        return;
    }
    var zoom = e.target.closest && e.target.closest('[data-lightbox]');
    if (zoom) {
        e.preventDefault();
        var src = zoom.getAttribute('src') || zoom.getAttribute('href');
        var img = document.getElementById('lightbox-img');
        var modalEl = document.getElementById('lightbox');
        if (img && modalEl && window.bootstrap) {
            img.src = src;
            window.bootstrap.Modal.getOrCreateInstance(modalEl).show();
        }
    }
});

// Kaydırma efektleri: beliren öğeler, ilerleme çubuğu, büyüyen gövde, paralaks ve yukarı çık düğmesi.
(function () {
    var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var root = document.documentElement;

    // Etiketlenmemiş sayfa bileşenlerine otomatik reveal ver; aynı satırdakilere kademeli gecikme uygula.
    document.querySelectorAll('main h1, main .section-title, main .wood-card, main .row > [class*="col-"] > .product-card').forEach(function (el) {
        var target = el.classList.contains('product-card') ? el.parentElement : el;
        if (target.hasAttribute('data-reveal') || target.closest('[data-reveal]')) return;
        target.setAttribute('data-reveal', 'up');
    });
    document.querySelectorAll('.row').forEach(function (row) {
        Array.prototype.forEach.call(row.children, function (child, i) {
            if (child.hasAttribute('data-reveal') && !child.style.getPropertyValue('--d')) {
                child.style.setProperty('--d', Math.min(i, 5) * 90 + 'ms');
            }
        });
    });

    var items = document.querySelectorAll('[data-reveal]');
    if (reduce || !('IntersectionObserver' in window)) {
        items.forEach(function (el) { el.classList.add('is-visible'); });
    } else {
        var io = new IntersectionObserver(function (entries) {
            entries.forEach(function (en) {
                if (en.isIntersecting) { en.target.classList.add('is-visible'); io.unobserve(en.target); }
            });
        }, { threshold: 0.12, rootMargin: '0px 0px -6% 0px' });
        items.forEach(function (el) { io.observe(el); });
    }

    var nav = document.querySelector('.site-nav');
    var bar = document.getElementById('scroll-progress');
    var top = document.getElementById('back-top');
    var trunk = document.getElementById('trunk');
    var ticking = false;

    function update() {
        ticking = false;
        var y = window.scrollY || window.pageYOffset;
        var max = Math.max(1, document.documentElement.scrollHeight - window.innerHeight);
        if (bar) bar.style.setProperty('--progress', Math.min(1, y / max).toFixed(4));
        if (nav) nav.classList.toggle('scrolled', y > 8);
        if (top) top.classList.toggle('show', y > 600);
        if (!reduce) root.style.setProperty('--scroll', y);
        if (trunk) {
            var r = trunk.getBoundingClientRect();
            var p = (window.innerHeight * 0.65 - r.top) / r.height;
            trunk.style.setProperty('--grow', reduce ? 1 : Math.max(0, Math.min(1, p)).toFixed(4));
        }
    }
    window.addEventListener('scroll', function () {
        if (!ticking) { ticking = true; window.requestAnimationFrame(update); }
    }, { passive: true });
    window.addEventListener('resize', update);
    update();

    if (top) top.addEventListener('click', function () { window.scrollTo({ top: 0, behavior: reduce ? 'auto' : 'smooth' }); });
})();
