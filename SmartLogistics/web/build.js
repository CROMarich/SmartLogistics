/* Up Rent — generator statičnih stranica.
   Pokretanje:  node build.js
   Zajedničko zaglavlje/podnožje je ovdje, sadržaj stranica u src/pages/*.html,
   a podaci za katalog i novosti u src/data/*.json. */
const fs = require('fs');
const path = require('path');
const ROOT = __dirname;
const read = f => fs.readFileSync(path.join(ROOT, f), 'utf8');
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

const ARROW = '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 10h14M11 4l6 6-6 6"/></svg>';
const CHECK = '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M4 10.500l4 4 8-9"/></svg>';
const DOWNLOAD = '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="2.4" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10 3v11M5 10l5 5 5-5M4 17h12"/></svg>';
const PLAY = '<svg viewBox="0 0 20 20" fill="currentColor" aria-hidden="true"><path d="M5 3l12 7-12 7z"/></svg>';
const CARET = '<svg viewBox="0 0 12 12" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path d="M2 4l4 4 4-4"/></svg>';
const PHONE = '<svg viewBox="0 0 512 512" fill="currentColor" aria-hidden="true"><path d="M164.9 24.6c-7.7-18.6-28-28.5-47.4-23.2l-88 24C12.1 30.2 0 46 0 64C0 311.4 200.6 512 448 512c18 0 33.8-12.1 38.6-29.5l24-88c5.3-19.4-4.6-39.7-23.2-47.4l-96-40c-16.3-6.8-35.2-2.1-46.300 11.600L304.700 368C234.300 334.700 177.300 277.700 144 207.300L193.300 167c13.700-11.200 18.400-30 11.600-46.300l-40-96z"/></svg>';
const SEARCH = '<svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" aria-hidden="true"><circle cx="9" cy="9" r="6"/><path d="M14 14l4 4"/></svg>';
const ICONS = { arrow: ARROW, check: CHECK, download: DOWNLOAD, play: PLAY };

/* ---------------------------------------------------------------- Navigacija */
const NAV = [
  { href: 'najam.html', label: 'Najam', key: 'najam' },
  { href: 'prodaja.html', label: 'Prodaja', key: 'prodaja' },
  { href: 'rabljeni.html', label: 'Rabljeni strojevi', key: 'rabljeni' },
  { href: 'brandovi.html', label: 'Brandovi', key: 'brandovi' },
  { label: 'Usluge', sub: [
    { href: 'transporti.html', label: 'Transporti', key: 'transporti' },
    { href: 'edukacija.html', label: 'Edukacija', key: 'edukacija' },
    { href: 'click2rent.html', label: 'Click2Rent', key: 'click2rent' },
  ] },
  { label: 'O nama', sub: [
    { href: 'o-nama.html', label: 'O tvrtki', key: 'o-nama' },
    { href: 'novosti.html', label: 'Novosti', key: 'novosti' },
    { href: 'dokumenti.html', label: 'Dokumenti', key: 'dokumenti' },
    { href: 'eu-projekti.html', label: 'EU projekti', key: 'eu-projekti' },
  ] },
  { href: 'kontakt.html', label: 'Kontakt', key: 'kontakt' },
];

function navHtml(cur) {
  const link = i => `<li><a href="${i.href}"${i.key === cur ? ' aria-current="page"' : ''}>${i.label}</a></li>`;
  return NAV.map(i => {
    if (!i.sub) return link(i);
    const on = i.sub.some(s => s.key === cur);
    return `<li class="has-sub${on ? ' current' : ''}">
          <button class="sub-toggle" type="button" aria-haspopup="true" aria-expanded="false">${i.label} ${CARET}</button>
          <ul class="sub">${i.sub.map(link).join('')}</ul>
        </li>`;
  }).join('\n        ');
}

/* ---------------------------------------------------------------- Okvir stranice */
function layout(p, main) {
  return `<!DOCTYPE html>
<html lang="hr">
<head>
<meta charset="UTF-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>${esc(p.title)}</title>
<meta name="description" content="${esc(p.desc)}">
<link rel="stylesheet" href="assets/site.css">
</head>
<body>
<a class="sr-only" href="#sadrzaj">Preskoči na sadržaj</a>

<div class="topbar">
  <div class="wrap">
    <div class="topbar-tags">Najam <span>/</span> Prodaja <span>/</span> Servis <span>/</span> Transport</div>
    <div class="topbar-info">
      <span>Pon – Pet 08:00 – 16:00</span>
      <a href="mailto:info@uprent.hr">info@uprent.hr</a>
    </div>
  </div>
</div>

<header class="header">
  <div class="wrap">
    <a class="logo" href="index.html" title="Početna stranica">
      <img src="assets/uprent-logo.png" alt="Up Rent" width="208" height="63">
    </a>
    <nav class="nav" id="nav" aria-label="Glavna navigacija">
      <ul>
        ${navHtml(p.key)}
      </ul>
    </nav>
    <a class="header-phone" href="tel:+38512455000" aria-label="Nazovite +385 1 24 55 000">${PHONE}<span>+385 1 24 55 000</span></a>
    <button class="menu-btn" id="menuBtn" type="button" aria-controls="nav" aria-expanded="false" aria-label="Izbornik">
      <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" aria-hidden="true"><path d="M3 6h18M3 12h18M3 18h18"/></svg>
    </button>
  </div>
</header>

<main id="sadrzaj">
${main}
</main>

<footer class="footer">
  <div class="wrap">
    <div class="newsletter">
      <div>
        <h2 class="display">Budite u toku</h2>
        <p>Prijavite se za letak i prvi saznajte za novosti i ponude.</p>
      </div>
      <div>
        <!-- TODO: povezati s postojećim servisom za prijavu na letak -->
        <form class="nl-form" id="nlForm" novalidate>
          <label class="sr-only" for="nlEmail">E-mail adresa</label>
          <input id="nlEmail" type="email" name="email" placeholder="Vaša e-mail adresa" maxlength="255" autocomplete="email" required>
          <button class="btn btn-primary" type="submit">Prijavi se</button>
        </form>
        <div class="nl-msg" id="nlMsg" role="status" aria-live="polite"></div>
      </div>
    </div>

    <div class="footer-main">
      <div>
        <img src="assets/uprent-logo.png" alt="Up Rent" width="208" height="63" loading="lazy">
        <p>Najam, prodaja, servis i transport strojeva za rad na visini.</p>
      </div>
      <div>
        <h3>Kontakt</h3>
        <ul>
          <li><a class="footer-phone" href="tel:+38512455000">+385 1 24 55 000</a></li>
          <li><a href="mailto:info@uprent.hr">info@uprent.hr</a></li>
          <li>Industrijska ulica 24, Hrušćica</li>
          <li>08:00 – 16:00</li>
        </ul>
      </div>
      <div>
        <h3>Strojevi</h3>
        <ul>
          <li><a href="najam.html">Najam</a></li>
          <li><a href="prodaja.html">Prodaja</a></li>
          <li><a href="rabljeni.html">Rabljeni strojevi</a></li>
          <li><a href="brandovi.html">Naši brandovi</a></li>
        </ul>
      </div>
      <div>
        <h3>Up Rent</h3>
        <ul>
          <li><a href="o-nama.html">O nama</a></li>
          <li><a href="transporti.html">Transporti</a></li>
          <li><a href="edukacija.html">Edukacija</a></li>
          <li><a href="click2rent.html">Click2Rent</a></li>
          <li><a href="novosti.html">Novosti</a></li>
          <li><a href="dokumenti.html">Dokumenti</a></li>
          <li><a href="eu-projekti.html">EU projekti</a></li>
        </ul>
      </div>
      <div>
        <h3>Pratite nas</h3>
        <ul>
          <li><a href="https://web.facebook.com/uprent.hr" target="_blank" rel="noopener">Facebook</a></li>
          <li><a href="https://www.instagram.com/up_rent_doo/?hl=hr" target="_blank" rel="noopener">Instagram</a></li>
          <li><a href="https://www.linkedin.com/company/up-rent-awp" target="_blank" rel="noopener">LinkedIn</a></li>
          <li><a href="https://www.mascus.hr/up-rent-d-o-o/df454e2b,companycard.html" target="_blank" rel="noopener">Mascus</a></li>
          <li><a href="https://www.njuskalo.hr/tvrtka/up-rent" target="_blank" rel="noopener">Njuškalo</a></li>
        </ul>
      </div>
    </div>
  </div>
  <div class="footer-bottom">
    <div class="wrap">© <span id="year">2026</span> Up Rent d.o.o.</div>
  </div>
</footer>

<button class="to-top" id="toTop" type="button" aria-label="Na vrh stranice">
  <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M10 16V4M4 10l6-6 6 6"/></svg>
</button>

<script src="assets/site.js"></script>
</body>
</html>
`;
}

/* ---------------------------------------------------------------- Zaglavlje podstranice */
function hero(h) {
  const crumbs = [['index.html', 'Početna']].concat(h.crumbs || []);
  return `<section class="page-hero">
  <img src="${h.img}" alt="" fetchpriority="high">
  <div class="wrap">
    <nav aria-label="Putanja"><ol class="crumbs">${crumbs.map(c => `<li><a href="${c[0]}">${c[1]}</a></li>`).join('')}<li aria-current="page">${h.crumb || h.h1}</li></ol></nav>
    <span class="eyebrow">${h.eyebrow}</span>
    <h1 class="display page-title">${h.h1}</h1>
    ${h.lead ? `<p class="lead">${h.lead}</p>` : ''}
    ${h.extra || ''}
  </div>
</section>`;
}

function ctaBand(title, text, mail) {
  return `<section class="section-sm tight-top">
  <div class="wrap">
    <div class="cta-band reveal">
      <div>
        <h2 class="display">${title}</h2>
        <p>${text}</p>
      </div>
      <div class="actions">
        <a class="btn btn-dark" href="tel:+38512455000">+385 1 24 55 000</a>
        <a class="btn btn-outline" href="mailto:${mail}">${mail}</a>
      </div>
    </div>
  </div>
</section>`;
}

/* ---------------------------------------------------------------- Katalog strojeva */
const STROJEVI = JSON.parse(read('src/data/strojevi.json'));
const GROUPS = [
  ['Auto košare', /košar/],
  ['Platforme', /platform|platfrom|pauk|snake|sinoboom|zglobn/],
  ['Viličari', /viličar|paletar|podizni|heli europe/],
  ['Čišćenje', /čistilic|perač/],
  ['Rasvjeta i agregati', /rasv|agregat/],
];
const groupOf = name => { const n = name.toLowerCase(); const g = GROUPS.find(g => g[1].test(n)); return g ? g[0] : 'Ostalo'; };

function segTabs(cur) {
  const t = [['najam', 'Najam'], ['prodaja', 'Prodaja'], ['rabljeni', 'Rabljeni strojevi']];
  return `<div class="seg" role="navigation" aria-label="Vrsta ponude">${t.map(([k, l]) => `<a href="${k}.html"${k === cur ? ' aria-current="page"' : ''}>${l}</a>`).join('')}</div>`;
}

function catalog(kind, o) {
  const items = STROJEVI[kind].map(t => ({ ...t, group: groupOf(t.name) }));
  const groups = [...new Set(items.map(i => i.group))].sort((a, b) => (a === 'Ostalo') - (b === 'Ostalo') || a.localeCompare(b, 'hr'));
  const chips = groups.length > 2
    ? `<div class="chips" role="group" aria-label="Filtriraj po vrsti">
        <button class="chip" type="button" data-group="" aria-pressed="true">Sve</button>
        ${groups.map(g => `<button class="chip" type="button" data-group="${esc(g)}" aria-pressed="false">${esc(g)}</button>`).join('\n        ')}
      </div>` : '';
  const cards = items.map(t => `<a class="m-card" href="${esc(t.href)}" data-name="${esc(t.name)}" data-group="${esc(t.group)}">
        <div class="pic"><span class="tag">${esc(t.group)}</span><img src="${t.img}" alt="" loading="lazy"></div>
        <div class="body"><h3>${esc(t.name)}</h3><span class="more">${o.more} ${ARROW}</span></div>
      </a>`).join('\n      ');

  return `${hero({ ...o.hero, extra: segTabs(kind) })}

<section class="section-sm light">
  <div class="wrap">
    <div class="section-head" style="margin-bottom:28px">
      <div>
        <span class="eyebrow">${o.eyebrow}</span>
        <h2 class="display section-title">${o.heading}</h2>
      </div>
    </div>
    <div class="toolbar">
      <label class="search"><span class="sr-only">Pretraži</span>${SEARCH}<input id="catSearch" type="search" placeholder="${o.placeholder}" autocomplete="off"></label>
      ${chips}
      <span class="count" aria-live="polite"><b id="catCount">${items.length} od ${items.length}</b> ${o.unit}</span>
    </div>
    <div class="catalog-grid" data-catalog>
      ${cards}
    </div>
    <div class="empty" id="catEmpty">
      <p>Nema rezultata za zadanu pretragu.</p>
      <p style="margin-top:14px"><button class="btn btn-dark" type="button" id="catReset">Prikaži sve</button></p>
    </div>
  </div>
</section>

${o.after}

${ctaBand(o.cta[0], o.cta[1], o.cta[2])}`;
}

/* ---------------------------------------------------------------- Novosti */
function novosti() {
  const posts = JSON.parse(read('src/data/novosti.json'));
  const fmt = new Intl.DateTimeFormat('hr-HR', { day: 'numeric', month: 'long', year: 'numeric', timeZone: 'Europe/Zagreb' });
  return posts.map(p => `<article class="post reveal">
      <a class="pic" href="${p.url}" target="_blank" rel="noopener" tabindex="-1" aria-hidden="true">${p.type === 'added_video' ? '<span class="badge">Video</span>' : ''}<img src="${p.img}" alt="" loading="lazy"></a>
      <div class="body">
        <time datetime="${p.date}">${fmt.format(new Date(p.date))}</time>
        <p class="txt">${esc(p.text)}</p>
        <div class="foot"><button type="button" aria-expanded="false">Prikaži više</button><a href="${p.url}" target="_blank" rel="noopener">Otvori na Facebooku ↗</a></div>
      </div>
    </article>`).join('\n    ');
}

/* ---------------------------------------------------------------- Brandovi */
const BRANDS = [
  ['dieci', 'DIECI', 'dieci-logo.jpg'], ['easy-lift', 'EASY LIFT', 'EASYLIFT-logo-300x79.png'], ['faraone-elevah', 'FARAONE ELEVAH', 'FARAONE-LOGO-300x73.jpg'],
  ['haulotte', 'HAULOTTE', 'Haulotte-Logo-300x45.jpg'], ['fiorentini', 'FIORENTINI', 'FIORENTINI-LOGO-300x78.png'], ['almacrawler', 'ALMACRAWLER', 'almacrawlerLogo-300x70.png'],
  ['lcm', 'LCM', 'lcm-logo-300x96.png'], ['twolift', '2LIFT', '2lift-logo.jpg'], ['oil-and-steel', 'OIL & STEEL', 'oil_steel-logo.jpg'],
  ['italtower', 'ITALTOWER', 'italtower-logo-300x90.png'], ['cgm', 'CGM', 'CGM-logo-300x131.jpg'], ['klubb', 'KLUBB', 'klubb-logo.jpg'],
  ['ep-vilicari', 'EP viličari', 'ep-logo.jpg'], ['mitsubishi', 'MITSUBISHI', 'mitubishi-logo.jpg'], ['isoli', 'ISOLI', 'isoli-logo.jpg'], ['d-avino', "D'AVINO", 'davino-logo.jpg'],
];
const brands = () => BRANDS.map(([slug, name, img]) =>
  `<a class="brand" href="https://www.uprent.hr/nasi-brandovi/${slug}"><img src="assets/brand/${img}" alt="" loading="lazy"><span>${esc(name)}</span></a>`).join('\n      ');

/* ---------------------------------------------------------------- Kontakt obrazac
   Bez poslužitelja: site.js otvara e-mail program s ispunjenom porukom (mailto).
   TODO: povezati s postojećim /api/sendEmail. */
function contactForm(to) {
  const f = (name, label, type, extra = '') => `<div class="field"><label for="f-${name}">${label}</label><input id="f-${name}" name="${name}" type="${type}" required ${extra}></div>`;
  return `<form class="form js-mailto" data-to="${to}">
        ${f('name', 'Ime i prezime', 'text', 'autocomplete="name"')}
        ${f('email', 'Email adresa', 'email', 'autocomplete="email"')}
        ${f('company', 'Tvrtka', 'text', 'autocomplete="organization"')}
        ${f('phone', 'Kontakt broj', 'tel', 'autocomplete="tel"')}
        <div class="field full"><label for="f-title">Naslov</label><input id="f-title" name="title" type="text" required></div>
        <div class="field full"><label for="f-message">Poruka</label><textarea id="f-message" name="message" rows="5" required></textarea></div>
        <div class="form-foot full">
          <button class="btn btn-dark" type="submit">Pošalji ${ARROW}</button>
          <span class="form-msg" role="status" aria-live="polite"></span>
        </div>
      </form>`;
}

/* ---------------------------------------------------------------- Stranice */
const fragment = name => read(`src/pages/${name}.html`)
  .replace(/\{\{(arrow|check|download|play)\}\}/g, (_, k) => ICONS[k])
  .replace(/\{\{form:([^}]+)\}\}/g, (_, to) => contactForm(to))
  .replace('{{novosti}}', () => novosti())
  .replace('{{brands}}', () => brands());

const perk = (title, text) => `<div class="step"><div class="ico">${CHECK}</div><h3>${title}</h3><p>${text}</p></div>`;

const PAGES = [
  { key: 'index', title: 'Najam i prodaja strojeva i radnih platformi - Up Rent',
    desc: 'Tvrtka Uprent bavi se najmom, transportom i servisiranjem strojeva za rad na visini',
    main: () => fragment('index') },

  { key: 'najam', title: 'Najam strojeva - Up Rent',
    desc: 'Najam radnih platformi, teleskopskih viličara, auto košara, agregata i rasvjetnih tornjeva.',
    main: () => catalog('najam', {
      hero: { img: 'assets/slides/slide-03.jpg', eyebrow: 'Strojevi', h1: 'Najam strojeva', crumb: 'Najam',
        lead: 'Radne platforme, teleskopski viličari, auto košare i ostala oprema za siguran rad na visini — uz dostavu na Vašu lokaciju i servisnu podršku 24 sata dnevno.' },
      eyebrow: 'Flota za najam', heading: 'Odaberite kategoriju', placeholder: 'Pretraži kategorije…', unit: 'kategorija', more: 'Pogledaj strojeve',
      after: `<section class="section-sm tight-top light">
  <div class="wrap">
    <div class="section-head reveal" style="margin-bottom:28px">
      <div><span class="eyebrow">Kako do stroja</span><h2 class="display section-title">Najam u četiri koraka</h2></div>
    </div>
    <div class="steps numbered reveal">
      <div class="step"><h3>Odaberite stroj</h3><p>Pregledajte kategorije ili nas nazovite — pomoći ćemo Vam pronaći pravu platformu za posao.</p></div>
      <div class="step"><h3>Zatražite ponudu</h3><p>Javite se na <a class="link" href="tel:+38512455000">+385 1 24 55 000</a> ili <a class="link" href="mailto:najam@uprent.hr">najam@uprent.hr</a>.</p></div>
      <div class="step"><h3>Dostava na lokaciju</h3><p>Isporuka i odvoz uvijek u dogovorenom vremenu, vlastitom <a class="link" href="transporti.html">kamionskom flotom</a>.</p></div>
      <div class="step"><h3>Podrška tijekom najma</h3><p>Naš servisni tim stoji Vam na usluzi s 4 servisna vozila, 24 sata dnevno.</p></div>
    </div>
  </div>
</section>`,
      cta: ['Niste sigurni koji stroj trebate?', 'Naše bogato iskustvo omogućava stručne savjete i rješenja prilagođena Vašoj potrebi.', 'najam@uprent.hr'] }) },

  { key: 'prodaja', title: 'Prodaja strojeva - Up Rent',
    desc: 'Prodaja novih radnih platformi, viličara, auto košara, čistilica i priključaka vodećih svjetskih proizvođača.',
    main: () => catalog('prodaja', {
      hero: { img: 'assets/slides/slide-02.jpg', eyebrow: 'Strojevi', h1: 'Prodaja strojeva', crumb: 'Prodaja',
        lead: 'Novi strojevi proizvođača koje zastupamo — od radnih platformi i teleskopskih viličara do industrijskih čistilica i priključaka.' },
      eyebrow: 'Prodajni program', heading: 'Odaberite proizvođača', placeholder: 'Pretraži proizvođače…', unit: 'proizvođača', more: 'Pogledaj ponudu',
      after: `<section class="section-sm tight-top light">
  <div class="wrap">
    <div class="section-head reveal" style="margin-bottom:28px">
      <div><span class="eyebrow">Zašto kupiti kod nas</span><h2 class="display section-title">Više od prodaje</h2></div>
      <a class="btn btn-dark" href="brandovi.html">Naši brandovi ${ARROW}</a>
    </div>
    <div class="steps three reveal">
      ${perk('Ekskluzivni zastupnik', 'Zastupamo brandove koji su već desetljećima u samom svjetskom vrhu svojih branši.')}
      ${perk('Vlastiti servis', 'Servisni tim vodi računa da su strojevi konstantno u tip top stanju — s 4 servisna vozila, 24 sata dnevno.')}
      ${perk('Edukacija korisnika', 'Educiramo klijente za siguran rad na visini. <a class="link" href="edukacija.html">Više o edukaciji</a>')}
    </div>
  </div>
</section>`,
      cta: ['Zatražite ponudu za novi stroj', 'Javite nam koji stroj trebate, a naš prodajni tim pripremit će ponudu.', 'prodaja@uprent.hr'] }) },

  { key: 'rabljeni', title: 'Rabljeni strojevi - Up Rent',
    desc: 'Prodaja rabljenih radnih platformi, viličara, auto košara i ostalih strojeva iz Up Rent flote.',
    main: () => catalog('rabljeni', {
      hero: { img: 'assets/bg/partners.jpg', eyebrow: 'Strojevi', h1: 'Rabljeni strojevi',
        lead: 'Polovni strojevi iz naše ponude — platforme, viličari, auto košare, rovokopači i ostala oprema.' },
      eyebrow: 'Polovni strojevi', heading: 'Odaberite kategoriju', placeholder: 'Pretraži kategorije…', unit: 'kategorija', more: 'Pogledaj strojeve',
      after: `<section class="section-sm tight-top light">
  <div class="wrap">
    <div class="split even stretch reveal">
      <div class="card">
        <img src="assets/njuskalo.png" alt="Njuškalo" width="205" height="67" loading="lazy" style="height:40px;width:auto;margin-bottom:20px">
        <h2>Njuškalo trgovina</h2>
        <p class="muted" style="margin:10px 0 22px">Posjetite našu Njuškalo trgovinu s aktualnim oglasima polovnih strojeva.</p>
        <a class="btn btn-dark" href="https://www.njuskalo.hr/tvrtka/up-rent" target="_blank" rel="noopener">Otvori trgovinu ${ARROW}</a>
      </div>
      <div class="card">
        <span class="eyebrow" style="margin-bottom:14px">Međunarodni oglasnik</span>
        <h2>Up Rent na Mascusu</h2>
        <p class="muted" style="margin:10px 0 22px">Naši rabljeni strojevi oglašeni su i na portalu Mascus.</p>
        <a class="btn btn-dark" href="https://www.mascus.hr/up-rent-d-o-o/df454e2b,companycard.html" target="_blank" rel="noopener">Otvori Mascus ${ARROW}</a>
      </div>
    </div>
  </div>
</section>`,
      cta: ['Tražite određeni rabljeni stroj?', 'Javite nam što trebate — provjerit ćemo što je trenutno dostupno.', 'prodaja@uprent.hr'] }) },

  { key: 'brandovi', title: 'Naši brandovi - Up Rent', desc: 'Brandovi koje Up Rent zastupa u Hrvatskoj.', main: () => fragment('brandovi') },
  { key: 'edukacija', title: 'Edukacija - Up Rent', desc: 'Edukacija za rad na visini', main: () => fragment('edukacija') },
  { key: 'transporti', title: 'Transporti - Up Rent', desc: 'Transporti radnih strojeva i platformi po dogovorenoj cijeni i u dogovorenom vremenu.', main: () => fragment('transporti') },
  { key: 'click2rent', title: 'Click2Rent programsko rješenje - Up Rent', desc: 'Programsko rješenje Click2Rent za upravljanje najmom i prodajom', main: () => fragment('click2rent') },
  { key: 'o-nama', title: 'O nama - Up Rent', desc: 'Saznajte više o tvrtki Up Rent d.o.o.', main: () => fragment('o-nama') },
  { key: 'novosti', title: 'Novosti - Up Rent', desc: 'Novosti iz tvrtke Up Rent.', main: () => fragment('novosti') },
  { key: 'dokumenti', title: 'Dokumenti - Up Rent', desc: 'Dokumenti za preuzimanje', main: () => fragment('dokumenti') },
  { key: 'eu-projekti', title: 'EU projekti - Up Rent', desc: 'Razvoj sustava temeljenog na AI modelima za autonomni pregled te detekciju štete i promjena na radnim strojevima', main: () => fragment('eu-projekti') },
  { key: 'kontakt', title: 'Kontakt - Up Rent', desc: 'Kontaktirajte Up Rent — najam, prodaja, servis i transport strojeva.', main: () => fragment('kontakt') },
];

/* Zaglavlja tekstualnih stranica: <!-- hero: {...json...} --> na vrhu fragmenta */
for (const p of PAGES) {
  let main = p.main();
  main = main.replace(/<!--\s*hero:\s*([\s\S]*?)-->/, (_, json) => hero(JSON.parse(json)));
  fs.writeFileSync(path.join(ROOT, p.key + '.html'), layout(p, main));
  console.log('✓', p.key + '.html');
}
