# Qoot landing page: www.qoot.com

This is the static landing page for **Qoot Catering & Supplies (قوت للتعهدات والتموين)** in Benghazi, Libya.

- **Arabic** is at `/` and is the default (RTL).
- **English** is at `/en/`.
- **No build step or framework:** it's plain HTML, CSS and about 3 KB of JavaScript.

## What's in the folder

```
index.html              Arabic page (lang="ar" dir="rtl")
en/index.html           English page
styles.css              All styles; the hero/header part is also inlined in each page
script.js               Hero photo reel, quote form → email, ?review mode
robots.txt, sitemap.xml Both URLs with hreflang alternates
site.webmanifest        App name, colours, icons
favicon.ico / .svg      The Q icon (the SVG switches to cream in dark mode)
apple-touch-icon.png    180×180
_headers                Cache + security headers (Netlify / Cloudflare Pages)
assets/fonts/           Poppins 300/400/700 (Latin) + Readex Pro (Arabic, variable), self-hosted woff2
assets/img/             qoot-*.avif / .webp in several widths, plus qoot-og-image.jpg (1200×630)
assets/logo/            Logos traced to SVG, the Q mask, the leaf, the line illustrations, PNG app icons
COPY-REVIEW.md          Every piece of copy I drafted, for you to approve or correct
```

## October 2026 redesign: what changed

The idea: the Q window from the logo is the frame every picture is seen through, and the catalogue is the start of the order.

- **Hero:** same Q-window reel, now with an entrance (the name rises, the window opens, the leaf drops in), a slightly larger window that grows as you scroll, the two halves of the name drifting apart, a picture that shifts against the pointer, and a "See what we supply" link. On phones the hero is no longer held; the reel advances by itself every few seconds.
- **What we supply:** the table is now a list of eight categories beside one Q window. The row in view (or under the pointer) lights up and its picture appears in the window. The **+** button adds a category to the quote: a small pill follows the visitor and the category arrives already ticked on the quote form.
- **How we work:** four tiles that grow in size (source → store → cook → deliver) instead of four full-screen photos.
- **Who we feed:** a leaf-green panel with the tomatoes photo in the Q shape.
- **FAQ:** wide heading, indented questions that open smoothly; email and phone beside them.
- **Request a quote:** a tear-off coupon (stub + form) instead of a card over a full-screen photo.
- **Footer:** much smaller. The line drawings drift slowly across a short band; the two wordmarks, email, phone, city and one legal line. Instagram, Facebook and the section links were removed.

New files:

- `assets/logo/qoot-draw-{chicken,fish,milk,water}.svg` single drawings (phone thumbnails). Chicken and fish are cut from the brand illustrations; milk and water were drawn to match them, so ask ZDS Agency for official versions if they have them.
- `assets/logo/qoot-pattern-{chicken,fish,milk,water}.svg` wrapping-paper patterns shown in the Q window for categories without a photo.
- `assets/logo/qoot-draw-strip.svg` the footer band. `assets/logo/qoot-q-leaf-green.svg` the leaf in deep green for the leaf panel.
- `assets/img/qoot-delivery-truck-tile-*.avif/.webp` the truck photo with its background extended to a tall frame.

`assets/logo/qoot-illustrations.svg` is no longer used by the pages.

The critical CSS (between `critical:start` and `critical:end` in `styles.css`) is still copied, minified, into the `<style>` tag of both pages; in `en/index.html` its `url(assets/...)` paths become `url(../assets/...)`. If you change that part of `styles.css`, update both `<style>` tags too.

Motion respects "reduce motion": nothing moves and every section is visible. With JavaScript off, everything is visible and the hero shows its first photo.

## 19 October 2026 update: what changed

- **What we supply:** the "+ add to quote" buttons, the hint line and the floating order pill are gone. The eight categories are now a shelf of coloured cards, each in the colour of what it holds (leaf for produce, coral for meat, sea blue for fish, and so on), with a line drawing, the delivery rhythm and the products. On phones each card shows only the drawing, the name, a three-word summary and the delivery rhythm.
- **How we work:** four coloured cards (Source → Store → Cook → Deliver) that stack as you scroll. Each card sticks, the next one slides over it, the card underneath shrinks and darkens, and each photo settles from a slight zoom. Same effect on phone and desktop; texts are one short sentence each. With "reduce motion" on, the cards still stack but nothing scales.
- **Who we feed:** now "Who we feed across Libya". Six cards with an icon and a short label (two per row on phones, alternating green and cream), and a map of Libya with the Q on Benghazi and a road drawn out to each city when the section comes into view. Pointing at a city lights up its road. "What the name means" is hidden on phones.
- **Benghazi → all of Libya:** the hero sentence, the delivery fact, the "Deliver" step and the FAQ answer (and its JSON-LD copy) now say you deliver across Libya from Benghazi.
- **Header:** once you scroll past the top it tucks away above the screen, and slides back in the moment you scroll up, so every section is one tap away. At the very top it sits on the page as before.
- **Phone and tablet menu:** below 1025px the header's "Request a quote" button is replaced by a "Menu" button (القائمة). It opens a green panel with the four section links and the "Request a quote" button. It closes on a link, on Escape, on a tap outside, or when you scroll away. Without JavaScript the old "Request a quote" button shows instead. The header styles are part of the critical CSS, so both `<style>` tags were regenerated (minified with csso).
- **New colours** (only on the cards, defined at the top of the "Secondary food tones" block in `styles.css`): coral `#F4A68C`, sea `#8EC5E0`, milk `#FFF7EA`, saffron `#F4B04C`, wheat `#F4CF72`, aqua `#CDECF2`, kraft `#D9B485`.

New files: `assets/logo/qoot-draw-tomato.svg`, `qoot-draw-sack.svg` and `qoot-draw-bag.svg` (drawn to match the brand's line drawings), and `qoot-draw-croissant.svg` (cut from the footer strip). The six small icons in "Who we feed" are inline in the HTML.

No longer used by the pages: `assets/img/qoot-delivery-truck-tile-*` (the Deliver card uses the wide truck photo again), and the four `qoot-pattern-*.svg` files.

### The map

The map in "Who we feed" is inline SVG in both pages. Each city is one `<path class="route" data-c="…">` (the road) plus one `<g class="city" data-c="…">` (the dot and its name).

- **To remove a city:** delete both elements with that `data-c` from `index.html` and `en/index.html`, and remove its name from the map's `<title>`.
- **To add a city:** the point has to be projected onto the same map, so ask for it to be added rather than guessing coordinates.
- Labels marked `minor` are hidden on phones so the map stays readable.

## Deploy

The page works on any static host. Pick one:

### Netlify / Cloudflare Pages

1. Drag the folder into the dashboard, or connect a Git repo with no build command and `/` as the publish directory.
2. Add the custom domain `www.qoot.com`.
3. Redirect `qoot.com` → `https://www.qoot.com` (301).
4. Turn on HTTPS. `_headers` is picked up automatically.

### Any other host (nginx, Apache, cPanel)

1. Upload everything to the web root.
2. Enable gzip or brotli for `text/html`, `text/css`, `application/javascript`, `image/svg+xml` and `application/manifest+json`.
3. Cache `/assets/` for a year (`Cache-Control: public, max-age=31536000, immutable`). Cache HTML for 0 seconds with revalidation.
4. Make sure `.webmanifest` is served as `application/manifest+json` and `.avif` as `image/avif`.
5. Force HTTPS and one hostname (`www`) with 301 redirects.

nginx example:

```nginx
gzip on; gzip_types text/css application/javascript image/svg+xml application/manifest+json;
location /assets/ { add_header Cache-Control "public, max-age=31536000, immutable"; }
types { application/manifest+json webmanifest; image/avif avif; }
```

### Changing images or icons

If you swap an image or icon for a new version with the same file name, rename it (for example `-v2`). Files in `/assets/` are cached for a year, so browsers won't fetch a replacement under the old name.

## Before launch: must fix

These are placeholders from the brand book or my assumptions. They're marked in the HTML with `data-review`.

**Preview them:** open either page with `?review` at the end of the URL (for example `/en/?review`). Every drafted line is outlined in pink.

- [ ] **Phone number:** `+218 123 456 789` is the brand book's placeholder.
  - In `index.html` and `en/index.html`, search for `123456789` and `123 456 789`. The spaces are non-breaking (`&nbsp;`), so search the number with and without spaces.
  - Also update `"telephone"` in the JSON-LD block.
- [ ] **Street address:** only "Benghazi, Libya" is used.
  - Add the full address to the FAQ facts, the footer and the `LocalBusiness` JSON-LD (`streetAddress`, `postalCode`).
- [ ] **Instagram and Facebook URLs:** `instagram.com/qoot.ly` and `facebook.com/qoot.ly` are guesses.
  - Fix them in the footer and in the JSON-LD `sameAs`.
- [ ] **Opening hours:** "Saturday to Thursday, 8:00–17:00" is assumed.
  - Fix it in the "Who we feed" facts and in `openingHoursSpecification`.
- [ ] **Product list, delivery days, FAQ answers, service claims:** all drafted by me. See `COPY-REVIEW.md`.
- [ ] **Vector logo:** the brand PDF only contains flattened images, so the logos were traced to SVG.
  - Replace `assets/logo/qoot-wordmark-*.svg`, `qoot-icon-*.svg`, `qoot-q-mask.svg` and `qoot-q-leaf.svg` with the originals from ZDS Agency.
  - The Q mask is also inlined as a data URI in `styles.css` (search `qoot-q-mask` / `mask:url(`). Update it there too.
- [ ] **Photos:** all photos come from the brand book and are agency mockup renders, not Qoot's own facilities.
  - Replace them with real photos of the warehouse, kitchen, trucks and team as soon as you have them.
  - Keep the file names (`qoot-*.webp/.avif`) and the same widths. The `sharp` commands are in the next section.

To edit the copy, change the HTML directly. The two pages share the same structure, so make the same change in both languages.

## Regenerating images

Install the image tool once with `npm i sharp`. Then create each width in both formats, for example:

```bash
npx sharp -i photo.jpg -o assets/img/qoot-warehouse-stock-1600.avif resize 1600 -- avif --quality 50
npx sharp -i photo.jpg -o assets/img/qoot-warehouse-stock-1600.webp resize 1600 -- webp --quality 74
```

Keep the widths listed in each `srcset`. Update `width`/`height` on the `<img>` if the aspect ratio changes.

## SEO: what's already done

- One `<h1>` per page with the brand name and keyword, then a logical h2 → h3 order.
- Titles: EN 56 characters, AR 52 characters, both starting with "Qoot".
- Meta descriptions: EN 157 characters, AR 155 characters.
- Each page has:
  - canonical, robots and theme-color tags, and `hreflang` (ar, en, x-default)
  - Open Graph and Twitter Card tags with a 1200×630 share image
- JSON-LD on each page: `Organization`, `LocalBusiness`, `WebSite`, `WebPage`, `FAQPage` (in the page's language).
- `robots.txt`, `sitemap.xml` with hreflang alternates, favicons and the manifest.
- All text is real HTML. Every image has alt text and a descriptive file name.
- Images are AVIF/WebP with width and height set. The first hero photo is preloaded, and everything else loads lazily.
- Fonts are self-hosted with `font-display: swap`. Critical CSS is inline and the stylesheet loads without blocking.
- Lighthouse (local server, mobile):
  - Arabic: Performance 99 · Accessibility 100 · Best Practices 100 · SEO 100
  - English: Performance 99 · Accessibility 100 · Best Practices 100 · SEO 100
- Desktop scored 100 on all four categories on both pages.

## SEO checklist after launch

- [ ] Verify `www.qoot.com` in **Google Search Console** and **Bing Webmaster Tools**, then submit `https://www.qoot.com/sitemap.xml`.
- [ ] In Search Console, use **URL Inspection** on `/` and `/en/` and request indexing.
- [ ] Run both URLs through the **Rich Results Test** and fix anything flagged in the FAQ/Organization markup.
  - Do this again once the real phone number and address are in.
- [ ] Create or claim the **Google Business Profile** for Qoot in Benghazi.
  - Use exactly the same name, address and phone as the site (NAP consistency), and link to `https://www.qoot.com/`.
- [ ] Put the real Instagram and Facebook URLs in `sameAs`. Link back to the site from both profiles' bios.
- [ ] Check the share preview with the Facebook Sharing Debugger and LinkedIn Post Inspector.
- [ ] Re-run Lighthouse on the live URL (target 95+ in all four).
  - If Performance drops, the host isn't compressing or caching (see Deploy).
- [ ] Set up analytics. Plausible or GA4 are both fine. Add it with `defer` so it doesn't block rendering.
  - Track quote emails as a conversion. The form opens `mailto:info@qoot.com`.
- [ ] Get listed in Libyan business directories and chambers of commerce (Benghazi Chamber of Commerce, oil & gas supplier registers).
  - Directory links from these are the most useful backlinks for a B2B supplier.
- [ ] Each quarter:
  - Update `lastmod` in `sitemap.xml` whenever you change content.
  - Review Search Console queries, and add FAQ entries for the real questions people search.
- [ ] Consider separate pages later, as each one can rank on its own:
  - **Site camp catering** (مخيمات حقول النفط)
  - **Event catering** (إعاشة المناسبات)
  - **Wholesale supply price list**
