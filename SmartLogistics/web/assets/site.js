(function () {
  var reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  function $(s, r) { return (r || document).querySelector(s); }
  function $$(s, r) { return [].slice.call((r || document).querySelectorAll(s)); }

  /* ---------- Izbornik ---------- */
  var nav = $('#nav'), menuBtn = $('#menuBtn');
  if (nav && menuBtn) {
    menuBtn.addEventListener('click', function () {
      var open = nav.classList.toggle('open');
      menuBtn.setAttribute('aria-expanded', open);
    });
    var subs = $$('.has-sub', nav);
    subs.forEach(function (li) {
      var btn = $('.sub-toggle', li);
      btn.addEventListener('click', function (e) {
        e.stopPropagation();
        var open = !li.classList.contains('open');
        subs.forEach(function (o) { o.classList.remove('open'); $('.sub-toggle', o).setAttribute('aria-expanded', 'false'); });
        li.classList.toggle('open', open);
        btn.setAttribute('aria-expanded', open);
      });
    });
    function closeSubs() {
      subs.forEach(function (o) { o.classList.remove('open'); $('.sub-toggle', o).setAttribute('aria-expanded', 'false'); });
    }
    document.addEventListener('click', closeSubs);
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape') closeSubs(); });
  }

  /* ---------- Hero slider (početna) ---------- */
  var hero = $('#hero');
  if (hero) {
    var slides = $$('.slide', hero), dotsBox = $('#dots'), current = 0, timer = null;
    var dots = slides.map(function (_, i) {
      var b = document.createElement('button');
      b.type = 'button';
      b.setAttribute('aria-label', 'Slajd ' + (i + 1) + ' od ' + slides.length);
      b.addEventListener('click', function () { go(i); restart(); });
      dotsBox.appendChild(b);
      return b;
    });
    function go(n) {
      current = (n + slides.length) % slides.length;
      slides.forEach(function (s, i) {
        var on = i === current;
        s.classList.toggle('active', on);
        s.setAttribute('aria-hidden', !on);
        if ('inert' in s) s.inert = !on;
        dots[i].setAttribute('aria-current', on);
      });
    }
    function stop() { clearInterval(timer); timer = null; }
    function start() { if (!reduced && !timer) timer = setInterval(function () { go(current + 1); }, 6500); }
    function restart() { stop(); start(); }
    $('#prev').addEventListener('click', function () { go(current - 1); restart(); });
    $('#next').addEventListener('click', function () { go(current + 1); restart(); });
    hero.addEventListener('mouseenter', stop);
    hero.addEventListener('mouseleave', start);
    hero.addEventListener('focusin', stop);
    hero.addEventListener('focusout', start);
    document.addEventListener('visibilitychange', function () { document.hidden ? stop() : start(); });
    var touchX = null;
    hero.addEventListener('touchstart', function (e) { touchX = e.touches[0].clientX; }, { passive: true });
    hero.addEventListener('touchend', function (e) {
      if (touchX === null) return;
      var dx = e.changedTouches[0].clientX - touchX;
      if (Math.abs(dx) > 50) { go(current + (dx < 0 ? 1 : -1)); restart(); }
      touchX = null;
    }, { passive: true });
    go(0); start();
  }

  /* ---------- Pojavljivanje sekcija ---------- */
  var items = $$('.reveal');
  if ('IntersectionObserver' in window && !reduced) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (en) {
        if (en.isIntersecting) { en.target.classList.add('in'); io.unobserve(en.target); }
      });
    }, { threshold: 0.08 });
    items.forEach(function (el) { io.observe(el); });
  } else {
    items.forEach(function (el) { el.classList.add('in'); });
  }

  /* ---------- Na vrh ---------- */
  var toTop = $('#toTop');
  if (toTop) {
    window.addEventListener('scroll', function () { toTop.classList.toggle('show', window.scrollY > 600); }, { passive: true });
    toTop.addEventListener('click', function () { window.scrollTo({ top: 0, behavior: reduced ? 'auto' : 'smooth' }); });
  }

  /* ---------- Katalog: pretraga i filter ---------- */
  var cat = $('[data-catalog]');
  if (cat) {
    var cards = $$('.m-card', cat), input = $('#catSearch'), chips = $$('.chip'), count = $('#catCount'), empty = $('#catEmpty');
    var group = '';
    function norm(s) { return s.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/đ/g, 'd'); }
    function apply() {
      var q = norm(input ? input.value.trim() : ''), shown = 0;
      cards.forEach(function (c) {
        var ok = (!q || norm(c.dataset.name).indexOf(q) > -1) && (!group || c.dataset.group === group);
        c.hidden = !ok;
        if (ok) shown++;
      });
      if (count) count.textContent = shown + ' od ' + cards.length;
      if (empty) empty.classList.toggle('show', shown === 0);
    }
    if (input) input.addEventListener('input', apply);
    chips.forEach(function (ch) {
      ch.addEventListener('click', function () {
        group = ch.dataset.group || '';
        chips.forEach(function (o) { o.setAttribute('aria-pressed', o === ch); });
        apply();
      });
    });
    var reset = $('#catReset');
    if (reset) reset.addEventListener('click', function () {
      if (input) input.value = '';
      group = '';
      chips.forEach(function (o, i) { o.setAttribute('aria-pressed', i === 0); });
      apply();
    });
    apply();
  }

  /* ---------- Video (učitava YouTube tek na klik) ---------- */
  $$('.video[data-yt]').forEach(function (v) {
    v.addEventListener('click', function () {
      var f = document.createElement('iframe');
      f.src = 'https://www.youtube-nocookie.com/embed/' + v.dataset.yt + '?autoplay=1&rel=0';
      f.title = v.getAttribute('aria-label') || 'Video';
      f.allow = 'accelerometer; autoplay; encrypted-media; gyroscope; picture-in-picture';
      f.allowFullscreen = true;
      var box = document.createElement('div');
      box.className = 'video';
      box.appendChild(f);
      v.replaceWith(box);
    });
  });

  /* ---------- Novosti: prikaži više ---------- */
  $$('.post').forEach(function (p) {
    var txt = $('.txt', p), btn = $('.foot button', p);
    if (!txt || !btn) return;
    if (txt.scrollHeight <= txt.clientHeight + 2) { btn.hidden = true; return; }
    btn.addEventListener('click', function () {
      var open = p.classList.toggle('open');
      btn.textContent = open ? 'Prikaži manje' : 'Prikaži više';
      btn.setAttribute('aria-expanded', open);
    });
  });

  /* ---------- Kontakt obrasci: otvaraju e-mail program s ispunjenom porukom ---------- */
  $$('form.js-mailto').forEach(function (form) {
    var msg = $('.form-msg', form);
    form.addEventListener('submit', function (e) {
      e.preventDefault();
      if (!form.checkValidity()) { form.reportValidity(); return; }
      var d = new FormData(form);
      var body = 'Ime i prezime: ' + d.get('name') + '\nTvrtka: ' + d.get('company') + '\nKontakt broj: ' + d.get('phone') +
                 '\nE-mail: ' + d.get('email') + '\n\n' + d.get('message');
      window.location.href = 'mailto:' + form.dataset.to + '?subject=' + encodeURIComponent(d.get('title')) + '&body=' + encodeURIComponent(body);
      if (msg) msg.textContent = 'Otvaramo Vaš e-mail program s pripremljenom porukom.';
    });
  });

  /* ---------- Letak: samo provjera unosa; slanje još nije povezano s poslužiteljem ---------- */
  var nl = $('#nlForm'), nlMsg = $('#nlMsg');
  if (nl) nl.addEventListener('submit', function (e) {
    e.preventDefault();
    var inp = $('#nlEmail');
    if (!inp.value.trim() || !inp.checkValidity()) { nlMsg.textContent = 'Unesite ispravnu e-mail adresu.'; inp.focus(); return; }
    nlMsg.textContent = 'Demo: prijava na letak još nije povezana s poslužiteljem.';
  });

  var y = $('#year');
  if (y) y.textContent = new Date().getFullYear();
})();
