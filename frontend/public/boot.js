/* Runs before the page script: language and direction from the address, the visitor's language
   preference, and an early start on the content request. Kept tiny and free of inline code so the
   page can run under a strict Content-Security-Policy (script-src 'self'). */
(function () {
  var root = document.documentElement;
  var path = location.pathname;
  var en = /^\/en(\/|$)/.test(path);
  var lang = en ? 'en' : 'ar';
  root.lang = lang;
  root.dir = en ? 'ltr' : 'rtl';
  root.classList.add('js');
  // If the page script never starts, show the page without the entrance animations' hidden states.
  setTimeout(function () { if (!window.__qoot) root.classList.remove('js'); }, 4000);

  var preview = /[?&]preview(=|&|$)/.test(location.search);

  // A visitor who chose a language before is taken to it when arriving from elsewhere.
  try {
    var stored = localStorage.getItem('qoot.lang');
    var fromSite = document.referrer && document.referrer.indexOf(location.origin) === 0;
    if (!preview && !fromSite && (stored === 'ar' || stored === 'en') && stored !== lang) {
      var rest = en ? path.slice(3) || '/' : path;
      if (rest === '/' || rest === '/index.html' || rest.indexOf('/news/') === 0) {
        location.replace((stored === 'en' ? '/en' : '') + (rest === '/index.html' ? '/' : rest) + location.search + location.hash);
        return;
      }
    }
  } catch (e) { /* storage unavailable: stay on this language */ }

  if (preview) return;
  var meta = document.querySelector('meta[name="qoot-api"]');
  var base = meta && meta.content && meta.content.charAt(0) !== '%' ? meta.content.replace(/\/+$/, '') : '';
  function get(path) {
    var request = fetch(base + path, {
      credentials: 'include',
      headers: { Accept: 'application/json', 'Accept-Language': lang }
    });
    request.catch(function () { /* the page script retries and shows the error state */ });
    return request;
  }
  try {
    window.__qootSite = get('/api/public/site?lang=' + lang);
    // The news is asked for at the same time as the page, not after it (paths must match news-ui.ts).
    var early = {};
    var rest = en ? path.slice(3) || '/' : path;
    var article = /^\/news\/([^\/?#]+)\/?$/.exec(rest);
    if (rest === '/' || rest === '/index.html') {
      early['/api/public/news?lang=' + lang + '&page=1&pageSize=6'] = 0;
    } else if (/^\/news\/?$/.test(rest)) {
      var category = new URLSearchParams(location.search).get('category') || '';
      early['/api/public/news?lang=' + lang + '&page=1&pageSize=9' + (category ? '&category=' + encodeURIComponent(category) : '')] = 0;
      early['/api/public/news/categories?lang=' + lang] = 0;
    } else if (article) {
      early['/api/public/news/' + encodeURIComponent(decodeURIComponent(article[1])) + '?lang=' + lang] = 0;
    }
    for (var key in early) early[key] = get(key);
    window.__qootEarly = early;
  } catch (e) { /* old browser: the page script makes the requests */ }
})();
