/* Qoot landing page — the only script.
   0. Header: hides on the way down, returns on the way up; on phones the links open from a menu button.
   1. Hero: the photo reel moves through the Q window as the hero is scrolled (on phones it advances by itself).
   2. Entrances: each section's parts arrive once, in reading order.
   3. How we work: the step cards stack as you scroll; the card underneath shrinks and darkens.
      Who we feed: pointing at a city on the map lights up its road from Benghazi.
   4. FAQ answers open and close smoothly.
   5. The quote form becomes a pre-filled email.
   6. ?review in the URL outlines copy that still needs the client's approval.
   Everything is readable without this file: hidden start states only apply under html.js. */
(function () {
  "use strict";
  var doc = document.documentElement;
  window.__qoot = true;
  doc.classList.add("js");
  if (/[?&]review\b/.test(location.search)) doc.classList.add("review");
  var reduce = window.matchMedia("(prefers-reduced-motion: reduce)");
  var phone = window.matchMedia("(max-width: 720px)");
  var fine = window.matchMedia("(hover: hover) and (pointer: fine)");
  var $ = function (s, r) { return (r || document).querySelector(s); };
  var $$ = function (s, r) { return Array.prototype.slice.call((r || document).querySelectorAll(s)); };

  /* 0. Header ---------------------------------------------------------------
     Once the top of the page has gone by, the header waits above the screen.
     Scrolling up brings it back so every section is one tap away; scrolling down puts it away.
     On phones and tablets the links live in a menu that drops from the header. */
  var header = $(".site-header");
  if (header) {
    var menuBtn = $(".menu-btn", header);
    var nav = $(".site-nav", header);
    var wide = window.matchMedia("(min-width: 1025px)");
    var lastY = window.scrollY || 0, openY = 0, hTick = false;
    var isOpen = function () { return header.classList.contains("menu-open"); };
    var setMenu = function (open, returnFocus) {
      if (!menuBtn || open === isOpen()) return;
      header.classList.toggle("menu-open", open);
      menuBtn.setAttribute("aria-expanded", open ? "true" : "false");
      var label = $(".menu-txt", menuBtn);
      if (label) label.textContent = open ? menuBtn.getAttribute("data-close") : menuBtn.getAttribute("data-open");
      if (open) {
        openY = window.scrollY || 0;
        var first = nav && $("a", nav);
        if (first) first.focus({ preventScroll: true });
      } else if (returnFocus) {
        menuBtn.focus({ preventScroll: true });
      }
    };
    var headerUpdate = function () {
      hTick = false;
      var y = window.scrollY || 0;
      if (isOpen() && Math.abs(y - openY) > 80) setMenu(false);
      if (y <= 2) {
        header.classList.remove("is-stuck", "is-shown", "is-ready");
        lastY = y;
        return;
      }
      if (!header.classList.contains("is-stuck")) {
        /* the header is still on the page itself: let it scroll away, then wait above the screen */
        if (y > header.offsetHeight + 40) {
          header.classList.add("is-stuck");
          void header.offsetWidth;
          header.classList.add("is-ready");
        }
        lastY = y;
        return;
      }
      var d = y - lastY;
      if (Math.abs(d) < 8) return;
      if (d < 0) header.classList.add("is-shown");
      else if (!isOpen()) header.classList.remove("is-shown");
      lastY = y;
    };
    var onHeader = function () { if (!hTick) { hTick = true; requestAnimationFrame(headerUpdate); } };
    window.addEventListener("scroll", onHeader, { passive: true });
    headerUpdate();

    if (menuBtn && nav) {
      menuBtn.addEventListener("click", function () { setMenu(!isOpen()); });
      nav.addEventListener("click", function (e) { if (e.target.closest("a")) setMenu(false); });
      document.addEventListener("click", function (e) { if (isOpen() && !header.contains(e.target)) setMenu(false); });
      document.addEventListener("keydown", function (e) { if (e.key === "Escape" && isOpen()) setMenu(false, true); });
      var closeWide = function () { if (wide.matches) setMenu(false); };
      if (wide.addEventListener) wide.addEventListener("change", closeWide);
    }
  }

  /* 1. Hero reel ---------------------------------------------------------- */
  var hero = $(".hero");
  var reel = hero && $(".reel", hero);
  var qwin = hero && $(".qwin", hero);
  var cap = hero && $("[data-cap]", hero);
  if (hero && reel) {
    var frames = $$("img", reel);
    var steps = frames.length - 1;
    var current = 0;
    var ticking = false;
    var ease = function (t) { return t < 0 ? 0 : t > 1 ? 1 : t * t * (3 - 2 * t); };
    var setCaption = function (idx) {
      if (idx === current || !cap) return;
      current = idx;
      var text = frames[idx].getAttribute("data-caption") || "";
      if (reduce.matches || !cap.animate) { cap.textContent = text; return; }
      var x = phone.matches ? "" : "translateX(-50%) ";
      cap.animate([{ opacity: 1, transform: x + "translateY(0)" }, { opacity: 0, transform: x + "translateY(-8px)" }], { duration: 160, easing: "cubic-bezier(.22,1,.36,1)", fill: "forwards" }).onfinish = function () {
        cap.textContent = text;
        cap.animate([{ opacity: 0, transform: x + "translateY(8px)" }, { opacity: 1, transform: x + "translateY(0)" }], { duration: 380, easing: "cubic-bezier(.22,1,.36,1)", fill: "forwards" });
      };
    };
    var place = function (y) {
      reel.style.transform = "translate3d(0," + (-y * 100) + "%,0)";
      /* frames outside the Q are hidden, so nothing sits behind the headline */
      for (var k = 0; k < frames.length; k++) frames[k].style.visibility = Math.abs(k - y) < 1 ? "visible" : "hidden";
    };
    var update = function () {
      ticking = false;
      if (reduce.matches || phone.matches) return;
      var r = hero.getBoundingClientRect();
      var travel = hero.offsetHeight - window.innerHeight;
      var p = travel > 0 ? Math.min(Math.max(-r.top / travel, 0), 1) : 0;
      var pos = p * steps;
      var base = Math.min(Math.floor(pos), steps - 1);
      /* each photo rests, then moves on: travel happens in the middle 50% of each step */
      var t = ease((pos - base - 0.25) / 0.5);
      var y = base + t;
      place(y);
      /* the leaf sways a little with every change of picture, and the window grows a touch */
      qwin.style.setProperty("--lr", (Math.sin(t * Math.PI) * -9).toFixed(2) + "deg");
      qwin.style.setProperty("--qs", (1 + p * 0.06).toFixed(4));
      /* the two halves of the name make room as the window grows */
      hero.style.setProperty("--hx", (p * 28).toFixed(1) + "px");
      setCaption(Math.round(y));
    };
    var onScroll = function () { if (!ticking) { ticking = true; requestAnimationFrame(update); } };
    /* desktop: the picture in the window shifts a little against the pointer, like looking through glass */
    var stage = $(".hero-stage", hero);
    stage.addEventListener("pointermove", function (e) {
      if (!fine.matches || reduce.matches) return;
      var r = stage.getBoundingClientRect();
      var dx = (e.clientX - r.left) / r.width - 0.5, dy = (e.clientY - r.top) / r.height - 0.5;
      reel.style.setProperty("--px", (-dx * 4).toFixed(2) + "%");
      reel.style.setProperty("--py", (-dy * 4).toFixed(2) + "%");
    });
    stage.addEventListener("pointerleave", function () { reel.style.setProperty("--px", "0%"); reel.style.setProperty("--py", "0%"); });
    window.addEventListener("scroll", onScroll, { passive: true });
    window.addEventListener("resize", onScroll, { passive: true });
    update();

    /* phones: the hero is not held, so the reel advances on its own while it is on screen.
       A copy of the first photograph sits after the last, so the loop never rewinds. */
    var first = reel.children[0].cloneNode(true);
    first.setAttribute("aria-hidden", "true");
    first.style.top = frames.length * 100 + "%";
    var firstImg = first.querySelector("img");
    firstImg.alt = ""; firstImg.removeAttribute("fetchpriority"); firstImg.style.visibility = "hidden";
    reel.appendChild(first);
    var timer = null, auto = 0, visible = true;
    var tick = function () {
      auto += 1;
      frames.forEach(function (f) { f.style.visibility = "visible"; });
      firstImg.style.visibility = "visible";
      reel.style.transition = "transform 1s cubic-bezier(.76,0,.24,1)";
      reel.style.transform = "translate3d(0," + (-auto * 100) + "%,0)";
      qwin.style.setProperty("--lr", "-9deg");
      setTimeout(function () { qwin.style.setProperty("--lr", "0deg"); }, 520);
      setCaption(auto % frames.length);
      if (auto === frames.length) {
        setTimeout(function () {
          reel.style.transition = "none";
          auto = 0;
          reel.style.transform = "translate3d(0,0,0)";
        }, 1050);
      }
    };
    var runAuto = function () {
      clearInterval(timer); timer = null;
      if (phone.matches && !reduce.matches) {
        frames.forEach(function (f) { f.style.visibility = "visible"; });
        if (visible && !document.hidden) timer = setInterval(tick, 3400);
      } else {
        reel.style.transition = "";
        firstImg.style.visibility = "hidden";
        update();
      }
    };
    if ("IntersectionObserver" in window) {
      new IntersectionObserver(function (e) { visible = e[0].isIntersecting; runAuto(); }).observe(qwin);
    }
    document.addEventListener("visibilitychange", runAuto);
    if (phone.addEventListener) phone.addEventListener("change", runAuto);
    runAuto();
  }

  /* 2. Entrances --------------------------------------------------------- */
  var groups = $$("[data-reveal]");
  if ("IntersectionObserver" in window && !reduce.matches) {
    var io = new IntersectionObserver(function (entries) {
      entries.forEach(function (e) {
        if (!e.isIntersecting) return;
        var el = e.target;
        io.unobserve(el);
        el.classList.add("is-in");
        setTimeout(function () { el.classList.add("is-done"); }, 1900);
      });
    }, { rootMargin: "0px 0px -6% 0px", threshold: 0 });
    groups.forEach(function (g) { io.observe(g); });
  } else {
    groups.forEach(function (g) { g.classList.add("is-in", "is-done"); });
  }

  /* 3. How we work: the cards stack as you scroll ----------------------------
     Each card sticks; as the next one slides over it, it shrinks a little and darkens.
     The photo in each card settles from a slight zoom as the card rises into place. */
  var cards = $$(".stack .card");
  if (cards.length) {
    var tops = [];
    var measure = function () { tops = cards.map(function (c) { return parseFloat(getComputedStyle(c).top) || 0; }); };
    var sTick = false;
    var stack = function () {
      sTick = false;
      if (reduce.matches) return;
      var vh = window.innerHeight;
      var r = cards.map(function (c) { return c.getBoundingClientRect().top; });
      cards.forEach(function (c, i) {
        var bottom = r[i] + c.offsetHeight;
        var cover = 0;
        for (var j = i + 1; j < cards.length; j++) {
          var p = (bottom - r[j]) / c.offsetHeight;
          cover += p < 0 ? 0 : p > 1 ? 1 : p;
        }
        c.style.setProperty("--s", (1 - Math.min(cover, 3) * 0.05).toFixed(4));
        c.style.setProperty("--dim", Math.min(cover * 0.28, 0.6).toFixed(3));
        var e = 1 - (r[i] - tops[i]) / vh;
        e = e < 0 ? 0 : e > 1 ? 1 : e;
        c.style.setProperty("--z", (1.2 - e * 0.2).toFixed(4));
      });
    };
    var onStack = function () { if (!sTick) { sTick = true; requestAnimationFrame(stack); } };
    measure();
    window.addEventListener("scroll", onStack, { passive: true });
    window.addEventListener("resize", function () { measure(); onStack(); }, { passive: true });
    stack();
  }

  /* Who we feed: pointing at a city lights up its road from Benghazi */
  $$(".map .city").forEach(function (city) {
    var road = $('.map .route[data-c="' + city.getAttribute("data-c") + '"]');
    if (!road) return;
    var on = function () { city.classList.add("is-hot"); road.classList.add("is-hot"); };
    var off = function () { city.classList.remove("is-hot"); road.classList.remove("is-hot"); };
    city.addEventListener("pointerenter", on);
    city.addEventListener("pointerleave", off);
  });

  /* The quote coupon: what is typed in "What do you need?" is listed on the stub,
     one item per comma or line */
  var form = document.getElementById("quote-form");
  var need = form ? form.elements.need : null;
  var sumList = $(".os-list");
  var emptyText = sumList ? sumList.getAttribute("data-empty") : "";
  var items = function () {
    return (need && need.value ? need.value : "").split(/[,\u060C;\u061B\n]+/)
      .map(function (s) { return s.trim(); }).filter(Boolean);
  };
  var sync = function () {
    if (!sumList) return;
    var list = items();
    sumList.innerHTML = "";
    if (!list.length) {
      var li = document.createElement("li"); li.className = "os-empty"; li.textContent = emptyText; sumList.appendChild(li);
    }
    list.slice(0, 8).forEach(function (s) {
      var li = document.createElement("li");
      li.textContent = s.length > 32 ? s.slice(0, 31) + "…" : s;
      sumList.appendChild(li);
    });
  };
  if (need) need.addEventListener("input", function () {
    sync();
    var st = form.querySelector(".quote-status");
    if (st && st.textContent === form.dataset.msgNone && items().length) st.textContent = "";
  });
  sync();

  /* 4. FAQ: smooth open and close ---------------------------------------- */
  $$(".qa").forEach(function (d) {
    var s = $("summary", d), a = $(".qa-a", d);
    if (!s || !a || !a.animate) return;
    var anim = null;
    s.addEventListener("click", function (e) {
      if (reduce.matches) return;
      e.preventDefault();
      if (anim) anim.cancel();
      if (d.open) {
        var h = a.offsetHeight;
        d.classList.add("closing");
        anim = a.animate([{ height: h + "px", opacity: 1 }, { height: "0px", opacity: 0 }], { duration: 320, easing: "cubic-bezier(.76,0,.24,1)" });
        anim.onfinish = function () { d.open = false; d.classList.remove("closing"); anim = null; };
      } else {
        d.open = true;
        var full = a.offsetHeight;
        anim = a.animate([{ height: "0px", opacity: 0 }, { height: full + "px", opacity: 1 }], { duration: 460, easing: "cubic-bezier(.22,1,.36,1)" });
        anim.onfinish = function () { anim = null; };
      }
    });
  });

  /* 5. Quote form -> email ----------------------------------------------- */
  if (form) {
    form.addEventListener("submit", function (e) {
      e.preventDefault();
      var t = form.dataset;
      var picked = items();
      var val = function (n) { var el = form.elements[n]; return el && el.value ? el.value.trim() : ""; };
      var status = form.querySelector(".quote-status");
      if (!picked.length) {
        if (status) status.textContent = t.msgNone;
        if (need) need.focus();
        return;
      }
      var org = val("organisation");
      var lines = [
        t.lGreeting, "",
        t.lNeed + " " + picked.join(", "),
        t.lPeople + " " + (val("people") || "-"),
        t.lOften + " " + (val("often") || "-"),
        t.lOrg + " " + (org || "-"),
        t.lPlace + " " + (val("location") || "-"),
        "", t.lThanks
      ];
      var subject = t.subject + (org ? " — " + org : "");
      if (status) status.textContent = t.msgOpening;
      window.location.href = "mailto:" + form.dataset.to + "?subject=" + encodeURIComponent(subject) + "&body=" + encodeURIComponent(lines.join("\n"));
    });
  }
})();

/* Location: open or closed right now, by the clock in Benghazi ---------------
   Hours and words come from the data attributes on [data-hours]; without script the
   hours list alone is shown. */
(function () {
  var box = document.querySelector("[data-hours]");
  if (!box || !window.Intl) return;
  var fmt;
  try {
    fmt = new Intl.DateTimeFormat("en-GB", { timeZone: box.dataset.tz, weekday: "short", hour: "2-digit", minute: "2-digit", hourCycle: "h23" });
  } catch (e) { return; }
  var now = box.querySelector(".visit-now"), clock = box.querySelector(".visit-clock");
  var rows = document.querySelectorAll(".visit-hours [data-days]");
  var open = box.dataset.openDays.split(",").map(Number);
  var names = box.dataset.days.split(",");
  var from = box.dataset.from, to = box.dataset.to;
  var WD = { Sun: 0, Mon: 1, Tue: 2, Wed: 3, Thu: 4, Fri: 5, Sat: 6 };
  function mins(t) { var p = t.split(":"); return +p[0] * 60 + +p[1]; }
  function fill(s, o) { return s.replace(/\{(\w+)\}/g, function (_, k) { return o[k]; }); }
  function tick() {
    var p = {};
    fmt.formatToParts(new Date()).forEach(function (x) { p[x.type] = x.value; });
    var day = WD[p.weekday], m = +p.hour * 60 + +p.minute;
    var openDay = open.indexOf(day) > -1;
    var isOpen = openDay && m >= mins(from) && m < mins(to);
    var msg;
    if (isOpen) msg = fill(box.dataset.lOpen, { to: to });
    else if (openDay && m < mins(from)) msg = fill(box.dataset.lToday, { from: from });
    else {
      var n = 1;
      while (n < 7 && open.indexOf((day + n) % 7) < 0) n++;
      msg = n === 1 ? fill(box.dataset.lTomorrow, { from: from })
                    : fill(box.dataset.lDay, { from: from, day: names[(day + n) % 7] });
    }
    if (now.textContent !== msg) now.textContent = msg;
    now.classList.toggle("is-open", isOpen);
    clock.textContent = fill(box.dataset.lClock, { time: p.hour + ":" + p.minute });
    rows.forEach(function (r) { r.classList.toggle("is-today", r.dataset.days.split(",").map(Number).indexOf(day) > -1); });
    box.hidden = false;
  }
  tick();
  setInterval(tick, 30000);
})();
