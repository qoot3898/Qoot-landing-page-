import { describe, expect, it } from 'vitest';
import { pageFor } from './routes';

describe('pageFor', () => {
  it.each([
    ['/admin', '/admin/index.html'],
    ['/admin/news/12', '/admin/index.html'],
    ['/news/', '/news.html'],
    ['/news/a-story?preview', '/news.html'],
    ['/en/news/', '/news.html'],
    ['/en/', '/index.html'],
    ['/en', '/index.html'],
  ])('%s → %s', (path, page) => expect(pageFor(path)).toBe(page));

  it.each(['/', '/static/app.js', '/assets/img/x.avif', '/api/public/site', '/newsletter'])('leaves %s alone', (path) =>
    expect(pageFor(path)).toBeNull(),
  );
});
